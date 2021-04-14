// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

using MixedRealityExtension.Core;
namespace MixedRealityExtension.PluginInterfaces
{
	public interface IEDTiumBrowser
	{
		void Navigate(string uri);
		void Destroy();
	}
}
