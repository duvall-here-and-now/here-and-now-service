using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace HereAndNow.Web.Tests.Helpers;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
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

        builder.UseEnvironment("Testing");
    }
}
