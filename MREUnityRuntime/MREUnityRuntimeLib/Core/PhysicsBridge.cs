﻿using MixedRealityExtension.Core.Physics;
using System;
using System.Collections.Generic;

namespace MixedRealityExtension.Core
{
	public class PhysicsBridge
	{
		/// <summary>
		/// to monitor the collisions between frames this we need to store per contact pair. 
		/// </summary>
		public class CollisionMonitorInfo
		{
			public float timeFromStartCollision = 0.0f;
			public float relativeDistance = float.MaxValue;
			/// how much we interpolate between the jitter buffer and the simulation
			public float keyframedInterpolationRatio = 0.0f;
		}

		/// <summary>
		///  for interactive collisions we need to store the implicit velocities and other stuff to know
		/// </summary>
		public class CollisionSwitchInfo
		{
			public UnityEngine.Vector3 startPosition;
			public UnityEngine.Quaternion startOrientation;
			public UnityEngine.Vector3 linearVelocity;
			public UnityEngine.Vector3 angularVelocity;
			public Guid rigidBodyId;
			public CollisionMonitorInfo monitorInfo;
		}

		private class RigidBodyInfo
		{
			public RigidBodyInfo(Guid id, UnityEngine.Rigidbody rb, bool ownership)
			{
				Id = id;
				RigidBody = rb;
				Ownership = ownership;
				lastTimeKeyFramedUpdate = 0.0f;
				lastValidLinerVelocity.Set(0.0f, 0.0f, 0.0f);
				lastValidAngularVelocity.Set(0.0f, 0.0f, 0.0f);

				IsKeyframed = false;
			}

			public Guid Id;

			public UnityEngine.Rigidbody RigidBody;

			/// these 3 fields are used to store the actual velocities
			public float lastTimeKeyFramedUpdate;
			public UnityEngine.Vector3 lastValidLinerVelocity;
			public UnityEngine.Vector3 lastValidAngularVelocity;

			/// true if this rigid body is owned by this client
			public bool Ownership;

			public bool IsKeyframed;
		}

		private int _countOwnedTransforms = 0;
		private int _countStreamedTransforms = 0;

		private SortedList<Guid, RigidBodyInfo> _rigidBodies = new SortedList<Guid, RigidBodyInfo>();

		TimeSnapshotManager _snapshotManager = new TimeSnapshotManager();

		/// when we update a body we compute the implicit velocity. This velocity is needed, in case of collisions to switch from kinematic to kinematic=false
		List<CollisionSwitchInfo> _switchCollisionInfos = new List<CollisionSwitchInfo>();

		/// input is Guid from the remote body (in case of multiple collisions take the minimum
		Dictionary<Guid, CollisionMonitorInfo> _monitorCollisionInfo = new Dictionary<Guid, CollisionMonitorInfo>();

		public PhysicsBridge()
		{
		}

		#region Rigid Body Management

		public void addRigidBody(Guid id, UnityEngine.Rigidbody rigidbody, bool ownership)
		{
			UnityEngine.Debug.Assert(!_rigidBodies.ContainsKey(id), "PhysicsBridge already has an entry for rigid body with specified ID.");

			_rigidBodies.Add(id, new RigidBodyInfo(id, rigidbody, ownership));

			if (ownership)
			{
				rigidbody.isKinematic = false;
				_countOwnedTransforms++;
			}
			else
			{
				rigidbody.isKinematic = true;
				_countStreamedTransforms++;
			}
		}

		public void removeRigidBody(Guid id)
		{
			UnityEngine.Debug.Assert(_rigidBodies.ContainsKey(id), "PhysicsBridge don't have rigid body with specified ID.");

			var rb = _rigidBodies[id];

			if (rb.Ownership)
			{
				_countOwnedTransforms--;
			}
			else
			{
				_countStreamedTransforms--;
			}

			_rigidBodies.Remove(id);
		}

		public void setRigidBodyOwnership(Guid id, bool ownership)
		{
			UnityEngine.Debug.Assert(_rigidBodies.ContainsKey(id), "PhysicsBridge don't have rigid body with specified ID.");
			UnityEngine.Debug.Assert(_rigidBodies[id].Ownership != ownership, "Rigid body with specified ID is already registered with same ownership flag.");

			if (ownership)
			{
				_countOwnedTransforms++;
				_countStreamedTransforms--;
			}
			else
			{
				_countOwnedTransforms--;
				_countStreamedTransforms++;
			}

			_rigidBodies[id].Ownership = ownership;
		}

