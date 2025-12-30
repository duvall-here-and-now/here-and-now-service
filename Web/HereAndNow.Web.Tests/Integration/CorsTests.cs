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



}
