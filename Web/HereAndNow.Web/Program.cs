using HereAndNowService.DTOs;
using HereAndNowService.Middlewares;
using HereAndNowService.Repositories;
using HereAndNowService.Services;
using dotenv.net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
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

// Cosmos DB configuration
var cosmosConnectionString = builder.Configuration.GetValue<string>("COSMOS_CONNECTION_STRING");
var cosmosDbSettings = new CosmosDbSettings
{
    ConnectionString = cosmosConnectionString ?? string.Empty,
    DatabaseName = builder.Configuration.GetValue<string>("COSMOS_DATABASE_NAME") ?? "HereAndNow",
    ContainerName = builder.Configuration.GetValue<string>("COSMOS_CONTAINER_NAME") ?? "Tasks"
};

// Only register Cosmos DB services if connection string is configured
if (!string.IsNullOrEmpty(cosmosConnectionString))
{
    builder.Services.AddSingleton(cosmosDbSettings);
    builder.Services.AddSingleton<CosmosClient>(sp =>
    {
        var cosmosOptions = new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        };
        return new CosmosClient(cosmosDbSettings.ConnectionString, cosmosOptions);
    });
    builder.Services.AddSingleton<ITaskRepository, TaskRepository>();
    builder.Services.AddSingleton<ITaskReminderRepository, TaskReminderRepository>();
    builder.Services.AddScoped<ITaskService, TaskService>();
    builder.Services.AddScoped<ITaskReminderService, TaskReminderService>();
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
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .SetPreflightMaxAge(TimeSpan.FromSeconds(86400));
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Configure validation error responses to use project-standard error format
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var firstError = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .SelectMany(e => e.Value!.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault() ?? "Validation failed";

        var errorResponse = new ErrorResponseDto
        {
            Error = new ErrorDetailsDto
            {
                Code = "VALIDATION_ERROR",
                Message = firstError
            }
        };

        return new BadRequestObjectResult(errorResponse);
    };
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

// Create Cosmos DB database and container if they don't exist
if (!string.IsNullOrEmpty(cosmosConnectionString))
{
    using var scope = app.Services.CreateScope();
    var cosmosClient = scope.ServiceProvider.GetRequiredService<CosmosClient>();
    var database = await cosmosClient.CreateDatabaseIfNotExistsAsync(cosmosDbSettings.DatabaseName);
    await database.Database.CreateContainerIfNotExistsAsync(
        new ContainerProperties(cosmosDbSettings.ContainerName, "/userId"));
}

app.Run();

// Make Program class accessible to tests
// See: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
public partial class Program { }
