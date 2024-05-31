using System;
using UnityEngine;

namespace Unityroom.Client
{
    [Serializable]
    internal record UnityroomApiErrorResponse
    {
        public string Code => code;
        public string Type => type;
        public string Message => message;

        [SerializeField] string code;
        [SerializeField] string type;
        [SerializeField] string message;
    }
}