// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using MixedRealityExtension.Core;
using MixedRealityExtension.Core.Types;
using System;

namespace MixedRealityExtension.Patching.Types
{
    public class UserPatch : IPatchable
    {
        public Guid Id { get; set; }

        [PatchProperty]
        public string Name { get; set; }

        public UserPatch()
        {
        }

        internal UserPatch(Guid id)
        {
            Id = id;
        }

        internal UserPatch(User user)
            : this(user.Id)
        {
            Name = user.Name;
        }

        public bool IsPatched()
        {
            return false;
        }
    }
}
