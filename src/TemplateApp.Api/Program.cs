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
    Console.WriteLine($"Raw connection string: {connectionString}");
    
    // Convert Railway PostgreSQL URL format to Entity Framework format
    if (!string.IsNullOrEmpty(connectionString) && connectionString.StartsWith("postgresql://"))
    {
        var uri = new Uri(connectionString);
        var convertedConnectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={uri.UserInfo.Split(':')[0]};Password={uri.UserInfo.Split(':')[1]};SSL Mode=Require;Trust Server Certificate=true";
        Console.WriteLine($"Converted connection string: {convertedConnectionString}");
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

if (app.Environment.IsDevelopment())
{
    app.UseTemplateAppSwaggerUI();
}
else
{
    app.UseExceptionHandler();
}

app.UseStatusCodePages();

// Run database migrations
await app.MigrateDbAsync();

app.Run();