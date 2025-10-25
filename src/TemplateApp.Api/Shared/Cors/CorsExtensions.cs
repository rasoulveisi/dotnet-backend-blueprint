using Microsoft.Net.Http.Headers;

namespace TemplateApp.Api.Shared.Cors;

public static class CorsExtensions
{
    public static IHostApplicationBuilder AddTemplateAppCors(this IHostApplicationBuilder builder)
    {
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(
                policy =>
                {
                    if (builder.Environment.IsDevelopment())
                    {
                        policy.AllowAnyOrigin();
                    }
                    else
                    {
                        var originsString = builder.Configuration["AllowedOrigins"] ?? string.Empty;
                        if (!string.IsNullOrEmpty(originsString))
                        {
                            var allowedOrigins = originsString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            policy.WithOrigins(allowedOrigins);
                        }
                        else
                        {
                            // Fallback: allow all origins if no whitelist is configured
                            policy.AllowAnyOrigin();
                        }
                    }

                    policy.WithHeaders(HeaderNames.Authorization, HeaderNames.ContentType)
                          .AllowAnyMethod();
                });
        });

        return builder;
    }
}