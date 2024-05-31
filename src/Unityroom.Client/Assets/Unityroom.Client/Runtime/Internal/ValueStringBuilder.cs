using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

#nullable enable

namespace Unityroom.Client
{
    internal ref partial struct ValueStringBuilder
    {
        char[]? arrayToReturnToPool;
        Span<char> chars;
        int pos;

        public ValueStringBuilder(Span<char> initialBuffer)
        {
            arrayToReturnToPool = null;
            chars = initialBuffer;
            pos = 0;
        }

        public ValueStringBuilder(int initialCapacity)
        {
            arrayToReturnToPool = ArrayPool<char>.Shared.Rent(initialCapacity);
            chars = arrayToReturnToPool;
            pos = 0;
        }

        public int Length
        {
            get => pos;
            set => pos = value;
        }

        public int Capacity => chars.Length;

        public void EnsureCapacity(int capacity)
        {
            if ((uint)capacity > (uint)chars.Length)
                Grow(capacity - pos);
        }

        public ref char this[int index]
        {
            get
            {
                Debug.Assert(index < pos);
                return ref chars[index];
            }
        }

        public override string ToString()
        {
            string s = chars.Slice(0, pos).ToString();
            Dispose();
            return s;
        }

        public Span<char> RawChars => chars;
        public ReadOnlySpan<char> AsSpan(bool terminate)
        {
            if (terminate)
            {
                EnsureCapacity(Length + 1);
                chars[Length] = '\0';
            }
            return chars.Slice(0, pos);
        }

        public ReadOnlySpan<char> AsSpan() => chars.Slice(0, pos);
        public ReadOnlySpan<char> AsSpan(int start) => chars.Slice(start, pos - start);
        public ReadOnlySpan<char> AsSpan(int start, int length) => chars.Slice(start, length);

        public bool TryCopyTo(Span<char> destination, out int charsWritten)
        {
            if (chars.Slice(0, pos).TryCopyTo(destination))
            {
                charsWritten = pos;
                Dispose();
                return true;
            }
            else
            {
                charsWritten = 0;
                Dispose();
                return false;
            }
        }

        public void Insert(int index, char value, int count)
        {
            if (pos > chars.Length - count)
            {
                Grow(count);
            }

            var remaining = pos - index;
            chars.Slice(index, remaining).CopyTo(chars.Slice(index + count));
            chars.Slice(index, count).Fill(value);
            pos += count;
        }

        public void Insert(int index, string? s)
        {
            if (s == null)
            {
                return;
            }

            var count = s.Length;

            if (pos > (chars.Length - count))
            {
                Grow(count);
            }

            var remaining = pos - index;
            chars.Slice(index, remaining).CopyTo(chars.Slice(index + count));
            s.AsSpan().CopyTo(chars.Slice(index));
            pos += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(char c)
        {
            if ((uint)pos < (uint)chars.Length)
            {
                chars[pos] = c;
                pos++;
            }
            else
            {
                GrowAndAppend(c);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(string? s)
        {
            if (s == null)
            {
                return;
            }

            if (s.Length == 1 && (uint)pos < (uint)chars.Length)
            {
                chars[pos] = s[0];
                this.pos = pos + 1;
            }
            else
            {
                AppendSlow(s);
            }
        }

        void AppendSlow(string s)
        {
            if (pos > chars.Length - s.Length)
            {
                Grow(s.Length);
            }

            s.AsSpan().CopyTo(chars.Slice(pos));
            pos += s.Length;
        }

        public void Append(char c, int count)
        {
            if (pos > chars.Length - count)
            {
                Grow(count);
            }

            var dst = chars.Slice(pos, count);
            for (int i = 0; i < dst.Length; i++)
            {
                dst[i] = c;
            }
            pos += count;
        }

        public void Append(ReadOnlySpan<char> value)
        {
            int pos = this.pos;
            if (pos > chars.Length - value.Length)
            {
                Grow(value.Length);
            }

            value.CopyTo(chars.Slice(this.pos));
            this.pos += value.Length;
        }

        public void Append<T>(T value, string? format = null, IFormatProvider? provider = null) where T : IFormattable
        {
            Append(value.ToString(format, provider));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<char> AppendSpan(int length)
        {
            int origPos = pos;
            if (origPos > chars.Length - length)
            {
                Grow(length);
            }

            pos = origPos + length;
            return chars.Slice(origPos, length);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void GrowAndAppend(char c)
        {
            Grow(1);
            Append(c);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void Grow(int additionalCapacityBeyondPos)
        {
            const uint ArrayMaxLength = 0x7FFFFFC7; // same as Array.MaxLength

            var newCapacity = (int)Math.Max(
                (uint)(pos + additionalCapacityBeyondPos),
                Math.Min((uint)chars.Length * 2, ArrayMaxLength));

            var poolArray = ArrayPool<char>.Shared.Rent(newCapacity);

            chars.Slice(0, pos).CopyTo(poolArray);

            var toReturn = arrayToReturnToPool;
            chars = arrayToReturnToPool = poolArray;
            if (toReturn != null)
            {
                ArrayPool<char>.Shared.Return(toReturn);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            var toReturn = arrayToReturnToPool;
            this = default;
            if (toReturn != null)
            {
                ArrayPool<char>.Shared.Return(toReturn);
            }
        }
    }
}