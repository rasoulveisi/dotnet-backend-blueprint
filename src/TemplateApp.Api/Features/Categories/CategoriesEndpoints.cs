using TemplateApp.Api.Features.Categories.GetCategories;

namespace TemplateApp.Api.Features.Categories;

public static class CategoriesEndpoints
{
    public static void MapCategories(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/categories");

        group.MapGetCategories();
    }
}
