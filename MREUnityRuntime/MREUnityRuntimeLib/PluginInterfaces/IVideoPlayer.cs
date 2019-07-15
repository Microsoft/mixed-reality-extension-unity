﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

using MixedRealityExtension.Core;
namespace MixedRealityExtension.PluginInterfaces
{
    public interface IVideoPlayer
    {
        void Play(VideoStreamDescription description, MediaStateOptions options, float? startTimeOffset);
        void Destroy();
        void ApplyMediaStateOptions(MediaStateOptions options);
    }
}
