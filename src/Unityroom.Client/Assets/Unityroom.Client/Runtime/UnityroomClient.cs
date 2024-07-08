using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Unityroom.Client
{
    public interface IUnityroomClient
    {
        IScoreboards Scoreboards { get; }
    }

    public sealed class UnityroomClient : IDisposable, IUnityroomClient, IScoreboards
    {
        public string HmacKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => hmacKey;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                hmacKey = value;
                sha256 = new HMACSHA256(Convert.FromBase64String(hmacKey));
            }
        }

        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);
        public int MaxRetries { get; set; } = 2;

        readonly CancellationTokenSource clientLifetimeTokenSource = new();

        string hmacKey;
        HMACSHA256 sha256;

        int requestCount;
        const int MaxRequestCount = 3;

        static readonly Dictionary<string, string> formFields = new();

        public IScoreboards Scoreboards => this;

        async Task<SendScoreResponse> IScoreboards.SendAsync(SendScoreRequest request, CancellationToken cancellationToken)
        {
            if (requestCount > MaxRequestCount)
            {
                throw new InvalidOperationException($"The number of concurrent executions exceeded the limit ({MaxRequestCount}).");
            }

            requestCount++;

            var retryCount = MaxRetries;

        RETRY:
            var cts = CancellationTokenSource.CreateLinkedTokenSource(clientLifetimeTokenSource.Token, cancellationToken);
            cts.CancelAfterOnMainThread(Timeout).Forget();

            var webRequest = CreateScoreRequest(request.ScoreboardId, HmacKey, request.Score);

            try
            {
                await webRequest.SendAsync(cts.Token);

                if (!string.IsNullOrWhiteSpace(webRequest.error))
                {
                    var errorResponse = JsonUtility.FromJson<UnityroomApiErrorResponse>(webRequest.downloadHandler.text);

                    if (errorResponse == null)
                    {
                        throw new InvalidOperationException($"UnityWebRequest.SendWebRequest is failed, Result: {webRequest.result}");
                    }

                    if (errorResponse.Type == "rate_limit_exceeded")
                    {
                        if (retryCount > 0)
                        {
                            retryCount--;
                            // unityroom側のrate_limitの仕様に合わせ、5秒間の待機を行う
                            await TaskEx.DelayOnMainThread(TimeSpan.FromSeconds(5.0), cts.Token);
                            goto RETRY;
                        }
                    }

                    throw new UnityroomApiException(int.Parse(errorResponse.Code), errorResponse.Type, errorResponse.Message);
                }

                return JsonUtility.FromJson<SendScoreResponse>(webRequest.downloadHandler.text);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cts.Token)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    // 引数のCancellationTokenがキャンセルされた場合
                    throw new OperationCanceledException(ex.Message, ex, cancellationToken);
                }
                else if (clientLifetimeTokenSource.IsCancellationRequested)
                {
                    // ClientがDisposeされた場合
                    throw new OperationCanceledException("UnityroomClient is disposed.", ex, clientLifetimeTokenSource.Token);
                }
                else
                {
                    // タイムアウト時
                    throw new TimeoutException($"The request was canceled due to the configured Timeout of {Timeout.TotalSeconds} seconds elapsing.", ex);
                }
            }
            finally
            {
                requestCount--;

                cts.Cancel();
                cts.Dispose();
                webRequest.Dispose();
            }
        }

        UnityWebRequest CreateScoreRequest(int scoreboardId, string hmacKey, float value)
        {
            var builder = new ValueStringBuilder();

            builder.Append("/gameplay_api/v1/scoreboards/");
            builder.Append(ConvertEx.ToString(scoreboardId));
            builder.Append("/scores");
            var path = builder.ToString();

            var unixTime = GetCurrentUnixTime().ToString();

            var scoreText = value.ToString(CultureInfo.InvariantCulture);

            builder = new();
            builder.Append("POST\n");
            builder.Append(path);
            builder.Append('\n');
            builder.Append(unixTime);
            builder.Append('\n');
            builder.Append(scoreText);
            var hmacDataText = builder.ToString();

            var hmac = GetHmacSha256(hmacDataText);

            formFields.Clear();
            formFields.Add("score", scoreText);
            var request = UnityWebRequest.Post(path, formFields);
            request.SetRequestHeader("X-Unityroom-Signature", hmac);
            request.SetRequestHeader("X-Unityroom-Timestamp", unixTime);

            return request;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        string GetHmacSha256(string dataText)
        {
            if (sha256 == null) return null;

            var buffer = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(dataText.Length));
            try
            {
                var bytesWritten = Encoding.UTF8.GetBytes(dataText, buffer);
                Span<byte> hash = stackalloc byte[32];
                sha256.TryComputeHash(buffer.AsSpan(0, bytesWritten), hash, out _);

                return ConvertEx.ToHexString(hash, HexConverter.Casing.Lower);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int GetCurrentUnixTime()
        {
            return (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        public void Dispose()
        {
            clientLifetimeTokenSource.Cancel();
            clientLifetimeTokenSource.Dispose();
        }
    }
}