﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Experimental
{
	public struct RBTransform
	{
		public Vector3 Position;

		public Quaternion Rotation;

		public void Lerp(RBTransform t0, RBTransform t1, float f)
		{
			Position = Vector3.Lerp(t0.Position, t1.Position, f);
			Rotation = Quaternion.Lerp(t0.Rotation, t1.Rotation, f);
		}
	}

	public struct RBInfo
	{
		public Guid ID;

		public RBTransform Transform;
	}

	public class Snapshot
	{
		public float Time;

		public List<RBInfo> RigidBodies;
	}

	public class SnapshotBuffer
	{
		public Snapshot PreviousSnapshot { get; private set; }
		public Snapshot CurrentSnapshot { get; private set; }

		public float CurentTime { get; set; }

		public float FullySupportedTime { get; private set; }

		public float LastSnapshotTime { get; private set; }

		private List<Snapshot> Snapshots = new List<Snapshot>();

		public void AddSnapshot(Snapshot snapshot)
		{
			int insertIndex = 0;

			while (insertIndex < Snapshots.Count)
			{
				if (snapshot.Time < Snapshots[insertIndex].Time)
				{
					break;
				}
				else
				{
					insertIndex++;
				}
			}

			LastSnapshotTime = LastSnapshotTime > snapshot.Time ? LastSnapshotTime : snapshot.Time;

			Snapshots.Insert(insertIndex, snapshot);
		}

		private float _playFactor = 1.0f;

		private float _runningAverageOffset = 0.0f;

		const int _runningAverageFrames = 120;

		float TotalTime = 0.0f;

		float dt = 0.0f;

		public void startFrame(float timestep)
		{
			Debug.Log("start frame " + CurentTime + " delta " + timestep);

			float offset = LastSnapshotTime - (CurentTime + timestep);
			_runningAverageOffset -= _runningAverageOffset / _runningAverageFrames;
			_runningAverageOffset += offset / _runningAverageFrames;

			if (_runningAverageOffset < 0.01f)
			{
				_playFactor = 0.8f;
			}
			else if (_runningAverageOffset > 0.1f)
			{
				_playFactor = 1.1f;
			}
			else
			{
				_playFactor = 1.0f;
			}
		}

		public void endFrame()
		{

		}

		public void step(float deltaTime)
		{
			CurentTime += deltaTime * _playFactor;

			Debug.Log("step " + CurentTime);

			if (CurentTime > LastSnapshotTime)
			{
				return;
			}

			int prevIndex = -1;
			int nextIndex = -1;

			int i = 0;
			for (; i < Snapshots.Count; i++)
			{
				if (Snapshots[i].Time >= CurentTime)
				{
					nextIndex = i;
					break;
				}
				else
				{
					prevIndex = i;
				}
			}

			if (prevIndex >= 0)
			{
				PreviousSnapshot = Snapshots[prevIndex];
			}

			if (nextIndex >= 0)
			{
				CurrentSnapshot = Snapshots[nextIndex];
			}

			if (prevIndex >= 1)
			{
				Snapshots.RemoveRange(0, prevIndex);
			}
		}
	}

	class JitterBuffer
	{
		//float Time;

		public SnapshotBuffer SnapshotBuffer = new SnapshotBuffer();

		public SortedList<Guid, RBInfo> RigidBodies = new SortedList<Guid, RBInfo>();

		public void addSnapshot(Snapshot snapshot)
		{
			SnapshotBuffer.AddSnapshot(snapshot);
		}

		public void step(float timestep)
		{
			// process snapshots
			SnapshotBuffer.step(timestep);

			// put value in interpolation buffer
			RigidBodies.Clear();
			RigidBodies.Capacity = SnapshotBuffer.CurrentSnapshot.RigidBodies.Capacity;

			float frac = (this.SnapshotBuffer.CurrentSnapshot.Time - this.SnapshotBuffer.PreviousSnapshot.Time) / (SnapshotBuffer.CurentTime - this.SnapshotBuffer.PreviousSnapshot.Time);

			int prevIndex = 0;
			int nextIndex = 0;

			for (; nextIndex < SnapshotBuffer.CurrentSnapshot.RigidBodies.Count; nextIndex++)
			{
				RBInfo rb = SnapshotBuffer.CurrentSnapshot.RigidBodies[nextIndex];

				while (prevIndex < SnapshotBuffer.PreviousSnapshot.RigidBodies.Count)
				{
					if (SnapshotBuffer.PreviousSnapshot.RigidBodies[prevIndex].ID.CompareTo(SnapshotBuffer.CurrentSnapshot.RigidBodies[nextIndex].ID) >= 0)
					{
						break;
					}
					else
					{
						prevIndex++;
					}
				}

				if (prevIndex < SnapshotBuffer.PreviousSnapshot.RigidBodies.Count &&
					SnapshotBuffer.PreviousSnapshot.RigidBodies[prevIndex].ID == SnapshotBuffer.CurrentSnapshot.RigidBodies[nextIndex].ID)
				{
					RBInfo rbi = new RBInfo();
					rbi.ID = SnapshotBuffer.PreviousSnapshot.RigidBodies[prevIndex].ID;
					rbi.Transform.Lerp(SnapshotBuffer.PreviousSnapshot.RigidBodies[prevIndex].Transform, SnapshotBuffer.CurrentSnapshot.RigidBodies[nextIndex].Transform, frac);

					RigidBodies.Add(rbi.ID, rbi);
				}
			}
		}
	}

	class MultiSourceJitterBuffer
	{
		Dictionary<Guid, JitterBuffer> Sources = new Dictionary<Guid, JitterBuffer>();

		public SortedList<Guid, RBInfo> RigidBodies = new SortedList<Guid, RBInfo>();

		public Experimental.RBTransform? getTransform(Guid rbId)
		{
			foreach (var s in RigidBodies.Values)
			{
					if (s.ID == rbId)
					{
						return s.Transform;
					}
			}

			return null;
		}

		public void startFrame(float timestep)
		{
			foreach (var source in Sources)
			{
				source.Value.SnapshotBuffer.startFrame(timestep);
			}
		}

		public void endFrame()
		{ }

		public void addSnapshot(Guid sourceId, Experimental.Snapshot snapshot)
		{
			if (!Sources.ContainsKey(sourceId))
			{
				var jb = new JitterBuffer();
				jb.SnapshotBuffer = new SnapshotBuffer();
				jb.SnapshotBuffer.CurentTime = snapshot.Time;
				Sources.Add(sourceId, jb);
			}

			foreach (var source in Sources)
			{
				if (source.Key == sourceId)
				{
					source.Value.addSnapshot(snapshot);
					break;
				}
			}
		}

		public void step(float deltaTime)
		{
			RigidBodies.Clear();

			foreach (var source in Sources.Values)
			{
				source.step(deltaTime);

				foreach (var b in source.RigidBodies)
				{
					RigidBodies.Add(b.Key, b.Value);
				}
			}
		}
	}
}

