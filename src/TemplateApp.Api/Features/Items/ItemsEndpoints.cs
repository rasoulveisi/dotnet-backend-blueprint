using TemplateApp.Api.Data;
using TemplateApp.Api.Features.Items.CreateItem;
using TemplateApp.Api.Features.Items.DeleteItem;
using TemplateApp.Api.Features.Items.GetItem;
using TemplateApp.Api.Features.Items.GetItems;
using TemplateApp.Api.Features.Items.UpdateItem;
using TemplateApp.Api.Models;

namespace TemplateApp.Api.Features.Items;

public static class ItemsEndpoints
{
    public static void MapItems(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/items");

        group.MapGetItems();
        group.MapGetItem();
        group.MapCreateItem();
        group.MapUpdateItem();
        group.MapDeleteItem();
    }
}
