using HereAndNowService.Configuration;
using HereAndNowService.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace HereAndNow.Web.Tests.Helpers;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override configuration for testing - include Cosmos settings to pass validation
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PORT"] = "5000",
                ["CLIENT_ORIGIN_URL"] = "http://localhost:3000",
                ["AUTH0_DOMAIN"] = "test-domain.auth0.com",
                ["AUTH0_AUDIENCE"] = "https://test-api",
                ["COSMOS_ENDPOINT"] = "https://test-cosmos.documents.azure.com:443/",
                ["COSMOS_PRIMARY_KEY"] = "test-key-for-testing-only",
                ["COSMOS_DATABASE_NAME"] = "test-db",
                ["COSMOS_CONTAINER_NAME"] = "test-container"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove CosmosDbSettings registration to avoid validation
            var settingsDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(CosmosDbSettings));
            if (settingsDescriptor != null)
            {
                services.Remove(settingsDescriptor);
            }

            // Remove CosmosClient registration to avoid connection attempts
            var cosmosDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(CosmosClient));
            if (cosmosDescriptor != null)
            {
                services.Remove(cosmosDescriptor);
            }

            // Remove any existing IReminderInstanceService registration
            var serviceDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IReminderInstanceService));
            if (serviceDescriptor != null)
            {
                services.Remove(serviceDescriptor);
            }

            // Register mock service for testing
            var mockService = new Mock<IReminderInstanceService>();
            services.AddSingleton(mockService.Object);
        });

        builder.UseEnvironment("Testing");
    }
}
