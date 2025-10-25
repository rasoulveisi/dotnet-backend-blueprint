using Azure.Identity;
using TemplateApp.Api.Data;
using TemplateApp.Api.Features.Items;
using TemplateApp.Api.Shared.Cors;
using TemplateApp.Api.Shared.ErrorHandling;
using TemplateApp.Api.Shared.OpenApi;
using TemplateApp.Api.Shared.Authentication;
using Microsoft.AspNetCore.HttpLogging;
using TemplateApp.Api.Features.Categories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Check if we're running in production (Railway) or development (Aspire)
if (builder.Environment.IsProduction())
{
    // Production mode - use connection string
    Console.WriteLine($"Production mode");
    
    var connectionString = builder.Configuration.GetConnectionString("TemplateAppDB");
    
    // Convert Railway PostgreSQL URL format to Entity Framework format
    if (!string.IsNullOrEmpty(connectionString) && connectionString.StartsWith("postgresql://"))
    {
        var uri = new Uri(connectionString);
        var convertedConnectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={uri.UserInfo.Split(':')[0]};Password={uri.UserInfo.Split(':')[1]};SSL Mode=Require;Trust Server Certificate=true";
        connectionString = convertedConnectionString;
    }
    
    builder.Services.AddDbContext<TemplateAppContext>(options =>
        options.UseNpgsql(connectionString));
    
    // Add health checks for production
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
}
else
{
    // Development mode - use Aspire
    builder.AddServiceDefaults();
    
    var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        ManagedIdentityClientId = builder.Configuration["AZURE_CLIENT_ID"]
    });

    builder.AddTemplateAppNpgsql<TemplateAppContext>("TemplateAppDB", credential);
}

builder.Services.AddProblemDetails()
                .AddExceptionHandler<GlobalExceptionHandler>();

// Configure authentication options with validation
if (builder.Environment.IsProduction())
{
    // Production mode - disable authentication for now
    Console.WriteLine("Production mode - Authentication disabled");
}
else
{
    // Development mode - use full authentication
    builder.Services.AddOptions<AuthOptions>()
                    .Bind(builder.Configuration.GetSection(AuthOptions.SectionName))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

    // Register the JWT Bearer options configurator first
    builder.Services.ConfigureOptions<JwtBearerOptionsSetup>();

    // Then add the authentication services
    builder.Services.AddAuthentication()
                    .AddJwtBearer();

    builder.Services.AddAuthorizationBuilder();
}

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod |
                            HttpLoggingFields.RequestPath |
                            HttpLoggingFields.ResponseStatusCode |
                            HttpLoggingFields.Duration;
    options.CombineLogs = true;
});

builder.AddTemplateAppOpenApi();
builder.AddTemplateAppCors();

var app = builder.Build();

app.UseCors();
Console.WriteLine("CORS middleware added");

// Map endpoints conditionally
if (builder.Environment.IsProduction())
{
    // Production health checks
    app.MapHealthChecks("/health/ready");
    app.MapHealthChecks("/health/alive", new HealthCheckOptions
    {
        Predicate = r => r.Tags.Contains("live")
    });
}
else
{
    // Development - use Aspire endpoints
    app.MapDefaultEndpoints();
}

app.MapItems();
app.MapCategories();

app.UseHttpLogging();

// Enable Swagger in both development and production
app.UseTemplateAppSwaggerUI();

// Add CORS headers for Swagger UI
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
    context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
    context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseStatusCodePages();

// Run database migrations
await app.MigrateDbAsync();

app.Run();