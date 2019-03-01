// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using MixedRealityExtension.API;
using MixedRealityExtension.Core.Components;
using MixedRealityExtension.Core.Interfaces;
using MixedRealityExtension.Core.Types;
using MixedRealityExtension.Messaging.Commands;
using MixedRealityExtension.Messaging.Events.Types;
using MixedRealityExtension.Messaging.Payloads;
using MixedRealityExtension.Patching;
using MixedRealityExtension.Patching.Types;
using MixedRealityExtension.Util.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using UnityLight = UnityEngine.Light;
using UnityCollider = UnityEngine.Collider;

namespace MixedRealityExtension.Core
{
    /// <summary>
    /// Class that represents an actor in a mixed reality extension app.
    /// </summary>
    internal sealed class Actor : MixedRealityExtensionObject, ICommandHandlerContext, IActor
    {
        private Rigidbody _rigidbody;
        private UnityLight _light;
        private UnityCollider _collider;
        private LookAtComponent _lookAt;
        private float _nextUpdateTime;

        private Dictionary<Type, ActorComponentBase> _components = new Dictionary<Type, ActorComponentBase>();

        private Queue<Action<Actor>> _updateActions = new Queue<Action<Actor>>();

        private ActorComponentType _subscriptions = ActorComponentType.None;

        private new Renderer renderer = null;
        private Renderer Renderer => renderer = renderer ?? GetComponent<Renderer>();

        #region IActor Properties - Public

        /// <inheritdoc />
        [HideInInspector]
        public IActor Parent => transform.parent?.GetComponent<Actor>();

        /// <inheritdoc />
        [HideInInspector]
        public new string Name
        {
            get => transform.name;
            set => transform.name = value;
        }

        #endregion

        #region Properties - Internal

        internal Guid? ParentId { get; private set; }

        internal RigidBody RigidBody { get; private set; }

        internal Light Light { get; private set; }

        internal IText Text { get; private set; }
		
		internal Collider Collider { get; private set; }

        internal Attachment Attachment { get; } = new Attachment();
        private Attachment _cachedAttachment = new Attachment();

        internal MWTransform LocalTransform => transform.ToMWTransform();

        internal Guid? MaterialId { get; set; }
        private UnityEngine.Material originalMaterial;
        #endregion

        #region Methods - Internal

        internal ComponentT GetActorComponent<ComponentT>() where ComponentT : ActorComponentBase
        {
            if (_components.ContainsKey(typeof(ComponentT)))
            {
                return (ComponentT)_components[typeof(ComponentT)];
            }

            return null;
        }

        internal ComponentT GetOrCreateActorComponent<ComponentT>() where ComponentT : ActorComponentBase, new()
        {
            var component = GetActorComponent<ComponentT>();
            if (component == null)
            {
                component = gameObject.AddComponent<ComponentT>();
                component.AttachedActor = this;
                _components[typeof(ComponentT)] = component;
            }

            return component;
        }

        internal void SynchronizeApp()
        {
            if (CanSync())
            {
                // Handle changes in game state and raise appropriate events for network updates.
                var actorPatch = new ActorPatch(Id);

                // We need to detect for changes in parent on the client, and handle updating the server.
                var parentId = Parent?.Id ?? Guid.Empty;
                if (ParentId != parentId)
                {
                    // TODO @tombu - Determine if the new parent is an actor in OUR MRE.
                    // TODO: Add in MRE ID's to help identify whether the new parent is in our MRE or not, not just
                    // whether it is a MRE actor.
                    ParentId = parentId;
                    actorPatch.ParentId = ParentId;
                }

                if (ShouldSync(_subscriptions, ActorComponentType.Transform))
                {
                    GenerateTransformPatch(actorPatch);
                }

                if (ShouldSync(_subscriptions, ActorComponentType.Rigidbody))
                {
                    GenerateRigidBodyPatch(actorPatch);
                }

                if (ShouldSync(ActorComponentType.Attachment, ActorComponentType.Attachment))
                {
                    GenerateAttachmentPatch(actorPatch);
                }

                if (actorPatch.IsPatched())
                {
                    App.EventManager.QueueEvent(new ActorChangedEvent(Id, actorPatch));
                }
            }
        }

