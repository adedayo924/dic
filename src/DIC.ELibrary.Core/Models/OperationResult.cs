namespace DIC.ELibrary.Core.Models;

/// <summary>Result of an operation that can fail without throwing.</summary>
public class OperationResult
{
    public bool Succeeded { get; protected init; }
    public string? Error { get; protected init; }

    public static OperationResult Ok() => new() { Succeeded = true };
    public static OperationResult Fail(string error) => new() { Succeeded = false, Error = error };
}

public class OperationResult<T> : OperationResult
{
    public T? Value { get; private init; }

    public static OperationResult<T> Ok(T value) => new() { Succeeded = true, Value = value };
    public static new OperationResult<T> Fail(string error) => new() { Succeeded = false, Error = error };
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Empty(int page, int pageSize) =>
        new() { Items = Array.Empty<T>(), TotalCount = 0, Page = page, PageSize = pageSize };
}