		public void setKeyframed(Guid id, bool isKeyFramed)
		{
			if (_rigidBodies.ContainsKey(id))
			{
				var rb = _rigidBodies[id];
				if (rb.Ownership)
				{
					rb.IsKeyframed = isKeyFramed;
				}
			}
		}

		#endregion

		#region Transform Streaming

		/// <summary>
		/// Add transform snapshot from specified source.
		/// </summary>
		/// <param name="sourceId">Snapshot source identifier.</param>
		/// <param name="snapshot">List of transform at specified timestamp.</param>
		public void addSnapshot(Guid sourceId, Snapshot snapshot)
		{
			_snapshotManager.addSnapshot(sourceId, snapshot);
		}

		#endregion

		public void FixedUpdate(UnityEngine.Transform rootTransform)
		{
			// physics rigid body management
			// set transforms/velocities for key framed bodies
			float DT = UnityEngine.Time.fixedDeltaTime;
			float halfDT = 0.5f * DT;
			float invDT = (1.0f / DT);
			const float startInterpolatingBack = 0.6f; // time in seconds
			const float endInterpolatingBack = 2.0f; // time in seconds
			const float invInterpolationTimeWindows = 1.0f / (endInterpolatingBack - startInterpolatingBack);
			const float limitCollisionInterpolation = 0.2f;
			const float collisionRelDistLimit = 0.8f;

			const float radiusExpansionFactor = 1.3f; // to detect potential collisions
			const float inCollisionRangeRelativeDistanceFactor = 1.25f;

			const float interpolationPosEpsilon = 0.01f;
			const float interpolationAngularEps = 3.0f;

#if MRE_PHYSICS_DEBUG
			Debug.Log(" ------------ ");
#endif
			// delete all the previous collisions
			_switchCollisionInfos.Clear();

			int index = 0;
			MultiSourceCombinedSnapshot snapshot = _snapshotManager.GetNextSnapshot(DT);

			foreach (var rb in _rigidBodies.Values)
			{
				if (rb.Ownership)
				{
					if (rb.IsKeyframed)
					{
						rb.RigidBody.isKinematic = true;
					}
					else
					{
						rb.RigidBody.isKinematic = false;
					}

					continue;
				}


				// Find corresponding rigid body info.
				while (index < snapshot.RigidBodies.Count && rb.Id.CompareTo(snapshot.RigidBodies.Values[index].Id) > 0) index++;

				if (index < snapshot.RigidBodies.Count && rb.Id == snapshot.RigidBodies.Values[index].Id)
				{
					if (!snapshot.RigidBodies.Values[index].HasUpdate)
					{
						rb.RigidBody.isKinematic = false;
						continue;
					}

					RigidBodyTransform transform = snapshot.RigidBodies.Values[index].Transform;
					float timeOfSnapshot = snapshot.RigidBodies.Values[index].LocalTime;

					var collisionInfo = new CollisionSwitchInfo();

					collisionInfo.startPosition = rb.RigidBody.transform.position;
					collisionInfo.startOrientation = rb.RigidBody.transform.rotation;
					collisionInfo.rigidBodyId = rb.Id;

					// get the key framed stream, and compute implicit velocities
					UnityEngine.Vector3 keyFramedPos = rootTransform.TransformPoint(transform.Position);
					UnityEngine.Quaternion keyFramedOrientation = rootTransform.rotation * transform.Rotation;
					// if there is a really new update then also store the implicit velocity
					if (rb.lastTimeKeyFramedUpdate < timeOfSnapshot)
					{
						// <todo> for long running times this could be a problem 
						float invUpdateDT = 1.0f / (timeOfSnapshot - rb.lastTimeKeyFramedUpdate);
						rb.lastValidLinerVelocity = (keyFramedPos - collisionInfo.startPosition) * invUpdateDT;
						rb.lastValidAngularVelocity = (UnityEngine.Quaternion.Inverse(collisionInfo.startOrientation)
						 * keyFramedOrientation).eulerAngles * invUpdateDT;
					}
					rb.lastTimeKeyFramedUpdate = timeOfSnapshot;

					// test is this remote body is in the monitor stream
					if (_monitorCollisionInfo.ContainsKey(rb.Id))
					{
						// dynamic
						rb.RigidBody.isKinematic = false;
						collisionInfo.monitorInfo = _monitorCollisionInfo[rb.Id];
						collisionInfo.monitorInfo.timeFromStartCollision += DT;
						collisionInfo.linearVelocity = rb.RigidBody.velocity;
						collisionInfo.angularVelocity = rb.RigidBody.angularVelocity;
#if MRE_PHYSICS_DEBUG
						Debug.Log(" Remote body: " + rb.Id.ToString() + " is dynamic since:"
							+ collisionInfo.monitorInfo.timeFromStartCollision + " relative distance:" + collisionInfo.monitorInfo.relativeDistance
							+ " interpolation:" + collisionInfo.monitorInfo.keyframedInterpolationRatio);
#endif
						// if time passes by then make a transformation between key framed and dynamic
						// but only change the positions not the velocity
						if (collisionInfo.monitorInfo.keyframedInterpolationRatio > 0.05f)
						{
							// interpolate between key framed and dynamic transforms
							float t = collisionInfo.monitorInfo.keyframedInterpolationRatio;
							UnityEngine.Vector3 interpolatedPos;
							UnityEngine.Quaternion interpolatedQuad;
							interpolatedPos = t * keyFramedPos + (1.0f - t) * rb.RigidBody.transform.position;
							interpolatedQuad = UnityEngine.Quaternion.Slerp(keyFramedOrientation, rb.RigidBody.transform.rotation, t);
#if MRE_PHYSICS_DEBUG
							Debug.Log(" Interpolate body " + rb.Id.ToString() + " t=" + t
								+ " time=" + UnityEngine.Time.time
								+ " pos KF:" + keyFramedPos
								+ " dyn:" + rb.RigidBody.transform.position
							    + " interp pos:" + interpolatedPos
								+ " rb vel:" + rb.RigidBody.velocity
								+ " KF vel:" + rb.lastValidLinerVelocity);
#endif
							// apply these changes only if they are significant in order to not to bother the physics engine
							// for settled objects
							UnityEngine.Vector3 posdiff = rb.RigidBody.transform.position - interpolatedPos;
							if (posdiff.magnitude > interpolationPosEpsilon)
							{
								rb.RigidBody.transform.position = interpolatedPos;
							}
							float angleDiff = Math.Abs(
								UnityEngine.Quaternion.Angle(rb.RigidBody.transform.rotation, interpolatedQuad) );
							if (angleDiff > interpolationAngularEps)
							{
								rb.RigidBody.transform.rotation = interpolatedQuad;
							}
						}
					}
					else
					{
						// 100% key framing
						rb.RigidBody.isKinematic = true;
						rb.RigidBody.transform.position = keyFramedPos;
						rb.RigidBody.transform.rotation = keyFramedOrientation;
						rb.RigidBody.velocity.Set(0.0f, 0.0f, 0.0f);
						rb.RigidBody.angularVelocity.Set(0.0f, 0.0f, 0.0f);
						collisionInfo.linearVelocity = rb.lastValidLinerVelocity;
						collisionInfo.angularVelocity = rb.lastValidAngularVelocity;
						collisionInfo.monitorInfo = new CollisionMonitorInfo();
#if MRE_PHYSICS_DEBUG
						Debug.Log(" Remote body: " + rb.Id.ToString() + " is key framed:"
							+ " linvel:" + collisionInfo.linearVelocity
							+ " angvel:" + collisionInfo.angularVelocity);
#endif
					}
					// <todo> add more filtering here to cancel out unnecessary items,
					// but for small number of bodies should be OK
					_switchCollisionInfos.Add(collisionInfo);
				}
			}

			// clear here all the monitoring since we will re add them
			_monitorCollisionInfo.Clear();
#if MRE_PHYSICS_DEBUG
			Debug.Log(" ===================== ");
#endif

			// test collisions of each owned body with each not owned body
			if (false)
			foreach (var rb in _rigidBodies.Values)
			{
				if (rb.Ownership)
				{
					// test here all the remote-owned collisions and those should be turned to dynamic again.
					foreach (var remoteBodyInfo in _switchCollisionInfos)
					{
						var remoteBody = _rigidBodies[remoteBodyInfo.rigidBodyId].RigidBody;
						var comDist = (remoteBody.transform.position - rb.RigidBody.transform.position).magnitude;

						var remoteHitPoint = remoteBody.ClosestPointOnBounds(rb.RigidBody.transform.position);
						var ownedHitPoint = rb.RigidBody.ClosestPointOnBounds(remoteBody.transform.position);

						var remoteRelativeHitP = (remoteHitPoint - remoteBody.transform.position);
						var ownedRelativeHitP = (ownedHitPoint - rb.RigidBody.transform.position);

						var radiousRemote = radiusExpansionFactor * remoteRelativeHitP.magnitude;
						var radiusOwnedBody = radiusExpansionFactor * ownedRelativeHitP.magnitude;

						var totalDistance = radiousRemote + radiusOwnedBody + 0.0001f; // avoid division by zero

						// project the linear velocity of the body
						var projectedOwnedBodyPos = rb.RigidBody.transform.position + rb.RigidBody.velocity * DT;
						var projectedComDist = (remoteBody.transform.position - projectedOwnedBodyPos).magnitude;

						var collisionMonitorInfo = remoteBodyInfo.monitorInfo;
						float lastApproxDistance = Math.Min(comDist, projectedComDist);
						collisionMonitorInfo.relativeDistance = lastApproxDistance / totalDistance;

						bool addToMonitor = false;
						bool isWithinCollisionRange = (projectedComDist < totalDistance || comDist < totalDistance);
						float timeSinceCollisionStart = collisionMonitorInfo.timeFromStartCollision;
						bool isAlreadyInMonitor = _monitorCollisionInfo.ContainsKey(remoteBodyInfo.rigidBodyId);
#if MRE_PHYSICS_DEBUG
						Debug.Log("prprojectedComDistoj: " + projectedComDist + " comDist:" + comDist
							+ " totalDistance:" + totalDistance + " remote body pos:" + remoteBodyInfo.startPosition.ToString()
							+ "input lin vel:" + remoteBodyInfo.linearVelocity + " radiousRemote:" + radiousRemote +
							" radiusOwnedBody:" + radiusOwnedBody + " relative dist:" + collisionMonitorInfo.relativeDistance
							+ " timeSinceCollisionStart:" + timeSinceCollisionStart + " isAlreadyInMonitor:" + isAlreadyInMonitor);
#endif

						// unconditionally add to the monitor stream if this is a reasonable collision and we are only at the beginning
						if (collisionMonitorInfo.timeFromStartCollision > halfDT &&
							timeSinceCollisionStart <= startInterpolatingBack &&
							collisionMonitorInfo.relativeDistance < inCollisionRangeRelativeDistanceFactor)
						{
#if MRE_PHYSICS_DEBUG
							Debug.Log(" unconditionally add to collision stream time:" + timeSinceCollisionStart +
								" relative dist:" + collisionMonitorInfo.relativeDistance);
#endif
							addToMonitor = true;
						}

						// switch over smoothly to the key framing
						if (!addToMonitor &&
							timeSinceCollisionStart > 5 * DT && // some basic check for lower limit
							//(!isAlreadyInMonitor || collisionMonitorInfo.relativeDistance > 1.2f) && // if this is already added then ignore large values
							timeSinceCollisionStart <= endInterpolatingBack)
						{
							addToMonitor = true;
							float oldVal = collisionMonitorInfo.keyframedInterpolationRatio;
							// we might enter here before startInterpolatingBack
							timeSinceCollisionStart = Math.Max(timeSinceCollisionStart, startInterpolatingBack + DT);
							// progress the interpolation
							collisionMonitorInfo.keyframedInterpolationRatio =
								invInterpolationTimeWindows * (timeSinceCollisionStart - startInterpolatingBack);
#if MRE_PHYSICS_DEBUG
							Debug.Log(" doing interpolation new:" + collisionMonitorInfo.keyframedInterpolationRatio +
								" old:" + oldVal + " relative distance:" + collisionMonitorInfo.relativeDistance);
#endif
							// stop interpolation
							if (collisionMonitorInfo.relativeDistance < collisionRelDistLimit
								&& collisionMonitorInfo.keyframedInterpolationRatio > limitCollisionInterpolation)
							{
#if MRE_PHYSICS_DEBUG
								Debug.Log(" Stop interpolation time with DT:" + DT +
									" Ratio:" + collisionMonitorInfo.keyframedInterpolationRatio +
									" relDist:" + collisionMonitorInfo.relativeDistance +
									" t=" + collisionMonitorInfo.timeFromStartCollision);
#endif
								// --- this is a different way to drop the percentage for key framing ---
								//collisionMonitorInfo.timeFromStartCollision -=
								//	Math.Min( 4.0f, ( (1.0f/limitCollisionInterpolation) * collisionMonitorInfo.keyframedInterpolationRatio) )* DT;

								// this version just drops the percentage back to the limit 
								collisionMonitorInfo.timeFromStartCollision = startInterpolatingBack
									+ limitCollisionInterpolation * (endInterpolatingBack - startInterpolatingBack);
								collisionMonitorInfo.keyframedInterpolationRatio = limitCollisionInterpolation;
							}
						}

						// we add to the collision stream either when it is within range or 
						if (isWithinCollisionRange || addToMonitor)
						{
							// add to the monitor stream
							if (isAlreadyInMonitor)
							{
								// if there is existent collision already with this remote body, then build the minimum
								var existingMonitorInfo = _monitorCollisionInfo[remoteBodyInfo.rigidBodyId];
#if MRE_PHYSICS_DEBUG
								Debug.Log(" START merge collision info: " + remoteBodyInfo.rigidBodyId.ToString() +
									" r:" + existingMonitorInfo.keyframedInterpolationRatio + " d:" + existingMonitorInfo.relativeDistance);
#endif
								existingMonitorInfo.timeFromStartCollision =
									Math.Min(existingMonitorInfo.timeFromStartCollision, collisionMonitorInfo.timeFromStartCollision);
								existingMonitorInfo.relativeDistance =
									Math.Min(existingMonitorInfo.relativeDistance, collisionMonitorInfo.relativeDistance);
								existingMonitorInfo.keyframedInterpolationRatio =
									Math.Min(existingMonitorInfo.keyframedInterpolationRatio, collisionMonitorInfo.keyframedInterpolationRatio);
								collisionMonitorInfo = existingMonitorInfo;
#if MRE_PHYSICS_DEBUG
								// <todo> check why is this working sometimes weired.
								_monitorCollisionInfo[remoteBodyInfo.rigidBodyId] = collisionMonitorInfo;
								existingMonitorInfo = _monitorCollisionInfo[remoteBodyInfo.rigidBodyId];
								Debug.Log(" merge collision info: " + remoteBodyInfo.rigidBodyId.ToString() +
									" r:" + existingMonitorInfo.keyframedInterpolationRatio + " d:" + existingMonitorInfo.relativeDistance);
#endif
							}
							else
							{
								_monitorCollisionInfo.Add(remoteBodyInfo.rigidBodyId, collisionMonitorInfo);
#if MRE_PHYSICS_DEBUG
								var existingMonitorInfo = _monitorCollisionInfo[remoteBodyInfo.rigidBodyId];
								Debug.Log(" Add collision info: " + remoteBodyInfo.rigidBodyId.ToString() +
									" r:" + existingMonitorInfo.keyframedInterpolationRatio + " d:" + existingMonitorInfo.relativeDistance);
#endif
							}

							// this is a new collision
							if (collisionMonitorInfo.timeFromStartCollision < halfDT)
							{
								// switch back to dynamic
								remoteBody.isKinematic = false;
								remoteBody.transform.position = remoteBodyInfo.startPosition;
								remoteBody.transform.rotation = remoteBodyInfo.startOrientation;
								remoteBody.velocity = remoteBodyInfo.linearVelocity;
								remoteBody.angularVelocity = remoteBodyInfo.angularVelocity;
#if MRE_PHYSICS_DEBUG
								Debug.Log(" remote body velocity SWITCH to collision: " + remoteBody.velocity.ToString()
	                               + "  start position:" + remoteBody.transform.position.ToString()
								   + " linVel:" + remoteBody.velocity + " angVel:" + remoteBody.angularVelocity);
#endif
							}
							else
							{
#if MRE_PHYSICS_DEBUG
								// this is a previous collision so do nothing just leave dynamic
								Debug.Log(" remote body velocity stay collision: " + remoteBody.velocity.ToString() +
									"  start position:" + remoteBody.transform.position.ToString() +
									" relative dist:" + collisionMonitorInfo.relativeDistance +
									" Ratio:" + collisionMonitorInfo.keyframedInterpolationRatio );
#endif
							}
						}
					}
				}
			}

		}

		/// <summary>
		/// Generate rigid body transform snapshot for owned transforms with specified timestamp.
		/// </summary>
		/// <param name="time">Snapshot timestamp.</param>
		/// <param name="rootTransform">Root transform.</param>
		/// <returns>Generated snapshot.</returns>
		public Snapshot GenerateSnapshot(float time, UnityEngine.Transform rootTransform)
		{
			// collect transforms from owned rigid bodies
			// and generate update packet/snapshot

			List<Snapshot.TransformInfo> transforms = new List<Snapshot.TransformInfo>(_rigidBodies.Count);

			foreach (var rb in _rigidBodies.Values)
			{
				if (!rb.Ownership)
				{
					continue;
				}

				RigidBodyTransform transform;
				{
					transform.Position = rootTransform.InverseTransformPoint(rb.RigidBody.transform.position);
					transform.Rotation = UnityEngine.Quaternion.Inverse(rootTransform.rotation) * rb.RigidBody.transform.rotation;
				}

				transforms.Add(new Snapshot.TransformInfo(rb.Id, transform));
			}

			return new Snapshot(time, transforms);
		}

		public void LateUpdate()
		{
			// smooth transform update to hide artifacts for rendering
		}
	}
}