        internal void ApplyPatch(ActorPatch actorPatch)
        {
            PatchParent(actorPatch.ParentId);
            PatchName(actorPatch.Name);
            PatchMaterial(actorPatch.MaterialId);
            PatchTransform(actorPatch.Transform);
            PatchLight(actorPatch.Light);
            PatchRigidBody(actorPatch.RigidBody);
            PatchCollider(actorPatch.Collider);
            PatchText(actorPatch.Text);
            PatchAttachment(actorPatch.Attachment);
            PatchLookAt(actorPatch.LookAt);
        }

        internal void SynchronizeEngine(ActorPatch actorPatch)
        {
            _updateActions.Enqueue((actor) => ApplyPatch(actorPatch));
        }

        internal void ExecuteRigidBodyCommands(RigidBodyCommands commandPayload)
        {
            foreach (var command in commandPayload.CommandPayloads.OfType<ICommandPayload>())
            {
                App.ExecuteCommandPayload(this, command);
            }
        }

        internal void Destroy()
        {
            CleanUp();
            Destroy(gameObject);
        }

        internal ActorPatch GenerateInitialPatch()
        {
            if (ParentId == null)
            {
                ParentId = Parent?.Id ?? Guid.Empty;
            }

            var transform = new TransformPatch()
            {
                Position = new Vector3Patch(Transform.Position),
                Rotation = new QuaternionPatch(Transform.Rotation),
                Scale = new Vector3Patch(Transform.Scale)
            };

            var rigidBody = PatchingUtilMethods.GeneratePatch(RigidBody, (Rigidbody)null, App.SceneRoot.transform);

            ColliderPatch collider = null;
            _collider = gameObject.GetComponent<UnityCollider>();
            if (_collider != null)
            {
                Collider = new Collider(_collider);
                collider = Collider.GenerateInitialPatch();
            }

            var actorPatch = new ActorPatch(Id)
            {
                ParentId = ParentId,
                Name = Name,
                Transform = transform,
                RigidBody = rigidBody,
                MaterialId = MaterialId,
                Collider = collider
            };

            return (!actorPatch.IsPatched()) ? null : actorPatch;
        }

        internal OperationResult EnableRigidBody(RigidBodyPatch rigidBodyPatch)
        {
            if (AddRigidBody() != null)
            {
                if (rigidBodyPatch != null)
                {
                    PatchRigidBody(rigidBodyPatch);
                }

                return new OperationResult()
                {
                    ResultCode = OperationResultCode.Success
                };
            }

            return new OperationResult()
            {
                ResultCode = OperationResultCode.Error,
                Message = string.Format("Failed to create and enable the rigidbody for actor with id {0}", Id)
            };
        }

        internal OperationResult EnableLight(LightPatch lightPatch)
        {
            if (AddLight() != null)
            {
                if (lightPatch != null)
                {
                    PatchLight(lightPatch);
                }

                return new OperationResult()
                {
                    ResultCode = OperationResultCode.Success
                };
            }

            return new OperationResult()
            {
                ResultCode = OperationResultCode.Error,
                Message = string.Format("Failed to create and enable the light for actor with id {0}", Id)
            };
        }

        internal OperationResult EnableText(TextPatch textPatch)
        {
            if (AddText() != null)
            {
                if (textPatch != null)
                {
                    PatchText(textPatch);
                }

                return new OperationResult()
                {
                    ResultCode = OperationResultCode.Success
                };
            }

            return new OperationResult()
            {
                ResultCode = OperationResultCode.Error,
                Message = string.Format("Failed to create and enable the text object for actor with id {0}", Id)
            };
        }

        internal IActor GetParent()
        {
            return ParentId != null ? App.FindActor(ParentId.Value) : Parent;
        }

        internal void AddSubscriptions(IEnumerable<ActorComponentType> adds)
        {
            if (adds != null)
            {
                foreach (var subscription in adds)
                {
                    _subscriptions |= subscription;
                }
            }
        }

        internal void RemoveSubscriptions(IEnumerable<ActorComponentType> removes)
        {
            if (removes != null)
            {
                foreach (var subscription in removes)
                {
                    _subscriptions &= ~subscription;
                }
            }
        }

        internal void SendActorUpdate(ActorComponentType flags)
        {
            ActorPatch actorPatch = new ActorPatch(Id);

            if ((flags & ActorComponentType.Transform) != ActorComponentType.None)
            {
                actorPatch.Transform = transform.ToMWTransform().AsPatch();
            }

            //if ((flags & SubscriptionType.Rigidbody) != SubscriptionType.None)
            //{
            //    actorPatch.Transform = this.RigidBody.AsPatch();
            //}

            if (actorPatch.IsPatched())
            {
                App.EventManager.QueueEvent(new ActorChangedEvent(Id, actorPatch));
            }
        }

