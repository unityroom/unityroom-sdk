using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unityroom.Client
{
    internal static class TaskEx
    {
        // TODO: 効率的な待機処理の追加

        public static async Task DelayOnMainThread(TimeSpan timeSpan, CancellationToken cancellationToken = default)
        {
            var startTime = Time.realtimeSinceStartupAsDouble;
            while (TimeSpan.FromSeconds(Time.realtimeSinceStartupAsDouble - startTime) < timeSpan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }

        public static async Task CancelAfterOnMainThread(this CancellationTokenSource cts, TimeSpan delay)
        {
            try
            {
                await DelayOnMainThread(delay, cts.Token);
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