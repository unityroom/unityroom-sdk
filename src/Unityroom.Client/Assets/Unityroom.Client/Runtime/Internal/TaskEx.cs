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

        public static async Task CancelAfterOnPlayerLoop(this CancellationTokenSource cts, TimeSpan delay)
        {
            try
            {
                await DelayOnPlayerLoop(delay, cts.Token);
                cts.Cancel();
            }
            catch (OperationCanceledException)
            {

            }
        }

        public static void Forget(this Task task)
        {
            task.ContinueWith(x =>
            {
                Debug.LogException(x.Exception);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}