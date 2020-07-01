// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using MixedRealityExtension.Animation;
using MixedRealityExtension.Core;
using MixedRealityExtension.Core.Types;
using MixedRealityExtension.Messaging.Payloads.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace MixedRealityExtension.Patching.Types
{
	public class UserPatch : Patchable<UserPatch>
	{
		public Guid Id { get; set; }

		[PatchProperty]
		public string Name { get; set; }

		[PatchProperty]
		[JsonConverter(typeof(UnsignedConverter))]
		public UInt32? Groups { get; set; }

		public Dictionary<string, string> Properties { get; set; }

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
			Groups = user.Groups;
			Properties = user.UserInfo.Properties;
		}
	}
}
