﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using MixedRealityExtension.Core;
using MixedRealityExtension.Patching.Types;
using UnityEngine;

namespace MixedRealityExtension.Animation
{
	internal class NativeAnimation : Animation
	{
		private UnityEngine.Animation nativeAnimation;
		private AnimationState nativeState;

		public override string Name
		{
			get => nativeState.name;
			protected set { nativeState.name = value; }
		}

		public override long BasisTime
		{
			get
			{
				if (IsPlaying && Speed != 0)
					return manager.ServerNow() - (long)Mathf.Floor(Time * 1000 / Speed);
				else
					return 0;
			}
			protected set
			{
				Time = (manager.ServerNow() - value) * Speed / 1000.0f;
			}
		}

		public override float Time
		{
			get => nativeState.time;
			protected set
			{
				nativeState.time = value;
			}
		}

		public override float Speed
		{
			get => nativeState.speed;
			protected set
			{
				nativeState.speed = value;
			}
		}

		public override float Weight
		{
			get => nativeState.weight;
			protected set
			{
				nativeState.weight = value;
				nativeState.enabled = IsPlaying;
			}
		}

		public override MWAnimationWrapMode WrapMode
		{
			get
			{
				switch (nativeState.wrapMode)
				{
					case UnityEngine.WrapMode.Loop:
						return MWAnimationWrapMode.Loop;
					case UnityEngine.WrapMode.PingPong:
						return MWAnimationWrapMode.PingPong;
					default:
						return MWAnimationWrapMode.Once;
				}
			}
			protected set
			{
				switch (value)
				{
					case MWAnimationWrapMode.Loop:
						nativeState.wrapMode = UnityEngine.WrapMode.Loop;
						break;
					case MWAnimationWrapMode.PingPong:
						nativeState.wrapMode = UnityEngine.WrapMode.PingPong;
						break;
					default:
						nativeState.wrapMode = UnityEngine.WrapMode.Once;
						break;
				}
			}
		}

		internal NativeAnimation(AnimationManager manager, Guid id, UnityEngine.Animation nativeAnimation, AnimationState nativeState) : base(manager, id)
		{
			this.nativeAnimation = nativeAnimation;
			this.nativeState = nativeState;
		}

		public override AnimationPatch GeneratePatch()
		{
			var patch = base.GeneratePatch();
			patch.Duration = nativeState.length;
			return patch;
		}

		internal override void Update()
		{

		}
	}
}