        #endregion

        #region MonoBehaviour Virtual Methods

        protected override void OnStart()
        {
            _rigidbody = gameObject.GetComponent<Rigidbody>();
            _light = gameObject.GetComponent<UnityLight>();
        }

        protected override void OnDestroyed()
        {
            // TODO @tombu, @eanders - We need to decide on the correct cleanup timing here for multiplayer, as this could cause a potential
            // memory leak if the engine deletes game objects, and we don't do proper cleanup here.
            //CleanUp();
            //App.OnActorDestroyed(this.Id);

            IUserInfo userInfo = MREAPI.AppsAPI.UserInfoProvider.GetUserInfo(App, Attachment.UserId);
            if (userInfo != null)
            {
                userInfo.BeforeAvatarDestroyed -= UserInfo_BeforeAvatarDestroyed;
            }
        }

        protected override void InternalUpdate()
        {
            try
            {
                while (_updateActions.Count > 0)
                {
                    _updateActions.Dequeue()(this);
                }

                // TODO: Add ability to flag an actor for "high-frequency" updates
                if (Time.time >= _nextUpdateTime)
                {
                    _nextUpdateTime = Time.time + 0.2f + UnityEngine.Random.Range(-0.1f, 0.1f);
                    SynchronizeApp();
                }
            }
            catch (Exception e)
            {
                MREAPI.Logger.LogError($"Failed to synchronize app.  Exception: {e.Message}\nStackTrace: {e.StackTrace}");
            }
        }

        protected override void InternalFixedUpdate()
        {
            try
            {
                if (_rigidbody == null)
                {
                    return;
                }

                RigidBody = RigidBody ?? new RigidBody(_rigidbody, App.SceneRoot.transform);
                RigidBody.Update();
                // TODO: Send this update if actor is set to "high-frequency" updates
                //Actor.SynchronizeApp();
            }
            catch (Exception e)
            {
                MREAPI.Logger.LogError($"Failed to update rigid body.  Exception: {e.Message}\nStackTrace: {e.StackTrace}");
            }
        }

        #endregion

        #region Methods - Private

        private Attachment FindAttachmentInHierarchy()
        {
            Attachment FindAttachmentRecursive(Actor actor)
            {
                if (actor == null)
                {
                    return null;
                }
                if (actor.Attachment.AttachPoint != null && actor.Attachment.UserId != Guid.Empty)
                {
                    return actor.Attachment;
                }
                return FindAttachmentRecursive(actor.Parent as Actor);
            };
            return FindAttachmentRecursive(this);
        }

        private void DetachFromAttachPointParent()
        {
            try
            {
                if (transform != null)
                {
                    var attachmentComponent = transform.parent.GetComponents<MREAttachmentComponent>()
                        .FirstOrDefault(component =>
                            component.Actor != null &&
                            component.Actor.Id == Id &&
                            component.Actor.AppInstanceId == AppInstanceId &&
                            component.UserId == _cachedAttachment.UserId);

                    if (attachmentComponent != null)
                    {
                        attachmentComponent.Actor = null;
                        Destroy(attachmentComponent);
                    }

                    transform.SetParent(App.SceneRoot.transform, true);
                }
            }
            catch (Exception e)
            {
                MREAPI.Logger.LogError($"Exception: {e.Message}\nStackTrace: {e.StackTrace}");
            }
        }

