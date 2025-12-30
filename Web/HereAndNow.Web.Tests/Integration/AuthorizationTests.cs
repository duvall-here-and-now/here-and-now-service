using System.Net;
using FluentAssertions;
using HereAndNow.Web.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HereAndNow.Web.Tests.Integration;

public class AuthorizationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthorizationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }




}
