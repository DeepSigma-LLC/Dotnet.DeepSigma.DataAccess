using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Serialization;
using DeepSigma.DataAccess.Http.Tests.Infrastructure;
using Xunit;

namespace DeepSigma.DataAccess.Http.Tests;

public class HttpApi_Xml_Tests
{
    [XmlRoot("person")]
    public sealed class PersonDto
    {
        [XmlElement("name")] public string Name { get; set; } = "";
        [XmlElement("age")] public int Age { get; set; }
    }

    [XmlRoot("item")]
    public sealed class ItemDto
    {
        [XmlElement("id")] public int Id { get; set; }
        [XmlElement("label")] public string Label { get; set; } = "";
    }

    private static StubHttpMessageHandler WithXmlBody(string xml)
        => new()
        {
            Responder = (_, _) =>
            {
                var content = new StringContent(xml, Encoding.UTF8);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            },
        };

    [Fact]
    public async Task GetXmlResponseAsync_ReturnsBody()
    {
        const string xml = "<person><name>Ada</name><age>36</age></person>";
        var http = new HttpApi(new HttpClient(WithXmlBody(xml)));

        string? body = await http.GetXmlResponseAsync(
            "https://example.com/x",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(xml, body);
    }

    [Fact]
    public async Task GetDataFromXmlUrlAsync_DeserializesIntoT()
    {
        const string xml = "<person><name>Grace</name><age>85</age></person>";
        var http = new HttpApi(new HttpClient(WithXmlBody(xml)));

        PersonDto? dto = await http.GetDataFromXmlUrlAsync<PersonDto>(
            "https://example.com/x",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(dto);
        Assert.Equal("Grace", dto!.Name);
        Assert.Equal(85, dto.Age);
    }

    [Fact]
    public async Task GetDataFromXmlUrlAsync_InvokesLoggingCallback()
    {
        const string xml = "<person><name>Linus</name><age>56</age></person>";
        var http = new HttpApi(new HttpClient(WithXmlBody(xml)));
        string? observed = null;

        await http.GetDataFromXmlUrlAsync<PersonDto>(
            "https://example.com/x",
            apiResultLoggingMethod: body => observed = body,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(xml, observed);
    }

    [Fact]
    public async Task StreamXmlElementsAsync_YieldsAllMatches()
    {
        const string xml = """
            <root>
              <item><id>1</id><label>one</label></item>
              <item><id>2</id><label>two</label></item>
              <item><id>3</id><label>three</label></item>
            </root>
            """;
        var http = new HttpApi(new HttpClient(WithXmlBody(xml)));

        List<ItemDto> items = new();
        await foreach (var item in http.StreamXmlElementsAsync<ItemDto>(
                           "https://example.com/x",
                           "item",
                           cancellationToken: TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        Assert.Equal(3, items.Count);
        Assert.Equal(new[] { 1, 2, 3 }, items.Select(i => i.Id));
    }

    [Fact]
    public async Task StreamXmlElementsAsync_AllowsEarlyBreak()
    {
        // Build a moderately large payload; consumer should be able to stop after N.
        var sb = new StringBuilder("<root>");
        for (int i = 0; i < 500; i++) { sb.Append("<item><id>").Append(i).Append("</id><label>x</label></item>"); }
        sb.Append("</root>");
        var http = new HttpApi(new HttpClient(WithXmlBody(sb.ToString())));

        int seen = 0;
        await foreach (var _ in http.StreamXmlElementsAsync<ItemDto>(
                           "https://example.com/x",
                           "item",
                           cancellationToken: TestContext.Current.CancellationToken))
        {
            seen++;
            if (seen == 5) { break; }
        }

        Assert.Equal(5, seen);
    }

    [Fact]
    public void LoadFromXml_StaticHelper_Deserializes()
    {
        PersonDto? dto = HttpApi.LoadFromXml<PersonDto>("<person><name>Margaret</name><age>84</age></person>");

        Assert.NotNull(dto);
        Assert.Equal("Margaret", dto!.Name);
        Assert.Equal(84, dto.Age);
    }

    [Fact]
    public void LoadFromXml_ReturnsDefault_ForEmptyInput()
    {
        Assert.Null(HttpApi.LoadFromXml<PersonDto>(""));
    }
}
