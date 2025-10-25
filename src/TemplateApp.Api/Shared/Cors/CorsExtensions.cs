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
                        // Try to get AllowedOrigins as an array first
                        var allowedOriginsArray = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
                        
                        if (allowedOriginsArray != null && allowedOriginsArray.Length > 0)
                        {
                            Console.WriteLine($"CORS - Allowed origins (array): {string.Join(", ", allowedOriginsArray)}");
                            policy.WithOrigins(allowedOriginsArray);
                        }
                        else
                        {
                            // Fallback: try string format
                            var originsString = builder.Configuration["AllowedOrigins"] ?? string.Empty;
                            Console.WriteLine($"CORS - AllowedOrigins (string): {originsString}");
                            
                            if (!string.IsNullOrEmpty(originsString))
                            {
                                var allowedOrigins = originsString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                                Console.WriteLine($"CORS - Allowed origins (parsed): {string.Join(", ", allowedOrigins)}");
                                policy.WithOrigins(allowedOrigins);
                            }
                            else
                            {
                                Console.WriteLine("CORS - No whitelist configured, allowing all origins");
                                // Fallback: allow all origins if no whitelist is configured
                                policy.AllowAnyOrigin();
                            }
                        }
                    }

                    policy.WithHeaders(HeaderNames.Authorization, HeaderNames.ContentType, "X-Requested-With", "Accept", "Origin")
                          .AllowAnyMethod()
                          .AllowCredentials();
                });
        });

        return builder;
    }
}