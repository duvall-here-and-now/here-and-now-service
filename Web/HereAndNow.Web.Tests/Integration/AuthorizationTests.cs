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

    [Fact]
    public async Task GetAll_WithoutAuthentication_ShouldReturn401Unauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/reminder-instances");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_WithoutAuthentication_ShouldReturn401Unauthorized()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/reminder-instances/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithoutAuthentication_ShouldReturn401Unauthorized()
    {
        // Arrange
        var content = new StringContent(
            "{\"text\":\"Test\",\"scheduledDateAndTime\":\"2024-12-31T10:00:00Z\"}",
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/reminder-instances", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_WithoutAuthentication_ShouldReturn401Unauthorized()
    {
        // Arrange
        var id = Guid.NewGuid();
        var content = new StringContent(
            $"{{\"id\":\"{id}\",\"text\":\"Updated\",\"scheduledDateAndTime\":\"2024-12-31T10:00:00Z\"}}",
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PutAsync($"/api/reminder-instances/{id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_WithoutAuthentication_ShouldReturn401Unauthorized()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/reminder-instances/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
