﻿using System;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using SpanJson.Helpers;

namespace SpanJson
{
    public ref struct JsonWriter
    {
        private char[] _arrayToReturnToPool;
        private Span<char> _chars;
        private int _pos;

        public JsonWriter(Span<char> initialBuffer)
        {
            _arrayToReturnToPool = null;
            _chars = initialBuffer;
            _pos = 0;
        }

        public JsonWriter(int initialSize)
        {
            _arrayToReturnToPool = ArrayPool<char>.Shared.Rent(initialSize);
            _chars = _arrayToReturnToPool;
            _pos = 0;
        }

        public override string ToString()
        {
            var s = new string(_chars.Slice(0, _pos));
            Dispose();
            return s;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Grow(int requiredAdditionalCapacity)
        {
            Debug.Assert(requiredAdditionalCapacity > 0);

            var poolArray =
                ArrayPool<char>.Shared.Rent(Math.Max(_pos + requiredAdditionalCapacity, _chars.Length * 2));

            _chars.CopyTo(poolArray);

            var toReturn = _arrayToReturnToPool;
            _chars = _arrayToReturnToPool = poolArray;
            if (toReturn != null)
            {
                ArrayPool<char>.Shared.Return(toReturn);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            var toReturn = _arrayToReturnToPool;
            this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
            if (toReturn != null)
            {
                ArrayPool<char>.Shared.Return(toReturn);
            }
        }

        public void WriteSByte(sbyte value)
        {
            WriteInt64Internal(value);
        }

        public void WriteInt16(short value)
        {
            WriteInt64Internal(value);
        }

        public void WriteInt32(int value)
        {
            WriteInt64Internal(value);
        }

        public void WriteInt64(long value)
        {
            WriteInt64Internal(value);
        }

        private static readonly char[] LongMinValue = long.MinValue.ToString().ToCharArray();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteInt64Internal(long value)
        {
            ref var pos = ref _pos;
            if (value == long.MinValue)
            {
                if (pos > _chars.Length - 21)
                {
                    Grow(21);
                }

                LongMinValue.AsSpan().TryCopyTo(_chars.Slice(pos));
                pos += LongMinValue.Length;
            }
            else if (value < 0)
            {
                if (pos > _chars.Length - 1)
                {
                    Grow(1);
                }

                _chars[_pos++] = '-';
                value = unchecked(-value);
            }

            WriteUInt64Internal((ulong) value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUInt64Internal(ulong value)
        {
            ref var pos = ref _pos;
            if (value < 10)
            {
                if (pos > _chars.Length - 1)
                {
                    Grow(1);
                }

                _chars[_pos++] = (char) ('0' + value);
                return;
            }

            var digits = FormatterUtils.CountDigits(value);

            if (pos > _chars.Length - digits)
            {
                Grow(digits);
            }

            for (var i = digits; i > 0; i--)
            {
                var temp = '0' + value;
                value /= 10;
                _chars[pos + i - 1] = (char) (temp - value * 10);
            }

            pos += digits;
        }

        public void WriteByte(byte value)
        {
            WriteUInt64Internal(value);
        }

        public void WriteUInt16(ushort value)
        {
            WriteUInt64Internal(value);
        }

        public void WriteUInt32(uint value)
        {
            WriteUInt64Internal(value);
        }

        public void WriteUInt64(ulong value)
        {
            WriteUInt64Internal(value);
        }

        public void WriteSingle(float value)
        {
            Span<char> span = stackalloc char[25]; // TODO find out how long
            value.TryFormat(span, out var written, provider: CultureInfo.InvariantCulture);
            ref var pos = ref _pos;
            if (pos > _chars.Length - written)
            {
                Grow(written);
            }

            span.Slice(0, written).CopyTo(_chars.Slice(pos));
            pos += written;
        }

        public void WriteDouble(double value)
        {
            Span<char> span = stackalloc char[50]; // TODO find out how long
            value.TryFormat(span, out var written, provider: CultureInfo.InvariantCulture);
            ref var pos = ref _pos;
            if (pos > _chars.Length - written)
            {
                Grow(written);
            }

            span.Slice(0, written).CopyTo(_chars.Slice(pos));
            pos += written;
        }

        public void WriteDecimal(decimal value)
        {
            Span<char> span = stackalloc char[100]; // TODO find out how long
            value.TryFormat(span, out var written, provider: CultureInfo.InvariantCulture);
            ref var pos = ref _pos;
            if (pos > _chars.Length - written)
            {
                Grow(written);
            }

            span.Slice(0, written).CopyTo(_chars.Slice(pos));
            pos += written;
        }

        public void WriteBoolean(bool value)
        {
            ref var pos = ref _pos;
            if (value)
            {
                const int trueLength = 4;
                if (pos > _chars.Length - trueLength)
                {
                    Grow(trueLength);
                }

                _chars[pos++] = JsonConstant.True;
                _chars[pos++] = 'r';
                _chars[pos++] = 'u';
                _chars[pos++] = 'e';
            }
            else
            {
                const int falseLength = 5;
                if (pos > _chars.Length - falseLength)
                {
                    Grow(falseLength);
                }

                _chars[pos++] = JsonConstant.False;
                _chars[pos++] = 'a';
                _chars[pos++] = 'l';
                _chars[pos++] = 's';
                _chars[pos++] = 'e';
            }
        }

        public void WriteChar(char value)
        {
            ref var pos = ref _pos;
            const int size = 6; // 1-6 chars + two JsonConstant.DoubleQuote
            if (pos > _chars.Length - size)
            {
                Grow(size);
            }

            WriteDoubleQuote();
            switch (value)
            {
                case JsonConstant.DoubleQuote:
                    WriteSingleEscapedChar(JsonConstant.DoubleQuote);
                    break;
                case JsonConstant.Escape:
                    WriteSingleEscapedChar(JsonConstant.Escape);
                    break;
                case '\b':
                    WriteSingleEscapedChar('b');
                    break;
                case '\f':
                    WriteSingleEscapedChar('f');
                    break;
                case '\n':
                    WriteSingleEscapedChar('n');
                    break;
                case '\r':
                    WriteSingleEscapedChar('r');
                    break;
                case '\t':
                    WriteSingleEscapedChar('t');
                    break;
                case '\x0':
                    WriteDoubleEscapedCar('0', '0');
                    break;
                case '\x1':
                    WriteDoubleEscapedCar('0', '1');
                    break;
                case '\x2':
                    WriteDoubleEscapedCar('0', '2');
                    break;
                case '\x3':
                    WriteDoubleEscapedCar('0', '3');
                    break;
                case '\x4':
                    WriteDoubleEscapedCar('0', '4');
                    break;
                case '\x5':
                    WriteDoubleEscapedCar('0', '5');
                    break;
                case '\x6':
                    WriteDoubleEscapedCar('0', '6');
                    break;
                case '\x7':
                    WriteDoubleEscapedCar('0', '7');
                    break;
                case '\xB':
                    WriteDoubleEscapedCar('0', 'B');
                    break;
                case '\xE':
                    WriteDoubleEscapedCar('0', 'E');
                    break;
                case '\xF':
                    WriteDoubleEscapedCar('0', 'F');
                    break;
                case '\x10':
                    WriteDoubleEscapedCar('1', '0');
                    break;
                case '\x11':
                    WriteDoubleEscapedCar('1', '1');
                    break;
                case '\x12':
                    WriteDoubleEscapedCar('1', '2');
                    break;
                case '\x13':
                    WriteDoubleEscapedCar('1', '3');
                    break;
                case '\x14':
                    WriteDoubleEscapedCar('1', '4');
                    break;
                case '\x15':
                    WriteDoubleEscapedCar('1', '5');
                    break;
                case '\x16':
                    WriteDoubleEscapedCar('1', '6');
                    break;
                case '\x17':
                    WriteDoubleEscapedCar('1', '7');
                    break;
                case '\x18':
                    WriteDoubleEscapedCar('1', '8');
                    break;
                case '\x19':
                    WriteDoubleEscapedCar('1', '9');
                    break;
                case '\x1A':
                    WriteDoubleEscapedCar('1', 'A');
                    break;
                case '\x1B':
                    WriteDoubleEscapedCar('1', 'B');
                    break;
                case '\x1C':
                    WriteDoubleEscapedCar('1', 'C');
                    break;
                case '\x1D':
                    WriteDoubleEscapedCar('1', 'D');
                    break;
                case '\x1E':
                    WriteDoubleEscapedCar('1', 'E');
                    break;
                case '\x1F':
                    WriteDoubleEscapedCar('1', 'F');
                    break;
                default:
                    _chars[pos++] = value;
                    break;
            }

            WriteDoubleQuote();
        }

        public void WriteDateTime(DateTime value)
        {
            ref var pos = ref _pos;
            const int dtSize = 35; // Form o + two JsonConstant.DoubleQuote
            if (pos > _chars.Length - dtSize)
            {
                Grow(dtSize);
            }

            WriteDoubleQuote();
            value.TryFormat(_chars.Slice(pos), out var written, "O", CultureInfo.InvariantCulture);
            pos += written;
            WriteDoubleQuote();
        }

        public void WriteDateTimeOffset(DateTimeOffset value)
        {
            ref var pos = ref _pos;
            const int dtSize = 35; // Form o + two JsonConstant.DoubleQuote
            if (pos > _chars.Length - dtSize)
            {
                Grow(dtSize);
            }

            WriteDoubleQuote();
            value.TryFormat(_chars.Slice(pos), out var written, "O", CultureInfo.InvariantCulture);
            pos += written;
            WriteDoubleQuote();
        }

        public void WriteTimeSpan(TimeSpan value)
        {
            ref var pos = ref _pos;
            const int dtSize = 20; // Form o + two JsonConstant.DoubleQuote
            if (pos > _chars.Length - dtSize)
            {
                Grow(dtSize);
            }

            WriteDoubleQuote();
            value.TryFormat(_chars.Slice(pos), out var written, "c", CultureInfo.InvariantCulture);
            pos += written;
            WriteDoubleQuote();
        }

        public void WriteGuid(Guid value)
        {
            ref var pos = ref _pos;
            const int guidSize = 42; // Format D + two JsonConstant.DoubleQuote;
            if (pos > _chars.Length - guidSize)
            {
                Grow(guidSize);
            }

            WriteDoubleQuote();
            value.TryFormat(_chars.Slice(pos), out var written);
            pos += written;
            WriteDoubleQuote();
        }

        public void WriteString(string value)
        {
            ref var pos = ref _pos;
            var sLength = value.Length + 2;
            if (pos > _chars.Length - sLength)
            {
                Grow(sLength);
            }

            WriteDoubleQuote();
            var remaining = value.AsSpan();
            for (var i = 0; i < remaining.Length; i++)
            {
                var c = remaining[i];
                switch (c)
                {
                    case JsonConstant.DoubleQuote:
                        CopyAndEscape(ref remaining, ref i, JsonConstant.DoubleQuote);
                        break;
                    case JsonConstant.Escape:
                        CopyAndEscape(ref remaining, ref i, JsonConstant.Escape);
                        break;
                    case '\b':
                        CopyAndEscape(ref remaining, ref i, 'b');
                        break;
                    case '\f':
                        CopyAndEscape(ref remaining, ref i, 'f');
                        break;
                    case '\n':
                        CopyAndEscape(ref remaining, ref i, 'n');
                        break;
                    case '\r':
                        CopyAndEscape(ref remaining, ref i, 'r');
                        break;
                    case '\t':
                        CopyAndEscape(ref remaining, ref i, 't');
                        break;
                    case '\x0':
                        CopyAndEscapeUnicode(ref remaining, ref i, '0', '0');
                        break;
                    case '\x1':
                        CopyAndEscapeUnicode(ref remaining, ref i, '0', '1');
                        break;
                    case '\x2':
                        CopyAndEscapeUnicode(ref remaining, ref i, '0', '2');
                        break;
                    case '\x3':
                        CopyAndEscapeUnicode(ref remaining, ref i, '0', '3');
                        break;
                    case '\x4':
                        CopyAndEscapeUnicode(ref remaining, ref i, '0', '4');
                        break;
                    case '\x5':
                        CopyAndEscapeUnicode(ref remaining, ref i, '0', '5');
                        break;
                    case '\x6':
                        CopyAndEscapeUnicode(ref remaining, ref i, '0', '6');
                        break;
                    case '\x7':
                        CopyAndEscapeUnicode(ref remaining, ref i, '0', '7');
                        break;
                    case '\xB':
                        CopyAndEscapeUnicode(ref remaining, ref i, '0', 'B');
                        break;
                    case '\xE':
                        CopyAndEscapeUnicode(ref remaining, ref i, '0', 'E');
                        break;
                    case '\xF':
                        CopyAndEscapeUnicode(ref remaining, ref i, '0', 'F');
                        break;
                    case '\x10':
                        CopyAndEscapeUnicode(ref remaining, ref i, '1', '0');
                        break;
                    case '\x11':
                        CopyAndEscapeUnicode(ref remaining, ref i, '1', '1');
                        break;
                    case '\x12':
                        CopyAndEscapeUnicode(ref remaining, ref i, '1', '2');
                        break;
                    case '\x13':
                        CopyAndEscapeUnicode(ref remaining, ref i, '1', '3');
                        break;
                    case '\x14':
                        CopyAndEscapeUnicode(ref remaining, ref i, '1', '4');
                        break;
                    case '\x15':
                        CopyAndEscapeUnicode(ref remaining, ref i, '1', '5');
                        break;
                    case '\x16':
                        CopyAndEscapeUnicode(ref remaining, ref i, '1', '6');
                        break;
                    case '\x17':
                        CopyAndEscapeUnicode(ref remaining, ref i, '1', '7');
                        break;
                    case '\x18':
                        CopyAndEscapeUnicode(ref remaining, ref i, '1', '8');
                        break;
                    case '\x19':
                        CopyAndEscapeUnicode(ref remaining, ref i, '1', '9');
                        break;
                    case '\x1A':
                        CopyAndEscapeUnicode(ref remaining, ref i, '1', 'A');
                        break;
                    case '\x1B':
                        CopyAndEscapeUnicode(ref remaining, ref i, '1', 'B');
                        break;
                    case '\x1C':
                        CopyAndEscapeUnicode(ref remaining, ref i, '1', 'C');
                        break;
                    case '\x1D':
                        CopyAndEscapeUnicode(ref remaining, ref i, '1', 'D');
                        break;
                    case '\x1E':
                        CopyAndEscapeUnicode(ref remaining, ref i, '1', 'E');
                        break;
                    case '\x1F':
                        CopyAndEscapeUnicode(ref remaining, ref i, '1', 'F');
                        break;
                }
            }

            remaining.CopyTo(_chars.Slice(pos)); // if there is still something to copy we continue here
            pos += remaining.Length;
            WriteDoubleQuote();
        }

        /// <summary>
        ///     The value should already be properly escaped
        /// </summary>
        /// <param name="value"></param>
        public void WriteName(string value)
        {
            ref var pos = ref _pos;
            var sLength = value.Length + 3;
            if (pos > _chars.Length - sLength)
            {
                Grow(sLength);
            }

            WriteDoubleQuote();
            value.AsSpan().CopyTo(_chars.Slice(pos));
            pos += value.Length;
            WriteDoubleQuote();
            _chars[pos++] = JsonConstant.NameSeparator;
        }

        /// <summary>
        ///     We need copy the span up to the current index, then write the escape char and continue
        ///     This is one messy thing, resetting the iterator
        /// </summary>
        private void CopyAndEscape(ref ReadOnlySpan<char> remaining, ref int i, char toEscape)
        {
            ref var pos = ref _pos;
            remaining.Slice(0, i).CopyTo(_chars.Slice(pos));
            pos += i;
            if (pos > _chars.Length - 1) // one more now
            {
                Grow(1);
            }

            WriteSingleEscapedChar(toEscape);
            remaining = remaining.Slice(i + 1); // continuing after the escaped char
            i = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteSingleEscapedChar(char toEscape)
        {
            _chars[_pos++] = JsonConstant.Escape;
            _chars[_pos++] = toEscape;
        }

        private void CopyAndEscapeUnicode(ref ReadOnlySpan<char> remaining, ref int i, char firstToEscape, char secondToEscape)
        {
            ref var pos = ref _pos;
            remaining.Slice(0, i).CopyTo(_chars.Slice(pos));
            pos += i;
            const int length = 6;
            if (pos > _chars.Length - length) // one more now
            {
                Grow(length);
            }

            WriteDoubleEscapedCar(firstToEscape, secondToEscape);
            remaining = remaining.Slice(i + 1); // continuing after the escaped char
            i = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteDoubleEscapedCar(char firstToEscape, char secondToEscape)
        {
            _chars[_pos++] = JsonConstant.Escape;
            _chars[_pos++] = 'u';
            _chars[_pos++] = '0';
            _chars[_pos++] = '0';
            _chars[_pos++] = firstToEscape;
            _chars[_pos++] = secondToEscape;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectStart()
        {
            ref var pos = ref _pos;
            if (pos > _chars.Length - 1)
            {
                Grow(1);
            }

            _chars[pos++] = JsonConstant.BeginObject;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteObjectEnd()
        {
            ref var pos = ref _pos;
            if (pos > _chars.Length - 1)
            {
                Grow(1);
            }

            _chars[pos++] = JsonConstant.EndObject;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArrayStart()
        {
            ref var pos = ref _pos;
            if (pos > _chars.Length - 1)
            {
                Grow(1);
            }

            _chars[pos++] = JsonConstant.BeginArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArrayEnd()
        {
            ref var pos = ref _pos;
            if (pos > _chars.Length - 1)
            {
                Grow(1);
            }

            _chars[pos++] = JsonConstant.EndArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueSeparator()
        {
            ref var pos = ref _pos;
            if (pos > _chars.Length - 1)
            {
                Grow(1);
            }

            _chars[pos++] = JsonConstant.ValueSeparator;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNull()
        {
            ref var pos = ref _pos;
            const int nullLength = 4;
            if (pos > _chars.Length - nullLength)
            {
                Grow(nullLength);
            }

            _chars[pos++] = JsonConstant.Null;
            _chars[pos++] = 'u';
            _chars[pos++] = 'l';
            _chars[pos++] = 'l';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteDoubleQuote()
        {
            _chars[_pos++] = JsonConstant.String;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVersion(Version value)
        {
            ref var pos = ref _pos;
            const int versionLength = 45; // 4 * int + 3 . + 2 double quote
            if (pos > _chars.Length - versionLength)
            {
                Grow(versionLength);
            }

            WriteDoubleQuote();
            value.TryFormat(_chars.Slice(pos), out var written);
            pos += written;
            WriteDoubleQuote();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUri(Uri value)
        {
            WriteString(value.ToString()); // Uri does not implement ISpanFormattable
        }
    }
}