        private bool PerformAttach()
        {
            // Assumption: Attachment state has changed and we need to (potentially) detach and (potentially) reattach.
            try
            {
                DetachFromAttachPointParent();

                IUserInfo userInfo = MREAPI.AppsAPI.UserInfoProvider.GetUserInfo(App, Attachment.UserId);
                if (userInfo != null)
                {
                    userInfo.BeforeAvatarDestroyed -= UserInfo_BeforeAvatarDestroyed;

                    Transform attachPoint = userInfo.GetAttachPoint(Attachment.AttachPoint);
                    if (attachPoint != null)
                    {
                        var attachmentComponent = attachPoint.gameObject.AddComponent<MREAttachmentComponent>();
                        attachmentComponent.Actor = this;
                        attachmentComponent.UserId = Attachment.UserId;
                        transform.SetParent(attachPoint, false);
                        userInfo.BeforeAvatarDestroyed += UserInfo_BeforeAvatarDestroyed;
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                MREAPI.Logger.LogError($"Exception: {e.Message}\nStackTrace: {e.StackTrace}");
            }

            return false;
        }

        private void UserInfo_BeforeAvatarDestroyed()
        {
            // Remember the original local transform.
            MWTransform cachedTransform = LocalTransform;

            // Detach from parent. This will preserve the world transform (changing the local transform).
            // This is desired so that the actor doesn't change position, but we must restore the local
            // transform when reattaching.
            DetachFromAttachPointParent();

            IUserInfo userInfo = MREAPI.AppsAPI.UserInfoProvider.GetUserInfo(App, Attachment.UserId);
            if (userInfo != null)
            {
                void Reattach()
                {
                    // Restore the local transform and reattach.
                    userInfo.AfterAvatarCreated -= Reattach;
                    // In the interim time this actor might have been destroyed.
                    if (transform != null)
                    {
                        transform.localPosition = cachedTransform.Position.ToVector3();
                        transform.localRotation = cachedTransform.Rotation.ToQuaternion();
                        transform.localScale = cachedTransform.Scale.ToVector3();
                        PerformAttach();
                    }
                }

                // Register for a callback once the avatar is recreated.
                userInfo.AfterAvatarCreated += Reattach;
            }
        }

        private IText AddText()
        {
            Text = MREAPI.AppsAPI.TextFactory.CreateText(this);
            return Text;
        }

        private Light AddLight()
        {
            if (_light == null)
            {
                _light = gameObject.AddComponent<UnityLight>();
                Light = new Light(_light);
            }
            return Light;
        }

        private RigidBody AddRigidBody()
        {
            if (_rigidbody == null)
            {
                _rigidbody = gameObject.AddComponent<Rigidbody>();
                RigidBody = new RigidBody(_rigidbody, App.SceneRoot.transform);
            }
            return RigidBody;
        }

        private Collider SetCollider(ColliderPatch colliderPatch)
        {
            if (colliderPatch == null || colliderPatch.ColliderGeometry == null)
            {
                return null;
            }

            var colliderGeometry = colliderPatch.ColliderGeometry;
            var colliderType = colliderGeometry.ColliderType;

            if (_collider != null)
            {
                if (Collider.ColliderType == colliderType)
                {
                    // We have a collider already of the same type as the desired new geometry.
                    // Update its values instead of removing and adding a new one.
                    colliderGeometry.Patch(_collider);
                    return Collider;
                }
                else
                {
                    Destroy(_collider);
                    _collider = null;
                    Collider = null;
                }
            }

            UnityCollider unityCollider = null;

            switch (colliderType)
            {
                case ColliderType.Box:
                    var boxCollider = gameObject.AddComponent<BoxCollider>();
                    colliderGeometry.Patch(boxCollider);
                    unityCollider = boxCollider;
                    break;
                case ColliderType.Sphere:
                    var sphereCollider = gameObject.AddComponent<SphereCollider>();
                    colliderGeometry.Patch(sphereCollider);
                    unityCollider = sphereCollider;
                    break;
                default:
                    MREAPI.Logger.LogWarning("Cannot add the given collider type to the actor " +
                        $"during runtime.  Collider Type: {colliderPatch.ColliderGeometry.ColliderType}");
                    break;
            }

            Collider = (unityCollider != null) ? new Collider(_collider) : null;
            return Collider;
        }

        private void PatchParent(Guid? parentIdOrNull)
        {
            var parentId = parentIdOrNull ?? ParentId;
            var parent = parentId != null ? App.FindActor(parentId.Value) : Parent;
            if (parent != null && (Parent == null || (Parent.Id != parent.Id)))
            {
                transform.SetParent(((Actor)parent).transform, false);
            }
            else if (parent == null && Parent != null)
            {
                // TODO: Unparent?
            }
        }

        private void PatchName(string nameOrNull)
        {
            if (nameOrNull != null)
            {
                Name = nameOrNull;
            }
        }

        private void PatchMaterial(Guid? materialIdOrNull)
        {
            if (Renderer != null)
            {
                if (originalMaterial == null)
                {
                    originalMaterial = Instantiate(Renderer.sharedMaterial);
                }

                if (materialIdOrNull == Guid.Empty)
                {
                    Renderer.sharedMaterial = originalMaterial;
                }
                else if (materialIdOrNull != null)
                {
                    MaterialId = materialIdOrNull.Value;
                    var sharedMat = MREAPI.AppsAPI.AssetCache.GetAsset(MaterialId) as Material;
                    if (sharedMat != null)
                    {
                        Renderer.sharedMaterial = sharedMat;
                    }
                    else
                    {
                        MREAPI.Logger.LogWarning($"Material {MaterialId} not found, cannot assign to actor {Id}");
                    }
                }
            }
        }

        private void PatchTransform(TransformPatch transformPatch)
        {
            if (transformPatch != null)
            {
                if (RigidBody == null)
                {
                    transform.localPosition = transform.localPosition.GetPatchApplied(LocalTransform.Position.ApplyPatch(transformPatch.Position));
                    transform.localRotation = transform.localRotation.GetPatchApplied(LocalTransform.Rotation.ApplyPatch(transformPatch.Rotation));
                    transform.localScale = transform.localScale.GetPatchApplied(LocalTransform.Scale.ApplyPatch(transformPatch.Scale));
                }
                else
                {
                    // In case of rigid body:
                    // - Apply scale directly.
                    transform.localScale = transform.localScale.GetPatchApplied(LocalTransform.Scale.ApplyPatch(transformPatch.Scale));
                    // - Apply position and rotation via rigid body.
                    var position = transform.localPosition.GetPatchApplied(LocalTransform.Position.ApplyPatch(transformPatch.Position));
                    var rotation = transform.localRotation.GetPatchApplied(LocalTransform.Rotation.ApplyPatch(transformPatch.Rotation));
                    RigidBodyPatch rigidBodyPatch = new RigidBodyPatch()
                    {
                        Position = new Vector3Patch(position),
                        Rotation = new QuaternionPatch(rotation)
                    };
                    // Queue update to happen in the fixed update
                    RigidBody.SynchronizeEngine(rigidBodyPatch);
                }
            }
        }

        private void PatchLight(LightPatch lightPatch)
        {
            if (lightPatch != null)
            {
                if (Light == null)
                {
                    AddLight();
                }
                Light.SynchronizeEngine(lightPatch);
            }
        }

        private void PatchRigidBody(RigidBodyPatch rigidBodyPatch)
        {
            if (rigidBodyPatch != null)
            {
                if (RigidBody == null)
                {
                    AddRigidBody();
                    RigidBody.ApplyPatch(rigidBodyPatch);
                }
                else
                {
                    // Queue update to happen in the fixed update
                    RigidBody.SynchronizeEngine(rigidBodyPatch);
                }
            }
        }

        private void PatchText(TextPatch textPatch)
        {
            if (textPatch != null)
            {
                if (Text == null)
                {
                    AddText();
                }
                Text.SynchronizeEngine(textPatch);
            }
        }

        private void PatchCollider(ColliderPatch colliderPatch)
        {
            if (colliderPatch != null)
            {
                // A collider patch that contains collider geometry signals that we need to update the
                // collider to match the desired geometry.
                if (colliderPatch.ColliderGeometry != null)
                {
                    SetCollider(colliderPatch);
                }

                Collider?.SynchronizeEngine(colliderPatch);
            }
        }

        private void PatchAttachment(AttachmentPatch attachmentPatch)
        {
            if (attachmentPatch != null && attachmentPatch.IsPatched() && !attachmentPatch.Equals(Attachment))
            {
                Attachment.ApplyPatch(attachmentPatch);
                if (!PerformAttach())
                {
                    Attachment.Clear();
                }
            }
        }

        private void PatchLookAt(LookAtPatch lookAtPatch)
        {
            if (lookAtPatch != null)
            {
                if (_lookAt == null)
                {
                    _lookAt = GetOrCreateActorComponent<LookAtComponent>();
                }
                _lookAt.ApplyPatch(lookAtPatch);
            }
        }

        private void GenerateTransformPatch(ActorPatch actorPatch)
        {
            actorPatch.Transform = PatchingUtilMethods.GeneratePatch(Transform, gameObject.transform);
            Transform = gameObject.transform.ToMWTransform();
        }

        private void GenerateRigidBodyPatch(ActorPatch actorPatch)
        {
            if (_rigidbody != null && RigidBody != null)
            {
                // convert to a RigidBody and build a patch from the old one to this one.
                var rigidBodyPatch = PatchingUtilMethods.GeneratePatch(RigidBody, _rigidbody, App.SceneRoot.transform);
                if (rigidBodyPatch != null && rigidBodyPatch.IsPatched())
                {
                    actorPatch.RigidBody = rigidBodyPatch;
                }

                RigidBody.Update(_rigidbody);
            }
        }

        private void GenerateAttachmentPatch(ActorPatch actorPatch)
        {
            actorPatch.Attachment = Attachment.GeneratePatch(_cachedAttachment);
            if (actorPatch.Attachment != null)
            {
                _cachedAttachment.CopyFrom(Attachment);
            }
        }

        private void CleanUp()
        {
            foreach (var component in _components.Values)
            {
                component.CleanUp();
            }
        }

        private bool ShouldSync(ActorComponentType subscriptions, ActorComponentType flag)
        {
            // We do not want to send actor updates until we're fully joined to the app.
            // TODO: We shouldn't need to do this check. The engine shouldn't try to send
            // updates until we're fully joined to the app.
            if (!(App.Protocol is Messaging.Protocols.Execution))
            {
                return false;
            }

            // If the actor has a rigid body then always sync the transform and the rigid body.
            if (RigidBody != null)
            {
                subscriptions |= ActorComponentType.Transform;
                subscriptions |= ActorComponentType.Rigidbody;
            }

            Attachment attachmentInHierarchy = FindAttachmentInHierarchy();
            bool inAttachmentHeirarchy = (attachmentInHierarchy != null);
            bool inOwnedAttachmentHierarchy = (inAttachmentHeirarchy && attachmentInHierarchy.UserId == LocalUser.Id);

            // Don't sync anything if the actor is in an attachment hierarchy on a remote avatar.
            if (inAttachmentHeirarchy && !inOwnedAttachmentHierarchy)
            {
                subscriptions = ActorComponentType.None;
            }

            if ((subscriptions & flag) != ActorComponentType.None)
            {
                return
                    (App.OperatingModel == OperatingModel.ServerAuthoritative) ||
                    App.IsAuthoritativePeer ||
                    inOwnedAttachmentHierarchy;
            }

            return false;
        }

        private bool CanSync()
        {
            // We do not want to send actor updates until we're fully joined to the app.
            // TODO: We shouldn't need to do this check. The engine shouldn't try to send
            // updates until we're fully joined to the app.
            if (!(App.Protocol is Messaging.Protocols.Execution))
            {
                return false;
            }

            Attachment attachmentInHierarchy = FindAttachmentInHierarchy();
            bool inAttachmentHeirarchy = (attachmentInHierarchy != null);
            bool inOwnedAttachmentHierarchy = (inAttachmentHeirarchy && attachmentInHierarchy.UserId == LocalUser.Id);

            // We can send actor updates to the app if we're operating in a server-authoritative model,
            // or if we're in a peer-authoritative model and we've been designated the authoritative peer.
            // Override the previous rules if this actor is in an attachment hierarchy owned by the local player.
            if (App.OperatingModel == OperatingModel.ServerAuthoritative ||
                App.IsAuthoritativePeer ||
                inOwnedAttachmentHierarchy)
            {
                return true;
            }

            return false;
        }

        #endregion

        #region Command Handlers - Rigid Body Commands

        [CommandHandler(typeof(RBMovePosition))]
        private void OnRBMovePosition(RBMovePosition payload)
        {
            RigidBody?.RigidBodyMovePosition(new MWVector3().ApplyPatch(payload.Position));
        }

        [CommandHandler(typeof(RBMoveRotation))]
        private void OnRBMoveRotation(RBMoveRotation payload)
        {
            RigidBody?.RigidBodyMoveRotation(new MWQuaternion().ApplyPatch(payload.Rotation));
        }

        [CommandHandler(typeof(RBAddForce))]
        private void OnRBAddForce(RBAddForce payload)
        {
            RigidBody?.RigidBodyAddForce(new MWVector3().ApplyPatch(payload.Force));
        }

        [CommandHandler(typeof(RBAddForceAtPosition))]
        private void OnRBAddForceAtPosition(RBAddForceAtPosition payload)
        {
            var force = new MWVector3().ApplyPatch(payload.Force);
            var position = new MWVector3().ApplyPatch(payload.Position);
            RigidBody?.RigidBodyAddForceAtPosition(force, position);
        }

        [CommandHandler(typeof(RBAddTorque))]
        private void OnRBAddTorque(RBAddTorque payload)
        {
            RigidBody?.RigidBodyAddTorque(new MWVector3().ApplyPatch(payload.Torque));
        }

        [CommandHandler(typeof(RBAddRelativeTorque))]
        private void OnRBAddRelativeTorque(RBAddRelativeTorque payload)
        {
            RigidBody?.RigidBodyAddRelativeTorque(new MWVector3().ApplyPatch(payload.RelativeTorque));
        }

        #endregion
    }
}
