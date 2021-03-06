﻿using System.Globalization;
using System.Text;
using SpanJson.Resolvers;
using Xunit;

namespace SpanJson.Tests
{
    public class EnumIntegerTests
    {
        public enum TestEnum
        {
            Hello = -1,
            None = 0,
            World = 1,
        }

        public enum TestLongEnum : long
        {
            Min = long.MinValue,
            None = 0,
            Max = long.MaxValue,
        }

        [Theory]
        [InlineData(TestEnum.Hello)]
        [InlineData(TestEnum.None)]
        [InlineData(TestEnum.World)]
        public void SerializeDeserializeUtf16(TestEnum value)
        {
            var serialized = JsonSerializer.Generic.Utf16.Serialize<TestEnum, ExcludeNullCamelCaseIntegerEnumResolver<char>>(value);
            Assert.Contains(((int) value).ToString(CultureInfo.InvariantCulture), serialized);
            var deserialized = JsonSerializer.Generic.Utf16.Deserialize<TestEnum, ExcludeNullCamelCaseIntegerEnumResolver<char>>(serialized);
            Assert.Equal(value, deserialized);
        }

        [Theory]
        [InlineData(TestEnum.Hello)]
        [InlineData(TestEnum.None)]
        [InlineData(TestEnum.World)]
        public void SerializeDeserializeUtf8(TestEnum value)
        {
            var serialized = JsonSerializer.Generic.Utf8.Serialize<TestEnum, ExcludeNullCamelCaseIntegerEnumResolver<byte>>(value);
            Assert.Contains(((int) value).ToString(CultureInfo.InvariantCulture), Encoding.UTF8.GetString(serialized));
            var deserialized = JsonSerializer.Generic.Utf8.Deserialize<TestEnum, ExcludeNullCamelCaseIntegerEnumResolver<byte>>(serialized);
            Assert.Equal(value, deserialized);
        }

        [Theory]
        [InlineData(TestLongEnum.Min)]
        [InlineData(TestLongEnum.None)]
        [InlineData(TestLongEnum.Max)]
        public void SerializeDeserializeLongUtf16(TestLongEnum value)
        {
            var serialized = JsonSerializer.Generic.Utf16.Serialize<TestLongEnum, ExcludeNullCamelCaseIntegerEnumResolver<char>>(value);
            Assert.Contains(((long) value).ToString(CultureInfo.InvariantCulture), serialized);
            var deserialized = JsonSerializer.Generic.Utf16.Deserialize<TestLongEnum, ExcludeNullCamelCaseIntegerEnumResolver<char>>(serialized);
            Assert.Equal(value, deserialized);
        }

        [Theory]
        [InlineData(TestLongEnum.Min)]
        [InlineData(TestLongEnum.None)]
        [InlineData(TestLongEnum.Max)]
        public void SerializeDeserializeLongUtf8(TestLongEnum value)
        {
            var serialized = JsonSerializer.Generic.Utf8.Serialize<TestLongEnum, ExcludeNullCamelCaseIntegerEnumResolver<byte>>(value);
            Assert.Contains(((long) value).ToString(CultureInfo.InvariantCulture), Encoding.UTF8.GetString(serialized));
            var deserialized = JsonSerializer.Generic.Utf8.Deserialize<TestLongEnum, ExcludeNullCamelCaseIntegerEnumResolver<byte>>(serialized);
            Assert.Equal(value, deserialized);
        }
    }
}