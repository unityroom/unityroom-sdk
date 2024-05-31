using System;

namespace Unityroom.Client
{
    public sealed class UnityroomApiException : Exception
    {
        public UnityroomApiException(int code, string type, string message) : base(message)
        {
            ErrorCode = code;
            ErrorType = type;
        }

        public int ErrorCode { get; }
        public string ErrorType { get; }
    }
}