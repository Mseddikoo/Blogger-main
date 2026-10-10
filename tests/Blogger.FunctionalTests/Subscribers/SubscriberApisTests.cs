using System.Net;
using System.Text;
using Blogger.FunctionalTests.Helper;
using Newtonsoft.Json;

namespace Blogger.FunctionalTests.Subscribers;

/// <summary>
/// Functional tests for Subscriber endpoints:
/// - POST /subscribe (Subscribe to blog newsletter)
/// </summary>
public class SubscriberApisTests : IClassFixture<BloggerWebApplicationFactory>
{
    private readonly HttpClient _httpClient;

    public SubscriberApisTests(BloggerWebApplicationFactory factory)
    {
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task Subscribe_With_Valid_Email_Should_Return_OkStatus()
    {
        // Arrange - prepare a unique subscriber email
        var randomId = StringGenerator.GenerateRandomString(6);
        var requestPayload = new
        {
            Email = $"subscriber_{randomId}@example.com"
        };
        var content = new StringContent(JsonConvert.SerializeObject(requestPayload), Encoding.UTF8, "application/json");

        // Act - send POST to /subscribe
        var response = await _httpClient.PostAsync("subscribe", content);

        // Assert - expect HTTP 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Subscribe_With_Invalid_Email_Should_Return_BadRequest()
    {
        // Arrange - prepare an invalid email
        var requestPayload = new
        {
            Email = "not-an-email"
        };
        var content = new StringContent(JsonConvert.SerializeObject(requestPayload), Encoding.UTF8, "application/json");

        // Act - send POST to /subscribe
        var response = await _httpClient.PostAsync("subscribe", content);

        // Assert - expect HTTP 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
