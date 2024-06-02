using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unityroom.Client
{
    internal static class TaskEx
    {
        public static async Task DelayOnPlayerLoop(TimeSpan timeSpan, CancellationToken cancellationToken = default)
        {
            var startTime = Time.realtimeSinceStartupAsDouble;
            while (TimeSpan.FromSeconds(Time.realtimeSinceStartupAsDouble - startTime) < timeSpan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }
    }
}