using Xunit;

namespace DeepSigma.DataAccess.Web.Tests;

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
        var dto = Serializer.LoadFromJson<Dto>("{\"name\":\"x\",\"count\":3}");

        Assert.NotNull(dto);
        Assert.Equal("x", dto!.Name);
        Assert.Equal(3, dto.Count);
    }

    [Fact]
    public void LoadFromJson_is_case_insensitive_on_property_names()
    {
        var dto = Serializer.LoadFromJson<Dto>("{\"Name\":\"x\",\"COUNT\":3}");

        Assert.Equal("x", dto!.Name);
        Assert.Equal(3, dto.Count);
    }

    [Fact]
    public void LoadFromJson_allows_numbers_quoted_as_strings()
    {
        var dto = Serializer.LoadFromJson<Dto>("{\"name\":\"x\",\"count\":\"3\"}");

        Assert.Equal(3, dto!.Count);
    }

    [Fact]
    public void LoadFromJson_returns_default_for_empty_input()
    {
        var dto = Serializer.LoadFromJson<Dto>("");
        Assert.Null(dto);
    }

    [Fact]
    public void LoadFromJson_with_AlphaVantage_guard_throws_on_Note_property()
    {
        const string body = "{\"Note\":\"Thank you for using Alpha Vantage! Please subscribe to a premium plan...\"}";

        var ex = Assert.Throws<InvalidOperationException>(() => Serializer.LoadFromJson<Dto>(body, guard: JsonResponseGuards.AlphaVantage));
        Assert.Contains("API note", ex.Message);
    }

    [Fact]
    public void LoadFromJson_with_AlphaVantage_guard_throws_on_Error_Message_property()
    {
        const string body = "{\"Error Message\":\"Invalid API call.\"}";

        var ex = Assert.Throws<InvalidOperationException>(() => Serializer.LoadFromJson<Dto>(body, guard: JsonResponseGuards.AlphaVantage));
        Assert.Contains("API error", ex.Message);
    }

    [Fact]
    public void LoadFromJson_without_guard_does_not_inspect_Note_or_Error_properties()
    {
        // The Note/Error check is opt-in: without a guard these deserialize like any other payload.
        var note = Serializer.LoadFromJson<Dto>("{\"Note\":\"ignored\",\"name\":\"ok\"}");
        Assert.Equal("ok", note!.Name);
    }
}
