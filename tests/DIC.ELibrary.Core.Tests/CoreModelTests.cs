using DIC.ELibrary.Core.Models;

namespace DIC.ELibrary.Core.Tests;

public class OperationResultTests
{
    [Fact]
    public void Ok_SetsSucceeded_AndNoError()
    {
        var result = OperationResult.Ok();

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Fail_SetsError_AndNotSucceeded()
    {
        var result = OperationResult.Fail("boom");

        Assert.False(result.Succeeded);
        Assert.Equal("boom", result.Error);
    }

    [Fact]
    public void Generic_Ok_CarriesValue()
    {
        var result = OperationResult<int>.Ok(42);

        Assert.True(result.Succeeded);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Generic_Fail_HasDefaultValue()
    {
        var result = OperationResult<int>.Fail("nope");

        Assert.False(result.Succeeded);
        Assert.Equal("nope", result.Error);
        Assert.Equal(0, result.Value);
    }
}

public class PagedResultTests
{
    [Fact]
    public void TotalPages_RoundsUp()
    {
        var page = new PagedResult<int> { TotalCount = 21, PageSize = 10, Page = 1 };

        Assert.Equal(3, page.TotalPages);
    }

    [Fact]
    public void Navigation_FlagsReflectPosition()
    {
        var middle = new PagedResult<int> { TotalCount = 30, PageSize = 10, Page = 2 };
        Assert.True(middle.HasPrevious);
        Assert.True(middle.HasNext);

        var last = new PagedResult<int> { TotalCount = 30, PageSize = 10, Page = 3 };
        Assert.True(last.HasPrevious);
        Assert.False(last.HasNext);

        var first = new PagedResult<int> { TotalCount = 30, PageSize = 10, Page = 1 };
        Assert.False(first.HasPrevious);
        Assert.True(first.HasNext);
    }

    [Fact]
    public void Empty_IsEmpty()
    {
        var page = PagedResult<string>.Empty(2, 25);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(2, page.Page);
        Assert.Equal(25, page.PageSize);
        Assert.Equal(0, page.TotalPages);
    }

    [Fact]
    public void TotalPages_WithZeroPageSize_IsZero()
    {
        var page = new PagedResult<int> { TotalCount = 10, PageSize = 0 };

        Assert.Equal(0, page.TotalPages);
    }
}
