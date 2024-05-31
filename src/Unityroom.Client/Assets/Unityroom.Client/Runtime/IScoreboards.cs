using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unityroom.Client
{
    public interface IScoreboards
    {
        Task<SendScoreResponse> SendAsync(SendScoreRequest request, CancellationToken cancellationToken = default);
    }

    [Serializable]
    public record SendScoreRequest
    {
        public int ScoreboardId { get; set; }
        public float Score { get; set; }
    }

    [Serializable]
    public record SendScoreResponse
    {
        public string Status
        {
            get => status;
            set => status = value;
        }

        public bool ScoreUpdated
        {
            get => saved;
            set => saved = value;
        }

        [SerializeField] string status;
        [SerializeField] bool saved;
    }
}