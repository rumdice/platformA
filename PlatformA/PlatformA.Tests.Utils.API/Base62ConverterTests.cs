using PlatformA.Library.Helper;

namespace PlatformA.Tests.Utils.API;

public class Base62ConverterTests
{
    [Fact]
    public void Encode_One_ReturnsOne()
    {
        var result = Base62Converter.Encode(1L);
        Assert.Equal("1", result);
    }

    [Fact]
    public void Encode_62_Returns10()
    {
        // 62 in base62 = "10" (1*62 + 0)
        var result = Base62Converter.Encode(62L);
        Assert.Equal("10", result);
    }

    [Fact]
    public void Encode_LargeValue_ReturnsNonEmptyString()
    {
        var result = Base62Converter.Encode(9_999_999_999L);
        Assert.False(string.IsNullOrEmpty(result));
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(62L)]
    [InlineData(3844L)]   // 62^2
    [InlineData(100_000L)]
    [InlineData(1_704_067_200_000L)]  // Snowflake epoch timestamp
    public void Decode_AfterEncode_RoundTripMatches(long original)
    {
        var encoded = Base62Converter.Encode(original);
        var decoded = Base62Converter.Decode(encoded);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Encode_SameInput_ReturnsSameOutput()
    {
        var result1 = Base62Converter.Encode(12345L);
        var result2 = Base62Converter.Encode(12345L);
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Encode_DifferentInputs_ReturnDifferentOutputs()
    {
        var result1 = Base62Converter.Encode(1L);
        var result2 = Base62Converter.Encode(2L);
        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void Encode_OnlyUsesBase62Characters()
    {
        const string validChars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var result = Base62Converter.Encode(987_654_321L);
        Assert.All(result, c => Assert.Contains(c, validChars));
    }
}
