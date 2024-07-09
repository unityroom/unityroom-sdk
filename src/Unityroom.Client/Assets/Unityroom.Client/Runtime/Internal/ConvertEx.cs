using System;
using System.Runtime.CompilerServices;

namespace Unityroom.Client
{
    internal static class ConvertEx
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToString(int value)
        {
            return value switch
            {
                0 => "0",
                1 => "1",
                2 => "2",
                3 => "3",
                4 => "4",
                5 => "5",
                6 => "6",
                7 => "7",
                8 => "8",
                9 => "9",
                _ => value.ToString(),
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToHexString(ReadOnlySpan<byte> bytes, HexConverter.Casing casing)
        {
            if (bytes.Length == 0) return string.Empty;
            return HexConverter.ToString(bytes, casing);
        }
    }
}