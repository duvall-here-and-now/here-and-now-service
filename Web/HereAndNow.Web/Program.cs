using HereAndNowService.Configuration;
using HereAndNowService.Middlewares;
using HereAndNowService.Services;
using dotenv.net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Cosmos;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.Sources.Clear();
DotEnv.Load();
builder.Configuration.AddEnvironmentVariables();

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.AddServerHeader = false;
});

// Add services to the container.
builder.Services.AddScoped<IMessageService, MessageService>();

// Configure Cosmos DB
var cosmosSettings = new CosmosDbSettings
{
    Endpoint = builder.Configuration.GetValue<string>("COSMOS_ENDPOINT") ?? "",
    PrimaryKey = builder.Configuration.GetValue<string>("COSMOS_PRIMARY_KEY") ?? "",
    DatabaseName = builder.Configuration.GetValue<string>("COSMOS_DATABASE_NAME") ?? "",
    ContainerName = builder.Configuration.GetValue<string>("COSMOS_CONTAINER_NAME") ?? ""
};

var useCosmosDb = !string.IsNullOrEmpty(cosmosSettings.Endpoint) && !string.IsNullOrEmpty(cosmosSettings.PrimaryKey);

if (useCosmosDb)
{
    builder.Services.AddSingleton(cosmosSettings);
    builder.Services.AddSingleton<CosmosClient>(sp =>
        new CosmosClient(cosmosSettings.Endpoint, cosmosSettings.PrimaryKey, new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            },
            // Retry policy for 429 (TooManyRequests) throttling
            MaxRetryAttemptsOnRateLimitedRequests = 9,
            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30)
        }));
    builder.Services.AddScoped<IReminderInstanceService>(sp =>
        new CosmosReminderInstanceService(
            sp.GetRequiredService<CosmosClient>(),
            cosmosSettings.DatabaseName,
            cosmosSettings.ContainerName,
            sp.GetRequiredService<ILogger<CosmosReminderInstanceService>>()));
}
else
{
    // Fall back to in-memory implementation for local development without Cosmos
    builder.Services.AddSingleton<IReminderInstanceService, ReminderInstanceService>();
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var clientOriginUrlConfig = builder.Configuration.GetValue<string>("CLIENT_ORIGIN_URL")
            ?? throw new InvalidOperationException("CLIENT_ORIGIN_URL is not configured");

        var allowedOrigins = clientOriginUrlConfig
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        policy.WithOrigins(allowedOrigins)
            .WithHeaders(new string[] {
                HeaderNames.ContentType,
                HeaderNames.Authorization,
            })
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .SetPreflightMaxAge(TimeSpan.FromSeconds(86400));
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var auth0Domain = builder.Configuration.GetValue<string>("AUTH0_DOMAIN");
var auth0Audience = builder.Configuration.GetValue<string>("AUTH0_AUDIENCE");

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Here and Now API",
        Version = "v1",
        Description = "API for Here and Now service with Auth0 authentication"
    });

    // Include XML comments for better documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Define the OAuth2/JWT security scheme
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your Auth0 JWT token (without Bearer prefix)",
        In = ParameterLocation.Header,
        Name = "Authorization"
    });

    // Make all endpoints require the Bearer token by default
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://{auth0Domain}/";
        options.Audience = auth0Audience;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidIssuer = $"https://{auth0Domain}/",
            ValidateIssuerSigningKey = true
        };
    });

var app = builder.Build();

var requiredVars =
    new string[] {
          "PORT",
          "CLIENT_ORIGIN_URL",
          "AUTH0_DOMAIN",
          "AUTH0_AUDIENCE",
    };

foreach (var key in requiredVars)
{
    var value = app.Configuration.GetValue<string>(key);

    if (value == "" || value == null)
    {
        throw new Exception($"Config variable missing: {key}.");
    }
}

app.Urls.Add(
    $"http://+:{app.Configuration.GetValue<string>("PORT")}");

// Enable Swagger middleware
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Here and Now API v1");
    options.RoutePrefix = "swagger"; // Access Swagger UI at /swagger
    options.DocumentTitle = "Here and Now API Documentation";
    options.DisplayRequestDuration();
});

app.UseErrorHandler();
app.UseSecureHeaders();
app.MapControllers();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.Run();

// Make Program class accessible to tests
// See: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
public partial class Program { }
