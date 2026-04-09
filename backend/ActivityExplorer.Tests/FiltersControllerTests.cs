using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using ActivityExplorer.API.Controllers;
using ActivityExplorer.Core.Models;
using ActivityExplorer.Data;

namespace ActivityExplorer.Tests;

public class FiltersControllerTests : IDisposable
{
    private readonly ActivityContext _context;
    private readonly FiltersController _controller;

    public FiltersControllerTests()
    {
        _context = TestDbHelper.CreateContext();
        _controller = new FiltersController(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetFilters_Empty_ReturnsEmptyList()
    {
        var result = await _controller.GetFilters();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var filters = ok.Value.Should().BeAssignableTo<List<SavedFilter>>().Subject;
        filters.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateFilter_ReturnsCreated()
    {
        var filter = new SavedFilter
        {
            Name = "Test Filter",
            UserId = "default",
            FilterJson = "{\"workloads\":[\"SharePoint\"]}"
        };

        var result = await _controller.CreateFilter(filter);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var saved = created.Value.Should().BeAssignableTo<SavedFilter>().Subject;
        saved.Name.Should().Be("Test Filter");
        saved.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetFilter_ExistingId_ReturnsFilter()
    {
        var filter = new SavedFilter
        {
            Name = "My Filter",
            UserId = "default",
            FilterJson = "{}"
        };
        _context.SavedFilters.Add(filter);
        await _context.SaveChangesAsync();

        var result = await _controller.GetFilter(filter.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var returned = ok.Value.Should().BeAssignableTo<SavedFilter>().Subject;
        returned.Name.Should().Be("My Filter");
    }

    [Fact]
    public async Task GetFilter_NonExistingId_ReturnsNotFound()
    {
        var result = await _controller.GetFilter(999);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteFilter_ExistingId_ReturnsNoContent()
    {
        var filter = new SavedFilter
        {
            Name = "To Delete",
            UserId = "default",
            FilterJson = "{}"
        };
        _context.SavedFilters.Add(filter);
        await _context.SaveChangesAsync();

        var result = await _controller.DeleteFilter(filter.Id);

        result.Should().BeOfType<NoContentResult>();
        _context.SavedFilters.Count().Should().Be(0);
    }

    [Fact]
    public async Task DeleteFilter_NonExistingId_ReturnsNotFound()
    {
        var result = await _controller.DeleteFilter(999);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetFilters_ReturnsOrderedByCreatedAtDesc()
    {
        _context.SavedFilters.AddRange(
            new SavedFilter { Name = "Old", UserId = "default", FilterJson = "{}", CreatedAt = DateTime.UtcNow.AddHours(-2) },
            new SavedFilter { Name = "New", UserId = "default", FilterJson = "{}", CreatedAt = DateTime.UtcNow }
        );
        await _context.SaveChangesAsync();

        var result = await _controller.GetFilters();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var filters = ok.Value.Should().BeAssignableTo<List<SavedFilter>>().Subject;
        filters.First().Name.Should().Be("New");
        filters.Last().Name.Should().Be("Old");
    }
}
