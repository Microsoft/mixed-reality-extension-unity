// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using MixedRealityExtension.Animation;
using MixedRealityExtension.Behaviors;
using MixedRealityExtension.Core;
using MixedRealityExtension.Patching.Types;
using System;
using System.Collections.Generic;
using MixedRealityExtension.Core.Types;
using Newtonsoft.Json.Linq;
using MixedRealityExtension.Controllers;

namespace MixedRealityExtension.Messaging.Payloads
{
    /// <summary>
    /// App => Engine
    /// Payload that contains a remote procedure call to be made in the engine.
    /// </summary>
    public class AppToEngineRPC : NetworkCommandPayload
    {
        /// <summary>
        /// The name of the remote procedure call.
        /// </summary>
        public string ProcName { get; set; }

        /// <summary>
        /// The arguments to the remote procedure call.
        /// </summary>
        public JArray Args { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to create an asset.
    /// </summary>
    public abstract class CreateActor : NetworkCommandPayload
    {
        /// <summary>
        /// The initial actor patch to apply to the newly created actor.
        /// </summary>
        public ActorPatch Actor { get; set; }

        /// <summary>
        /// The subscriptions to register for on the actor.
        /// </summary>
        public List<SubscriptionType> Subscriptions { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to create an asset from the host library.
    /// </summary>
    public class CreateFromLibrary : CreateActor
    {
        /// <summary>
        /// The resource url for the asset bundle.
        /// </summary>
        public string ResourceId { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to create an asset from a GLTF.
    /// </summary>
    public class CreateFromGLTF : CreateActor
    {
        /// <summary>
        /// The resource url for the GLTF.
        /// </summary>
        public string ResourceUrl { get; set; }

        /// <summary>
        /// The assent name within the GLTF to instantiate.
        /// </summary>
        public string AssetName { get; set; }

        /// <summary>
        /// The type of collider to add to the actor upon creation.
        /// </summary>
        public ColliderType ColliderType { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to create a primitive.
    /// </summary>
    public class CreatePrimitive : CreateActor
    {
        /// <summary>
        /// The primitive shape to create.
        /// </summary>
        public PrimitiveDefinition Definition { get; set; }

        /// <summary>
        /// Whether to add a collider to the primitive upon creation.
        /// </summary>
        public bool AddCollider { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to create an empty actor.
    /// </summary>
    public class CreateEmpty : CreateActor
    {
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to enable a rigid body on an actor.
    /// </summary>
    public class EnableRigidBody : NetworkCommandPayload
    {
        /// <summary>
        /// The id of the actor to enable the rigid body on.
        /// </summary>
        public Guid ActorId { get; set; }

        /// <summary>
        /// The initial patch to apply to the rigid body.
        /// </summary>
        public RigidBodyPatch RigidBody { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to update an actor with a patch.
    /// </summary>
    public class ActorUpdate : NetworkCommandPayload
    {
        /// <summary>
        /// The actor patch to apply to the actor assocaited with the patch.
        /// </summary>
        public ActorPatch Actor { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to update an actor with a patch, interpolated.
    /// </summary>
    public class ActorCorrection : NetworkCommandPayload
    {
        /// <summary>
        /// The actor patch to apply to the actor assocaited with the patch.
        /// </summary>
        public ActorPatch Actor { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to destroy one or more actors.
    /// </summary>
    public class DestroyActors : NetworkCommandPayload
    {
        /// <summary>
        /// The enumeration of ids for the actors to be destroyed.
        /// </summary>
        public IEnumerable<Guid> ActorIds { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to update state through an enumeration of payloads to execute on.
    /// </summary>
    public class StateUpdate : NetworkCommandPayload
    {
        /// <summary>
        /// The enumeration of payloads to executed on.
        /// </summary>
        public IEnumerable<Payload> Payloads { get; set; }
    }

    /// <summary>
    /// Payload for when the app needs to restore the state of a set of actors.
    /// </summary>
    public class StateRestore : NetworkCommandPayload
    {
        /// <summary>
        /// The enumeration of actor patchs to apply to their corresponding actors.
        /// </summary>
        public IEnumerable<ActorPatch> Actors { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to enable a light on an actor.
    /// </summary>
    public class EnableLight : NetworkCommandPayload
    {
        /// <summary>
        /// The id of the actor to enable a light on.
        /// </summary>
        public Guid ActorId { get; set; }

        /// <summary>
        /// The initial light patch to apply to the light.
        /// </summary>
        public LightPatch Light { get; set; }
    }

	/// <summary>
	/// App => Engine
	/// Payload for when the app wants to set text on an actor
	/// </summary>
	public class EnableText : NetworkCommandPayload
	{
		/// <summary>
		/// The id of the actor to add text to
		/// </summary>
		public Guid ActorId { get; set; }

		/// <summary>
		/// The initial text patch
		/// </summary>
		public TextPatch Text { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to update the subscriptions registered for a specific owner type.
    /// </summary>
    public class UpdateSubscriptions : NetworkCommandPayload
    {
        /// <summary>
        /// The id of the mixed reality extension object to update subscriptions for.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The subscription owner type. See <see cref="SubscriptionOwnerType"/>.
        /// </summary>
        public SubscriptionOwnerType OwnerType { get; set; }

        /// <summary>
        /// The subscription types to add to the object. See <see cref="SubscriptionType"/>.
        /// </summary>
        public IEnumerable<SubscriptionType> Adds { get; set; }

        /// <summary>
        /// The subscription types to remove from the object. See <see cref="SubscriptionType"/>.
        /// </summary>
        public IEnumerable<SubscriptionType> Removes { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to execute one or more commands on the rigid body of an actor.
    /// </summary>
    public class RigidBodyCommands : NetworkCommandPayload
    {
        /// <summary>
        /// The id of the actor to execute rigid body commands on.
        /// </summary>
		public Guid ActorId { get; set; }

        /// <summary>
        /// The enumeration of command payloads to executed on the rigid body.
        /// </summary>
        public IEnumerable<Payload> CommandPayloads { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to execute an add force command on an actor's rigid body.
    /// </summary>
    public class RBAddForce : NetworkCommandPayload
    {
        /// <summary>
        /// The force patch to apply to the rigid body.
        /// </summary>
        public Vector3Patch Force { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to execute an add force at position command on an actor's rigid body.
    /// </summary>
    public class RBAddForceAtPosition : NetworkCommandPayload
    {
        /// <summary>
        /// The force patch to apply to the rigid body.
        /// </summary>
        public Vector3Patch Force { get; set; }

        /// <summary>
        /// The position at which to apply the force to the rigid body.
        /// </summary>
        public Vector3Patch Position { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to execute an add torque command on an actor's rigid body.
    /// </summary>
    public class RBAddTorque : NetworkCommandPayload
    {
        /// <summary>
        /// The torque patch to add to the rigid body.
        /// </summary>
        public Vector3Patch Torque { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to execute an add relative torque command on an actor's rigid body.
    /// </summary>
    public class RBAddRelativeTorque : NetworkCommandPayload
    {
        /// <summary>
        /// The relative torque patch to add to the rigid body.
        /// </summary>
        public Vector3Patch RelativeTorque { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to execute a move position command on an actor's rigid body.
    /// </summary>
    public class RBMovePosition : NetworkCommandPayload
    {
        /// <summary>
        /// The position patch to move the rigid body to.
        /// </summary>
        public Vector3Patch Position { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to execute a move rotation command on an actor's rigid body.
    /// </summary>
    public class RBMoveRotation : NetworkCommandPayload
    {
        /// <summary>
        /// The rotation patch to move the rigid body to.
        /// </summary>
        public QuaternionPatch Rotation { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to create an animation on a specific actor.
    /// </summary>
    public class CreateAnimation : NetworkCommandPayload
    {
        /// <summary>
        /// The id of the actor to create the animation on.
        /// </summary>
        public Guid ActorId { get; set; }

        /// <summary>
        /// The name of the animation to create.
        /// </summary>
        public string AnimationName { get; set; }

        /// <summary>
        /// The enumeration of animation key frames to set to the animation.
        /// </summary>
        public IEnumerable<MWAnimationKeyframe> Keyframes { get; set; }

        /// <summary>
        /// The enumeration of animation events to set to the animation.
        /// </summary>
        public IEnumerable<MWAnimationEvent> Events { get; set; }

        /// <summary>
        /// The wrap mode of the animation. See <see cref="MWAnimationWrapMode"/>.
        /// </summary>
        public MWAnimationWrapMode WrapMode { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to start an animation.
    /// </summary>
    public class StartAnimation : NetworkCommandPayload
    {
        /// <summary>
        /// The id of the actor to start the animation on.
        /// </summary>
        public Guid ActorId { get; set; }

        /// <summary>
        /// The name of the animation to start.
        /// </summary>
        public string AnimationName { get; set; }

        /// <summary>
        /// (Optional) The time code (in seconds) at which to start the animation.
        /// </summary>
        public float? AnimationTime { get; set; }

        /// <summary>
        /// (Optional) Whether or not to start the animation in the paused state.
        /// </summary>
        public bool? Paused { get; set; }

        /// <summary>
        /// (Optional) Whether or not to the animation should apply root motion when stopped/restarted.
        /// </summary>
        public bool? HasRootMotion { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to stop an animation.
    /// </summary>
    public class StopAnimation : NetworkCommandPayload
    {
        /// <summary>
        /// The id of the actor to stop the animation on.
        /// </summary>
        public Guid ActorId { get; set; }

        /// <summary>
        /// The name of the animation to stop.
        /// </summary>
        public string AnimationName { get; set; }

        /// <summary>
        /// (Optional) The time offset into the animation when it was stopped.
        /// </summary>
        public float? AnimationTime { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to reset an animation.
    /// </summary>
    public class ResetAnimation : NetworkCommandPayload
    {
        /// <summary>
        /// The id of the actor to reset the animation on.
        /// </summary>
        public Guid ActorId { get; set; }

        /// <summary>
        /// The name of the animation to reset.
        /// </summary>
        public string AnimationName { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to reset an animation.
    /// </summary>
    public class PauseAnimation : NetworkCommandPayload
    {
        /// <summary>
        /// The id of the actor to reset the animation on.
        /// </summary>
        public Guid ActorId { get; set; }

        /// <summary>
        /// The name of the animation to reset.
        /// </summary>
        public string AnimationName { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to reset an animation.
    /// </summary>
    public class ResumeAnimation : NetworkCommandPayload
    {
        /// <summary>
        /// The id of the actor to reset the animation on.
        /// </summary>
        public Guid ActorId { get; set; }

        /// <summary>
        /// The name of the animation to reset.
        /// </summary>
        public string AnimationName { get; set; }
    }

    /// <summary>
    /// Bidirectional
    /// Payload to sync animation states between peers.
    /// </summary>
    public class SyncAnimations : NetworkCommandPayload
    {
        public IEnumerable<MWAnimationState> AnimationStates { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Payload for when the app wants to set the behavior on an actor.
    /// </summary>
    public class SetBehavior : NetworkCommandPayload
    {
        /// <summary>
        /// The id of the actor to add the behaviors to.
        /// </summary>
        public Guid ActorId { get; set; }

        /// <summary>
        /// The type of behavior to set as the primary behavior. See <see cref="BehaviorType"/>.
        /// </summary>
        public BehaviorType BehaviorType { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Specific to multi-peer adapter: Sets whether this client is "authoritative". When authoritative, the client
    /// sends additional actor updates to the app (rigidbody updates, animation events, etc).
    /// </summary>
    public class SetAuthoritative : NetworkCommandPayload
    {
        /// <summary>
        /// Whether or not this client is authoritative.
        /// </summary>
        public bool Authoritative { get; set; }
    }

    /// <summary>
    /// App => Engine
    /// Tells an actor to look at another actor or user.
    /// </summary>
    public class LookAt : NetworkCommandPayload
    {
        /// <summary>
        /// The Id of the actor that will do the looking at.
        /// </summary>
        public Guid ActorId { get; set; }
        /// <summary>
        /// The target object to look at.
        /// </summary>
        public Guid? TargetId { get; set; }
        /// <summary>
        /// How to look at the target object.
        /// </summary>
        public LookAtMode LookAtMode { get; set; }
    }
}
