using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Unityroom.Client
{
    internal static class UnityWebRequestExtensions
    {
        public static async Task SendAsync(this UnityWebRequest webRequest, CancellationToken cancellationToken)
        {
            CancellationTokenRegistration cancellationTokenRegistration = default;
            if (cancellationToken.CanBeCanceled)
            {
                cancellationTokenRegistration = cancellationToken.Register(static state =>
                {
                    var req = (UnityWebRequest)state;
                    req.Abort();
                    req.Dispose();
                }, webRequest);
            }

            using (cancellationTokenRegistration)
            {
                await new AwaitableAsyncOperation(webRequest.SendWebRequest());
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}