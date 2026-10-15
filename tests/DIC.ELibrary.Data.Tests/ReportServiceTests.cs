using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;

namespace DIC.ELibrary.Data.Tests;

public class ReportServiceTests
{
    [Fact]
    public async Task StatisticsReport_ContainsSummaryAndCategorySections()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var categories = host.GetService<ICategoryService>();
        var books = host.GetService<IBookService>();
        var users = host.GetService<IUserService>();
        var loans = host.GetService<ILoanService>();
        var reports = host.GetService<IReportService>();

        var category = (await categories.GetAllAsync()).First();
        var created = await books.CreateAsync(new BookEditModel
        {
            Title = "Report Book",
            Status = BookStatus.Published,
            CategoryId = category.Id
        }, null);
        Assert.True(created.Succeeded);

        var user = await users.CreateAsync(new UserEditModel { Username = "stats", Role = UserRole.Student }, "Stats@123");
        Assert.True((await loans.BorrowAsync(user.Value!.Id, created.Value!.Id)).Succeeded);

        var csv = await reports.BuildStatisticsReportCsvAsync();

        Assert.Contains("Section,Label,Value", csv);
        Assert.Contains("Summary,Total books,1", csv);
        Assert.Contains("Summary,Published,1", csv);
        Assert.Contains("Books by category", csv);
        Assert.Contains(category.Name, csv);
        Assert.Contains("Most borrowed,Report Book,1", csv);
        Assert.Contains("Recent additions,Report Book", csv);
    }

    [Fact]
    public async Task CirculationReport_ListsLoans()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var books = host.GetService<IBookService>();
        var users = host.GetService<IUserService>();
        var loans = host.GetService<ILoanService>();
        var reports = host.GetService<IReportService>();

        var book = await books.CreateAsync(new BookEditModel { Title = "Circulated", Status = BookStatus.Published }, null);
        var user = await users.CreateAsync(new UserEditModel { Username = "reader", Role = UserRole.Student }, "Reader@123");
        var borrow = await loans.BorrowAsync(user.Value!.Id, book.Value!.Id);
        Assert.True(borrow.Succeeded);

        var csv = await reports.BuildCirculationReportCsvAsync();

        Assert.Contains("LoanId,Book,User,Borrowed,Due,Returned,Status,Overdue", csv);
        Assert.Contains("Circulated", csv);
        Assert.Contains("reader", csv);
        Assert.Contains("Active", csv);
    }

    [Fact]
    public async Task CirculationReport_EscapesSpecialCharacters()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var books = host.GetService<IBookService>();
        var users = host.GetService<IUserService>();
        var loans = host.GetService<ILoanService>();
        var reports = host.GetService<IReportService>();

        var book = await books.CreateAsync(new BookEditModel { Title = "Once, \"Twice\"", Status = BookStatus.Published }, null);
        var user = await users.CreateAsync(new UserEditModel { Username = "comma", Role = UserRole.Student }, "Comma@123");
        var borrow = await loans.BorrowAsync(user.Value!.Id, book.Value!.Id);
        Assert.True(borrow.Succeeded);
        var csv = await reports.BuildCirculationReportCsvAsync();

        Assert.Contains("\"Once, \"\"Twice\"\"\"", csv);
    }

    [Fact]
    public async Task AuditReport_ContainsEntries()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var users = host.GetService<IUserService>();
        var reports = host.GetService<IReportService>();

        await users.CreateAsync(new UserEditModel { Username = "audited", Role = UserRole.Student }, "Audited@123");

        var csv = await reports.BuildAuditReportCsvAsync();

        Assert.Contains("Id,Timestamp,User,Action,EntityType,EntityId,Details", csv);
        Assert.Contains("UserCreated", csv);
    }
}
