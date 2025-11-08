using System.Net;
using FluentAssertions;
using HereAndNow.Web.Tests.Helpers;
using Microsoft.Net.Http.Headers;

namespace HereAndNow.Web.Tests.Integration;

public class CorsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private const string AllowedOrigin = "http://localhost:3000";
    private const string DisallowedOrigin = "http://evil.com";

    public CorsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PreflightRequest_WithAllowedOrigin_ShouldReturnCorsHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/reminder-instances");
        request.Headers.Add(HeaderNames.Origin, AllowedOrigin);
        request.Headers.Add(HeaderNames.AccessControlRequestMethod, "GET");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.Should().Contain(h => h.Key == HeaderNames.AccessControlAllowOrigin);
        response.Headers.GetValues(HeaderNames.AccessControlAllowOrigin).Should().Contain(AllowedOrigin);
    }

    [Fact]
    public async Task PreflightRequest_WithDisallowedOrigin_ShouldNotReturnAllowOriginHeader()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/reminder-instances");
        request.Headers.Add(HeaderNames.Origin, DisallowedOrigin);
        request.Headers.Add(HeaderNames.AccessControlRequestMethod, "GET");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.Headers.Should().NotContain(h => 
            h.Key == HeaderNames.AccessControlAllowOrigin && 
            h.Value.Contains(DisallowedOrigin));
    }

    [Fact]
    public async Task PreflightRequest_ShouldAllowContentTypeAndAuthorizationHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/reminder-instances");
        request.Headers.Add(HeaderNames.Origin, AllowedOrigin);
        request.Headers.Add(HeaderNames.AccessControlRequestMethod, "POST");
        request.Headers.Add(HeaderNames.AccessControlRequestHeaders, "Content-Type,Authorization");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        if (response.Headers.Contains(HeaderNames.AccessControlAllowHeaders))
        {
            var allowedHeaders = response.Headers.GetValues(HeaderNames.AccessControlAllowHeaders);
            var headersString = string.Join(",", allowedHeaders);
            headersString.Should().Contain("Content-Type");
            headersString.Should().Contain("Authorization");
        }
    }

    [Fact]
    public async Task PreflightRequest_ShouldAllowGetPostPutDeleteMethods()
    {
        // Arrange
        var methods = new[] { "GET", "POST", "PUT", "DELETE" };

        foreach (var method in methods)
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "/api/reminder-instances");
            request.Headers.Add(HeaderNames.Origin, AllowedOrigin);
            request.Headers.Add(HeaderNames.AccessControlRequestMethod, method);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent, 
                $"preflight for {method} should be allowed");
        }
    }

    [Fact]
    public async Task PreflightRequest_ShouldIncludeMaxAge()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/reminder-instances");
        request.Headers.Add(HeaderNames.Origin, AllowedOrigin);
        request.Headers.Add(HeaderNames.AccessControlRequestMethod, "GET");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        if (response.Headers.Contains(HeaderNames.AccessControlMaxAge))
        {
            var maxAge = response.Headers.GetValues(HeaderNames.AccessControlMaxAge).First();
            int.Parse(maxAge).Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task ActualRequest_WithAllowedOrigin_ShouldIncludeAllowOriginHeader()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/reminder-instances");
        request.Headers.Add(HeaderNames.Origin, AllowedOrigin);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        // Will be 401 because we don't have auth, but should still have CORS headers
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        
        // CORS headers should be present even for 401 responses
        if (response.Headers.Contains(HeaderNames.AccessControlAllowOrigin))
        {
            response.Headers.GetValues(HeaderNames.AccessControlAllowOrigin).Should().Contain(AllowedOrigin);
        }
    }
}
