using HereAndNowService.Models;
using HereAndNowService.Repositories;
using HereAndNowService.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace HereAndNow.Web.Tests.Helpers;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public Mock<ITaskRepository> MockTaskRepository { get; } = new();
    public Mock<ITaskService> MockTaskService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override configuration for testing
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PORT"] = "5000",
                ["CLIENT_ORIGIN_URL"] = "http://localhost:3000",
                ["AUTH0_DOMAIN"] = "test-domain.auth0.com",
                ["AUTH0_AUDIENCE"] = "https://test-api"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Add mock Task services for integration tests
            services.AddSingleton(new CosmosDbSettings
            {
                ConnectionString = "",
                DatabaseName = "TestDb",
                ContainerName = "TestTasks"
            });
            services.AddSingleton<ITaskRepository>(MockTaskRepository.Object);
            services.AddScoped<ITaskService>(sp => MockTaskService.Object);

            // Add test authentication handler
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
        });

        builder.UseEnvironment("Testing");
    }
}
