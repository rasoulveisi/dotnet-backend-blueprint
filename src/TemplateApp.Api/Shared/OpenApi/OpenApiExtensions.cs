using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using TemplateApp.Api.Shared.Authentication;

namespace TemplateApp.Api.Shared.OpenApi;

public static class OpenApiExtensions
{
    public static IHostApplicationBuilder AddTemplateAppOpenApi(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info.Title = "TemplateApp API";
                var authOptions = context.ApplicationServices.GetRequiredService<IOptions<AuthOptions>>().Value;

                // Add OAuth2 security scheme
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>
                {
                    ["oauth2"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.OAuth2,
                        Description = "OAuth2 Authorization Code Flow with PKCE",
                        Flows = new OpenApiOAuthFlows
                        {
                            AuthorizationCode = new OpenApiOAuthFlow
                            {
                                AuthorizationUrl = new Uri(authOptions.Authority + "/protocol/openid-connect/auth"),
                                TokenUrl = new Uri(authOptions.Authority + "/protocol/openid-connect/token"),
                                Scopes = new Dictionary<string, string>
                                {
                                    [authOptions.ApiScope] = "Grants full access to the TemplateApp API"
                                },
                            }
                        }
                    }
                };

                // Add security requirement
                document.SecurityRequirements =
                [
                    new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "oauth2"
                                }
                            },
                            Array.Empty<string>()
                        }
                    }
                ];

                return Task.CompletedTask;
            });
        });

        return builder;
    }

    public static WebApplication UseTemplateAppSwaggerUI(this WebApplication app)
    {
        app.MapOpenApi();

        var authOptions = app.Services.GetRequiredService<IOptions<AuthOptions>>().Value;

        var swaggerUiClientId = app.Configuration["SWAGGERUI_CLIENTID"]
                                ?? throw new InvalidOperationException("SWAGGERUI_CLIENTID is not configured");

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "TemplateApp API v1");

            options.OAuthClientId(swaggerUiClientId);

            options.OAuthUsePkce();
            options.OAuthScopes(authOptions.ApiScope);

            options.EnablePersistAuthorization();
        });

        return app;
    }
}
