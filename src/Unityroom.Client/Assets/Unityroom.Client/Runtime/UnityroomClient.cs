using System;
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
        public string HmacKey { get; set; }
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);
        public int MaxRetries { get; set; } = 2;

        readonly CancellationTokenSource clientLifetimeTokenSource = new();

        public IScoreboards Scoreboards => this;

        async Task<SendScoreResponse> IScoreboards.SendAsync(SendScoreRequest request, CancellationToken cancellationToken)
        {
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
                            await TaskEx.DelayOnMainThread(TimeSpan.FromSeconds(2.0), cts.Token);
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
                cts.Cancel();
                cts.Dispose();
                webRequest.Dispose();
            }
        }

        UnityWebRequest CreateScoreRequest(int scoreboardId, string hmacKey, float value)
        {
            var builder = new ValueStringBuilder();

            builder.Append("/gameplay_api/v1/scoreboards/");
            builder.Append(scoreboardId);
            builder.Append("/scores");
            var path = builder.ToString();

            var unixTime = GetCurrentUnixTime().ToString();

            var scoreText = value.ToString(CultureInfo.InvariantCulture);

            builder = new();
            builder.Append("POST\n");
            builder.Append(path);
            builder.Append("\n");
            builder.Append(unixTime);
            builder.Append("\n");
            builder.Append(scoreText);
            var hmacDataText = builder.ToString();

            var hmac = GetHmacSha256(hmacDataText, hmacKey);

            var form = new WWWForm();
            form.AddField("score", scoreText);
            var request = UnityWebRequest.Post(path, form);
            request.SetRequestHeader("X-Unityroom-Signature", hmac);
            request.SetRequestHeader("X-Unityroom-Timestamp", unixTime);

            return request;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static string GetHmacSha256(string dataText, string base64AuthenticationKey)
        {
            // TODO: アロケーションの削減
            var dataBytes = Encoding.UTF8.GetBytes(dataText);
            var keyBytes = Convert.FromBase64String(base64AuthenticationKey);
            var sha256 = new HMACSHA256(keyBytes);
            var hmacBytes = sha256.ComputeHash(dataBytes);
            var hmacText = BitConverter.ToString(hmacBytes)
                .ToLower()
                .Replace("-", "");
            return hmacText;
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