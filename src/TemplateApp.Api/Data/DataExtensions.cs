using Azure.Core;
using TemplateApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace TemplateApp.Api.Data;

public static class DataExtensions
{
    public static WebApplicationBuilder AddTemplateAppNpgsql<TContext>(
        this WebApplicationBuilder builder,
        string connectionStringName,
        TokenCredential credential
    ) where TContext : DbContext
    {
        if (builder.Environment.IsProduction())
        {
            builder.AddAzureNpgsqlDbContext<TContext>(
                connectionStringName,
                settings => settings.Credential = credential,
                configureDbContextOptions: options =>
                    ConfigureDbContext(options)
            );
        }
        else
        {
            builder.AddNpgsqlDbContext<TContext>(
                connectionStringName,
                configureDbContextOptions: options =>
                    ConfigureDbContext(options));
        }

        return builder;
    }

    public static async Task MigrateDbAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        TemplateAppContext dbContext = scope.ServiceProvider
                                          .GetRequiredService<TemplateAppContext>();
        await dbContext.Database.MigrateAsync();
    }    

    private static DbContextOptionsBuilder ConfigureDbContext(DbContextOptionsBuilder options)
    {
        return options.UseSeeding((context, _) =>
                    {
                        if (!context.Set<Category>().Any())
                        {
                            SeedCategories(context);

                            context.SaveChanges();
                        }
                    })
                    .UseAsyncSeeding(async (context, _, cancellationToken) =>
                    {
                        if (!context.Set<Category>().Any())
                        {
                            SeedCategories(context);

                            await context.SaveChangesAsync(cancellationToken);
                        }
                    });
    }

    private static void SeedCategories(DbContext context)
    {
        context.Set<Category>().AddRange(
            new Category { Name = "General" },
            new Category { Name = "Urgent" },
            new Category { Name = "Archived" },
            new Category { Name = "Favorites" },
            new Category { Name = "Upcoming" }
        );
    }
}
