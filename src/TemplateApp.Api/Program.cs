using Azure.Identity;
using TemplateApp.Api.Data;
using TemplateApp.Api.Features.Items;
using TemplateApp.Api.Shared.Cors;
using TemplateApp.Api.Shared.ErrorHandling;
using TemplateApp.Api.Shared.OpenApi;
using TemplateApp.Api.Shared.Authentication;
using Microsoft.AspNetCore.HttpLogging;
using TemplateApp.Api.Features.Categories;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails()
                .AddExceptionHandler<GlobalExceptionHandler>();

var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    ManagedIdentityClientId = builder.Configuration["AZURE_CLIENT_ID"]
});

builder.AddTemplateAppNpgsql<TemplateAppContext>("TemplateAppDB", credential);

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

app.MapDefaultEndpoints();
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

await app.MigrateDbAsync();

app.Run();