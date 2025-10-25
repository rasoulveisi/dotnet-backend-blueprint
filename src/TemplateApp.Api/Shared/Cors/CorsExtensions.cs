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
                    // Temporarily allow all origins for debugging
                    Console.WriteLine("CORS - Allowing all origins for debugging");
                    policy.AllowAnyOrigin();

                    policy.WithHeaders(
                            HeaderNames.Authorization, 
                            HeaderNames.ContentType, 
                            "X-Requested-With", 
                            "Accept", 
                            "Origin",
                            "Access-Control-Request-Method",
                            "Access-Control-Request-Headers"
                        )
                        .AllowAnyMethod()
                        .AllowCredentials()
                        .SetIsOriginAllowed(origin => 
                        {
                            Console.WriteLine($"CORS - Checking origin: {origin}");
                            return true; // Temporarily allow all for debugging
                        });
                });
        });

        return builder;
    }
}