namespace MixedRealityExtension.Core
{
	public class PhysicsBridge
	{
		Experimental.MultiSourceJitterBuffer MSJB = new Experimental.MultiSourceJitterBuffer();

		public class SnapshotBuffer
		{
			/// <summary>
			/// this stores the snapshots received from all other clients, each client sends the owned updates of the actors (RBs)
			/// </summary>
			/// If one wants an updated for a remote body then needs to go though all these separate updates and one of the updates from one client we should find the actor ID
			Dictionary<Guid, Experimental.Snapshot> _snapshots = new Dictionary<Guid, Experimental.Snapshot>();

			public void addSnapshot(Guid sourceId, Experimental.Snapshot snapshot)
			{
				// currently keep only the latest snapshot from same source
				if (_snapshots.ContainsKey(sourceId))
				{
					var oldSnapshot = _snapshots[sourceId];

					if (snapshot.Time > oldSnapshot.Time)
					{
						_snapshots[sourceId] = snapshot;
					}
				}
				else
				{
					_snapshots.Add(sourceId, snapshot);
				}
			}

			public Experimental.RBTransform? getTransform(Guid rbId, out float timeOfSnapshot)
			{
				timeOfSnapshot = -float.MaxValue;
				foreach (var s in _snapshots.Values)
				{
					foreach (var t in s.RigidBodies)
					{
						if (t.ID == rbId)
						{
							timeOfSnapshot = s.Time;
							return t.Transform;
						}
					}
				}
				return null;
			}
		}

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
			}

