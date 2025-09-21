using TemplateApp.Api.Data.Configurations;
using TemplateApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace TemplateApp.Api.Data;

public class TemplateAppContext(DbContextOptions<TemplateAppContext> options)
    : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();

    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ItemEntityConfiguration).Assembly);
    }
}
