using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoFixture;
using TemplateApp.Api.Data;
using TemplateApp.Api.Features.Items.CreateItem;
using TemplateApp.Api.Features.Items.UpdateItem;
using TemplateApp.Api.Features.Items.GetItems;
using TemplateApp.Api.IntegrationTests.Helpers;
using TemplateApp.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Testcontainers.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace TemplateApp.Api.IntegrationTests.Items;

public class ItemsEndpointsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgreContainer = new PostgreSqlBuilder().Build();
    private readonly Fixture fixture = new();

    public async Task InitializeAsync()
    {
        await postgreContainer.StartAsync();
        fixture.Customize<DateOnly>(o => o.FromFactory(() => DateOnly.FromDateTime(DateTime.UtcNow)));
        fixture.Customize<Category>(o => o.With(g => g.Name, fixture.Create<string>().Substring(0, 20)));
    }

    #region GetAll

    [Fact]
    public async Task GetAll_WithValidRequest_ReturnsItems()
    {
        // Arrange
        var application = new TemplateAppWebApplicationFactory(postgreContainer);

        var db = application.CreateDbContext();

        var item = fixture.Build<Item>()
                        .Create();

        db.Items.Add(item);

        await db.SaveChangesAsync();

        var client = application.CreateClient();

        // Act
        var response = await client.GetAsync("/items");

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 200-299

        var expectedDto = new ItemSummaryDto(
                        item.Id,
                        item.Name,
                        item.Category!.Name,
                        item.Price,
                        item.ReleaseDate,
                        item.LastUpdatedBy);
        var itemsResponse = await response.Content.ReadFromJsonAsync<ItemsPageDto>();
        itemsResponse.ShouldNotBeNull();
        itemsResponse.Data.ShouldNotBeNull();
        itemsResponse.Data.Count().ShouldBe(1);
        itemsResponse.Data.First().ShouldBeEquivalentTo(expectedDto);
    }

    #endregion

    #region GetById

    [Fact]
    public async Task GetById_WithValidId_ReturnsItem()
    {
        // Arrange
        var application = new TemplateAppWebApplicationFactory(postgreContainer);
        var db = application.CreateDbContext();

        var item = fixture.Build<Item>()
            .Create();

        db.Items.Add(item);

        await db.SaveChangesAsync();

        var client = application.CreateClient();

        // Act
        var response = await client.GetAsync($"/items/{item.Id}");

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 200-299

        var expectedDto = new ItemDetailsDto(
            item.Id,
            item.Name,
            item.CategoryId,
            item.Price,
            item.ReleaseDate,
            item.Description,
            item.LastUpdatedBy);

        var itemResponse = await response.Content.ReadFromJsonAsync<ItemDetailsDto>();
        itemResponse.ShouldNotBeNull();
        itemResponse.ShouldBeEquivalentTo(expectedDto);
    }

    [Fact]
    public async Task GetById_WithUnexistingId_ReturnsNotFound()
    {
        // Arrange
        var application = new TemplateAppWebApplicationFactory(postgreContainer);

        var unexistingId = Guid.NewGuid();

        var client = application.CreateClient();

        // Act
        var response = await client.GetAsync($"/items/{unexistingId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region Post

    [Fact]
    public async Task Post_WithValidRequest_CreatesItem()
    {
        // Arrange
        var testEmail = GenerateTestEmail();
        var application = new TemplateAppWebApplicationFactory(
            postgreContainer,
            email: testEmail);

        var db = application.CreateDbContext();
        var categoryId = GetFirstCategoryId(db);

        var client = application.CreateClient();

        var createItemDto = new CreateItemDto(
            Name: "Test Item",
            CategoryId: categoryId,
            Price: 10,
            ReleaseDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Description: "Test Description"
        );

        // Act
        var response = await client.PostAsJsonAsync("/items", createItemDto);

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 200-299

        var itemResponse = await response.Content.ReadFromJsonAsync<ItemDetailsDto>();
        itemResponse.ShouldNotBeNull();
        itemResponse.Id.ShouldNotBe(Guid.Empty);

        // Create a fresh db context to avoid caching issues
        var freshDb = application.CreateDbContext();
        var createdItem = await freshDb.Items.FindAsync(itemResponse.Id);
        createdItem.ShouldNotBeNull();
        createdItem.Name.ShouldBe(createItemDto.Name);
        createdItem.CategoryId.ShouldBe(createItemDto.CategoryId);
        createdItem.Price.ShouldBe(createItemDto.Price);
        createdItem.ReleaseDate.ShouldBe(createItemDto.ReleaseDate);
        createdItem.Description.ShouldBe(createItemDto.Description);
        createdItem.LastUpdatedBy.ShouldBe(testEmail);
    }

    [Fact]
    public async Task Post_MissingRequiredInfo_ReturnsBadRequest()
    {
        // Arrange
        var application = new TemplateAppWebApplicationFactory(
            postgreContainer);

        var db = application.CreateDbContext();
        var categoryId = GetFirstCategoryId(db);

        var client = application.CreateClient();

        var createItemDto = new CreateItemDto(
            Name: string.Empty, // Invalid - empty name
            CategoryId: categoryId,
            Price: 10,
            ReleaseDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Description: "Test Description"
        );

        // Act
        var response = await client.PostAsJsonAsync("/items", createItemDto);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        object? errorsJson = problemDetails?.Extensions["errors"];
        errorsJson.ShouldNotBeNull();
        var errors = JsonSerializer.Deserialize<Dictionary<string, string[]>>(errorsJson.ToString()!);
        errors.ShouldNotBeNull();
        errors.ShouldContainKey(nameof(createItemDto.Name));
    }

    #endregion

    #region Put

    [Fact]
    public async Task Put_WithValidRequest_UpdatesItem()
    {
        // Arrange
        var testEmail = GenerateTestEmail();
        var application = new TemplateAppWebApplicationFactory(
            postgreContainer,
            email: testEmail);

        var db = application.CreateDbContext();
        var categoryId = GetFirstCategoryId(db);

        var item = fixture.Build<Item>()
                        .With(x => x.CategoryId, categoryId)
                        .Create();

        db.Items.Add(item);
        await db.SaveChangesAsync();

        var client = application.CreateClient();

        var updateItemDto = new UpdateItemDto(
            Name: "Updated Item",
            CategoryId: categoryId,
            Price: 20,
            ReleaseDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Description: "Updated Description"
        );

        // Act
        var response = await client.PutAsJsonAsync($"/items/{item.Id}", updateItemDto);

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 200-299

        // Create a fresh db context to avoid caching issues
        var freshDb = application.CreateDbContext();
        var updatedItem = await freshDb.Items.FindAsync(item.Id);
        updatedItem.ShouldNotBeNull();
        updatedItem.Name.ShouldBe(updateItemDto.Name);
        updatedItem.CategoryId.ShouldBe(updateItemDto.CategoryId);
        updatedItem.Price.ShouldBe(updateItemDto.Price);
        updatedItem.ReleaseDate.ShouldBe(updateItemDto.ReleaseDate);
        updatedItem.Description.ShouldBe(updateItemDto.Description);
    }

    [Fact]
    public async Task Put_NoAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var application = new TemplateAppWebApplicationFactory(
            postgreContainer,
            authenticationSucceeds: false);

        var db = application.CreateDbContext();
        var categoryId = GetFirstCategoryId(db);

        var client = application.CreateClient();

        var updateItemDto = new UpdateItemDto(
            Name: "Updated Item",
            CategoryId: categoryId,
            Price: 20,
            ReleaseDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Description: "Updated Description"
        );

        // Act
        var response = await client.PutAsJsonAsync($"/items/{Guid.NewGuid()}", updateItemDto);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Delete

    [Fact]
    public async Task Delete_WithValidId_DeletesItem()
    {
        // Arrange
        var application = new TemplateAppWebApplicationFactory(
            postgreContainer);

        var db = application.CreateDbContext();

        var item = fixture.Build<Item>()
            .Create();

        db.Items.Add(item);

        await db.SaveChangesAsync();

        var client = application.CreateClient();

        // Act
        var response = await client.DeleteAsync($"/items/{item.Id}");

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 200-299

        db = application.CreateDbContext();
        var itemInDb = await db.Items.FindAsync(item.Id);
        itemInDb.ShouldBeNull();
    }

    #endregion

    public async Task DisposeAsync()
    {
        await postgreContainer.DisposeAsync();
    }

    #region Helpers

    private static string GenerateTestEmail()
    {
        var guid = Guid.NewGuid().ToString("N")[..8]; // Take first 8 characters of guid without hyphens
        return $"test-{guid}@example.com";
    }

    private static Guid GetFirstCategoryId(TemplateAppContext db)
    {
        return db.Categories.First().Id;
    }    

    #endregion
}
