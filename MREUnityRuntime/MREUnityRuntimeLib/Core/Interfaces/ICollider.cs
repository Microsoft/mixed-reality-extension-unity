﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace MixedRealityExtension.Core.Interfaces
{
    // public enum CollisionLayer
    // {
    //     Object,
    //     Environment,
    //     Hologram
    // }

    /// <summary>
    /// The interface that represents a collider within the mixed reality extension runtime.
    /// </summary>
    public interface ICollider
    {
        /// <summary>
        /// Whether the collider is enabled.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Whether the collider is a trigger.
        /// </summary>
        bool IsTrigger { get; }

        //CollisionLayer CollisionLayer { get; }

        /// <summary>
        /// The type of the collider.  <see cref="ColliderType"/>
        /// </summary>
        ColliderType ColliderType { get; }
    }
}