			public Guid Id;

			public UnityEngine.Rigidbody RigidBody;

			/// these 3 fields are used to store the actual velocities
			public float lastTimeKeyFramedUpdate;
			public UnityEngine.Vector3 lastValidLinerVelocity;
			public UnityEngine.Vector3 lastValidAngularVelocity;

			/// <summary> true if this rigid body is owned by this client </summary>
			public bool Ownership; 
		}

		private int _countOwnedTransforms = 0;
		private int _countStreamedTransforms = 0;

		private Dictionary<Guid, RigidBodyInfo> _rigidBodies = new Dictionary<Guid, RigidBodyInfo>();

		private SnapshotBuffer _snapshotBuffer = new SnapshotBuffer();

		/// when we update a body we compute the implicit velocity. This velocity is needed, in case of collisions to switch from kinematic to kinematic=false
		List<CollisionSwitchInfo> _switchCollisionInfos = new List<CollisionSwitchInfo>();

		/// <todo> the input should be GuidXGuid since one body could collide with multiple other, or one collision is enough (?) </todo>
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
				_countOwnedTransforms++;
			}
			else
			{
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

		#endregion

		#region Transform Streaming

		public void addSnapshot(Guid sourceId, Experimental.Snapshot snapshot)
		{
			_snapshotBuffer.addSnapshot(sourceId, snapshot);

			MSJB.addSnapshot(sourceId, snapshot);
		}

		#endregion

		private bool _startFrame = true;

		public void FixedUpdate(UnityEngine.Transform rootTransform)
		{
			MSJB.startFrame(Time.fixedDeltaTime);
			MSJB.step(Time.fixedDeltaTime);

			// physics rigid body management
			// set transforms/velocities for key framed bodies
			float DT = UnityEngine.Time.fixedDeltaTime;
			float halfDT = 0.5f * DT;
			float invDT = (1.0f / DT);
			const float startInterpolatingBack = 0.6f; // time in seconds
			const float endInterpolatingBack = 2.0f; // time in seconds
			const float invInterpolationTimeWindows = 1.0f / (endInterpolatingBack - startInterpolatingBack);

			// delete all the previous collisions
			_switchCollisionInfos.Clear();

			foreach (var rb in _rigidBodies.Values)
			{
				if (rb.Ownership)
				{
					continue;
				}
				float timeOfSnapshot;

				Experimental.RBTransform? nt = _snapshotBuffer.getTransform(rb.Id, out timeOfSnapshot);

				if (nt != null)
				{
					var transform = nt.Value;

					var collisionInfo = new CollisionSwitchInfo();

					collisionInfo.startPosition = rb.RigidBody.transform.position;
					collisionInfo.startOrientation = rb.RigidBody.transform.rotation;
					collisionInfo.rigidBodyId = rb.Id;

					if (_monitorCollisionInfo.ContainsKey(rb.Id))
					{
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
						if (collisionInfo.monitorInfo.keyframedInterpolationRatio > 0.001f)
						{
							// interpolate between key framed and dynamic transforms
							float t = collisionInfo.monitorInfo.keyframedInterpolationRatio;
							UnityEngine.Vector3 keyFramedPos = rootTransform.TransformPoint(transform.Position), interpolatedPos;
							UnityEngine.Quaternion keyFramedOrientation = rootTransform.rotation * transform.Rotation, interpolatedQuad;
							interpolatedPos = t * keyFramedPos + (1.0f - t) * rb.RigidBody.transform.position;
							interpolatedQuad = UnityEngine.Quaternion.Slerp(keyFramedOrientation, rb.RigidBody.transform.rotation, t);
							rb.RigidBody.transform.position = interpolatedPos;
							rb.RigidBody.transform.rotation = interpolatedQuad;
						}
					}
					else
					{
						// no previous collision, keep key framed
						UnityEngine.Vector3 newPosition = rootTransform.TransformPoint(transform.Position);
						UnityEngine.Quaternion newOrientation = rootTransform.rotation * transform.Rotation;
						rb.RigidBody.isKinematic = true;
						// if there is a really new update then also store the implicit velocity
						if (rb.lastTimeKeyFramedUpdate < timeOfSnapshot)
						{
							// <todo> for long running times this could be a problem 
							float invUpdateDT = 1.0f/(timeOfSnapshot - rb.lastTimeKeyFramedUpdate);
							rb.lastValidLinerVelocity = (newPosition - collisionInfo.startPosition) * invUpdateDT;
							rb.lastValidAngularVelocity = (UnityEngine.Quaternion.Inverse(collisionInfo.startOrientation)
							 * newOrientation).eulerAngles * invUpdateDT;
						}
						rb.RigidBody.transform.position = newPosition;
						rb.RigidBody.transform.rotation = newOrientation;
						rb.lastTimeKeyFramedUpdate = timeOfSnapshot;

						collisionInfo.linearVelocity = rb.lastValidLinerVelocity;
						collisionInfo.angularVelocity = rb.lastValidAngularVelocity;
						collisionInfo.monitorInfo = new CollisionMonitorInfo();
#if MRE_PHYSICS_DEBUG
						Debug.Log(" Remote body: " + rb.Id.ToString() + " is key framed:");
#endif
					}
					// <todo> add more filtering here to cancel out unnecessary items,
					// but for small number of bodies should be OK
					_switchCollisionInfos.Add(collisionInfo);
				}
			}

			// clear here all the monitoring since we will re add them
			_monitorCollisionInfo.Clear();

			// test collisions of each owned body with each not owned body
			foreach (var rb in _rigidBodies.Values)
			{
				if (rb.Ownership)
				{
					// <todo> test here all the remote-owned collisions and those should be turned to dynamic again.
					foreach (var remoteBodyInfo in _switchCollisionInfos)
					{
						var remoteBody = _rigidBodies[remoteBodyInfo.rigidBodyId].RigidBody;
						var comDist = (remoteBody.transform.position - rb.RigidBody.transform.position).magnitude;

						var remoteHitPoint = remoteBody.ClosestPointOnBounds(rb.RigidBody.transform.position);
						var ownedHitPoint = rb.RigidBody.ClosestPointOnBounds(remoteBody.transform.position);

						var remoteRelativeHitP = (remoteHitPoint - remoteBody.transform.position);
						var ownedRelativeHitP = (ownedHitPoint - rb.RigidBody.transform.position);

						var radiousRemote = 1.3f * remoteRelativeHitP.magnitude;
						var radiusOwnedBody = 1.3f * ownedRelativeHitP.magnitude;

						var totalDistance = radiousRemote + radiusOwnedBody + 0.0001f; // avoid division by zero

						// project the linear velocity of the body
						var projectedOwnedBodyPos = rb.RigidBody.transform.position + rb.RigidBody.velocity * DT;
						var projectedComDist = (remoteBody.transform.position - projectedOwnedBodyPos).magnitude;

#if MRE_PHYSICS_DEBUG
						Debug.Log("prprojectedComDistoj: " + projectedComDist + " comDist:" + comDist
							+ " totalDistance:" + totalDistance + " remote body pos:" + remoteBodyInfo.startPosition.ToString()
							+ "input lin vel:" + remoteBodyInfo.linearVelocity + " radiousRemote:" + radiousRemote +
							" radiusOwnedBody:" + radiusOwnedBody);
#endif

						var collisionMonitorInfo = remoteBodyInfo.monitorInfo;
						float lastApproxDistance = Math.Min(comDist, projectedComDist);
						collisionMonitorInfo.relativeDistance = lastApproxDistance / totalDistance;

						bool addToMonitor = false;
						bool isWithinCollisionRange = (projectedComDist < totalDistance || comDist < totalDistance || addToMonitor);
						float timeSinceCollisionStart = collisionMonitorInfo.timeFromStartCollision;

						// unconditionally add to the monitor stream if this is a reasonable collision and we are only at the beginning
						if (collisionMonitorInfo.timeFromStartCollision > halfDT &&
							timeSinceCollisionStart <= startInterpolatingBack &&
							collisionMonitorInfo.relativeDistance < 1.2f)
						{
#if MRE_PHYSICS_DEBUG
							Debug.Log(" unconditionally add to collision stream time:" + timeSinceCollisionStart +
								" relative dist:" + collisionMonitorInfo.relativeDistance);
#endif
							addToMonitor = true;
						}
						// switch over smoothly to the key framing
						if (!addToMonitor && timeSinceCollisionStart > startInterpolatingBack && timeSinceCollisionStart <= endInterpolatingBack)
						{
							addToMonitor = true;
							float oldVal = collisionMonitorInfo.keyframedInterpolationRatio;
							// progress the interpolation
							collisionMonitorInfo.keyframedInterpolationRatio =
								invInterpolationTimeWindows * (timeSinceCollisionStart - startInterpolatingBack);
#if MRE_PHYSICS_DEBUG
							Debug.Log(" doing interpolation new:" + collisionMonitorInfo.keyframedInterpolationRatio +
								" old:" + oldVal);
#endif
							// stop interpolation
							if (collisionMonitorInfo.relativeDistance < 0.8f
								&& collisionMonitorInfo.keyframedInterpolationRatio > 0.2f)
							{
#if MRE_PHYSICS_DEBUG
								Debug.Log(" Stop interpolation time with DT:" + DT +
									" Ratio:" + collisionMonitorInfo.keyframedInterpolationRatio +
									" relDist:" + collisionMonitorInfo.relativeDistance);
#endif
								collisionMonitorInfo.timeFromStartCollision -= DT;
							}
						}

						// we add to the collision stream either when it is within range or 
						if (isWithinCollisionRange || addToMonitor)
						{
							// add to the monitor stream
							if (_monitorCollisionInfo.ContainsKey(remoteBodyInfo.rigidBodyId))
							{
								// if there is existent collision already with this remote body, then build the minimum
								var existingMonitorInfo = _monitorCollisionInfo[remoteBodyInfo.rigidBodyId];
								existingMonitorInfo.timeFromStartCollision =
									Math.Min(existingMonitorInfo.timeFromStartCollision, collisionMonitorInfo.timeFromStartCollision);
								existingMonitorInfo.relativeDistance =
									Math.Min(existingMonitorInfo.relativeDistance, collisionMonitorInfo.relativeDistance);
								existingMonitorInfo.keyframedInterpolationRatio =
									Math.Min(existingMonitorInfo.keyframedInterpolationRatio, collisionMonitorInfo.keyframedInterpolationRatio);
							}
							else
							{
								_monitorCollisionInfo.Add(remoteBodyInfo.rigidBodyId, collisionMonitorInfo);
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
								Debug.Log(" remote body velocity SWITCH collision: " + remoteBody.velocity.ToString() +
	                               "  start position:" + remoteBody.transform.position.ToString());
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

		public Experimental.Snapshot Update(UnityEngine.Transform rootTransform)
		{
			_startFrame = true;
			MSJB.endFrame();

			// collect transforms from owned rigid bodies
			// and generate update packet/snapshot

			Experimental.Snapshot snapshot = new Experimental.Snapshot();
			//snapshot.SourceId = _appId;
			snapshot.Time = UnityEngine.Time.fixedTime + UnityEngine.Time.fixedDeltaTime;
			snapshot.RigidBodies = new List<Experimental.RBInfo>();
			snapshot.RigidBodies.Capacity = _countOwnedTransforms;

			foreach (var rb in _rigidBodies.Values)
			{
				if (!rb.Ownership)
				{
					continue;
				}

				var ti = new Experimental.RBInfo();
				ti.ID = rb.Id;

				//Position = new Vector3Patch(App.SceneRoot.transform.InverseTransformPoint(transform.position)),
				//Rotation = new QuaternionPatch(Quaternion.Inverse(App.SceneRoot.transform.rotation) * transform.rotation)

				//ti.Transform.Position = rb.RigidBody.transform.position - rootTransform.position;
				//ti.Transform.Rotation = UnityEngine.Quaternion.Inverse(rootTransform.rotation) * rb.RigidBody.transform.rotation;

				ti.Transform.Position = rootTransform.InverseTransformPoint(rb.RigidBody.transform.position);
				ti.Transform.Rotation = UnityEngine.Quaternion.Inverse(rootTransform.rotation) * rb.RigidBody.transform.rotation;

				snapshot.RigidBodies.Add(ti);
			}

			return snapshot;
		}

		public void LateUpdate()
		{
			// smooth transform update to hide artifacts for rendering
		}
	}
}
