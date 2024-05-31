using System;
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

    public sealed class UnityroomClient : IUnityroomClient, IScoreboards
    {
        public string HmacKey { get; set; }

        public IScoreboards Scoreboards => this;

        async Task<SendScoreResponse> IScoreboards.SendAsync(SendScoreRequest request, CancellationToken cancellationToken)
        {
            using var webRequest = CreateScoreRequest(request.ScoreboardId, HmacKey, request.Score);
            await webRequest.SendAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(webRequest.error))
            {
                var errorResponse = JsonUtility.FromJson<UnityroomApiErrorResponse>(webRequest.downloadHandler.text);
                
                if (errorResponse == null)
                {
                    throw new InvalidOperationException($"UnityWebRequest.SendWebRequest is failed, Result: {webRequest.result}");
                }

                throw new UnityroomApiException(int.Parse(errorResponse.Code), errorResponse.Type, errorResponse.Message);
            }

            return JsonUtility.FromJson<SendScoreResponse>(webRequest.downloadHandler.text);
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
    }
}