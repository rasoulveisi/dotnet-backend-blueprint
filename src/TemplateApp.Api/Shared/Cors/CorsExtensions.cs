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
                    Console.WriteLine("CORS - Configuring simple policy");
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
        });

        return builder;
    }
}