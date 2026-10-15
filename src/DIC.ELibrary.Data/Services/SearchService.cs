using System.Text;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.Core.Options;
using DIC.ELibrary.Data.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DIC.ELibrary.Data.Services;

public sealed class SearchService : ISearchService
{
    private readonly IDbContextFactory<LibraryDbContext> _factory;
    private readonly LibraryOptions _library;
    private readonly ILogger<SearchService> _logger;

    public SearchService(
        IDbContextFactory<LibraryDbContext> factory,
        IOptions<LibraryOptions> library,
        ILogger<SearchService> logger)
    {
        _factory = factory;
        _library = library.Value;
        _logger = logger;
    }

    public async Task EnsureIndexAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE VIRTUAL TABLE IF NOT EXISTS BookSearch USING fts5(
                BookId UNINDEXED, Title, Author, Description, Body, tokenize = 'unicode61');
            """, cancellationToken);
    }

    public async Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var books = await db.Books.AsNoTracking().ToListAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM BookSearch;", cancellationToken);

        foreach (var book in books)
        {
            var body = await ReadBodyAsync(book.TextPath, cancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO BookSearch(BookId, Title, Author, Description, Body) VALUES ({0},{1},{2},{3},{4});",
                new object[] { book.Id.ToString(), book.Title, book.Author ?? string.Empty, book.Description ?? string.Empty, body },
                cancellationToken);
        }
        _logger.LogInformation("Rebuilt full-text index for {Count} books.", books.Count);
    }

    public async Task ReindexBookAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM BookSearch WHERE BookId = {0};", new object[] { bookId.ToString() }, cancellationToken);

        var book = await db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bookId, cancellationToken);
        if (book is null)
        {
            return;
        }

        var body = await ReadBodyAsync(book.TextPath, cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO BookSearch(BookId, Title, Author, Description, Body) VALUES ({0},{1},{2},{3},{4});",
            new object[] { book.Id.ToString(), book.Title, book.Author ?? string.Empty, book.Description ?? string.Empty, body },
            cancellationToken);
    }

    public async Task<PagedResult<SearchHit>> SearchAsync(string term, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        term = term?.Trim() ?? string.Empty;
        if (term.Length == 0)
        {
            return PagedResult<SearchHit>.Empty(page, pageSize);
        }

        var match = BuildMatchExpression(term);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        int total;
        await using (var countCmd = connection.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM BookSearch WHERE BookSearch MATCH $match;";
            countCmd.Parameters.AddWithValue("$match", match);
            total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(cancellationToken) ?? 0);
        }

        var hits = new List<SearchHit>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT BookSearch.BookId AS BookId,
                       b.Title AS Title,
                       b.Author AS Author,
                       snippet(BookSearch, 4, '[', ']', ' … ', 12) AS Snippet,
                       bm25(BookSearch) AS Rank
                FROM BookSearch
                LEFT JOIN Books b ON b.Id = BookSearch.BookId
                WHERE BookSearch MATCH $match
                ORDER BY Rank
                LIMIT $limit OFFSET $offset;
                """;
            cmd.Parameters.AddWithValue("$match", match);
            cmd.Parameters.AddWithValue("$limit", pageSize);
            cmd.Parameters.AddWithValue("$offset", (page - 1) * pageSize);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var idText = reader.GetString(0);
                hits.Add(new SearchHit
                {
                    BookId = Guid.TryParse(idText, out var id) ? id : Guid.Empty,
                    Title = reader.IsDBNull(1) ? "(unknown)" : reader.GetString(1),
                    Author = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Snippet = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Rank = reader.IsDBNull(4) ? 0 : reader.GetDouble(4)
                });
            }
        }

        return new PagedResult<SearchHit> { Items = hits, TotalCount = total, Page = page, PageSize = pageSize };
    }

    private async Task<string> ReadBodyAsync(string? relativeTextPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(relativeTextPath))
        {
            return string.Empty;
        }
        var path = _library.ToAbsolute(relativeTextPath);
        if (!File.Exists(path))
        {
            return string.Empty;
        }
        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to read indexed text {Path}.", path);
            return string.Empty;
        }
    }

    /// <summary>Builds a safe FTS5 MATCH expression: each token becomes a quoted prefix term, implicitly AND-ed.</summary>
    internal static string BuildMatchExpression(string term)
    {
        var tokens = term
            .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => "\"" + t.Replace("\"", "\"\"") + "\"*");
        var expression = string.Join(" ", tokens);
        return string.IsNullOrWhiteSpace(expression) ? "\"\"" : expression;
    }
}
