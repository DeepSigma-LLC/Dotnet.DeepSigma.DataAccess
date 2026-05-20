using Xunit;

namespace DeepSigma.DataAccess.Http.Tests;

public class StaticHelperTests
{
    private sealed record Dto
    {
        public string Name { get; init; } = "";
        public int Count { get; init; }
    }

    [Fact]
    public void LoadFromJson_deserializes_well_formed_json()
    {
        var dto = HttpApi.LoadFromJson<Dto>("{\"name\":\"x\",\"count\":3}");

        Assert.NotNull(dto);
        Assert.Equal("x", dto!.Name);
        Assert.Equal(3, dto.Count);
    }

    [Fact]
    public void LoadFromJson_is_case_insensitive_on_property_names()
    {
        var dto = HttpApi.LoadFromJson<Dto>("{\"Name\":\"x\",\"COUNT\":3}");

        Assert.Equal("x", dto!.Name);
        Assert.Equal(3, dto.Count);
    }

    [Fact]
    public void LoadFromJson_allows_numbers_quoted_as_strings()
    {
        var dto = HttpApi.LoadFromJson<Dto>("{\"name\":\"x\",\"count\":\"3\"}");

        Assert.Equal(3, dto!.Count);
    }

    [Fact]
    public void LoadFromJson_returns_default_for_empty_input()
    {
        var dto = HttpApi.LoadFromJson<Dto>("");
        Assert.Null(dto);
    }

    [Fact]
    public void LoadFromJson_throws_on_Note_property_rate_limit_pattern()
    {
        const string body = "{\"Note\":\"Thank you for using Alpha Vantage! Please subscribe to a premium plan...\"}";

        var ex = Assert.Throws<InvalidOperationException>(() => HttpApi.LoadFromJson<Dto>(body));
        Assert.Contains("API note", ex.Message);
    }

    [Fact]
    public void LoadFromJson_throws_on_Error_Message_property()
    {
        const string body = "{\"Error Message\":\"Invalid API call.\"}";

        var ex = Assert.Throws<InvalidOperationException>(() => HttpApi.LoadFromJson<Dto>(body));
        Assert.Contains("API error", ex.Message);
    }
}
