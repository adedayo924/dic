namespace DIC.ELibrary.Core.Abstractions;

/// <summary>Abstraction over the system clock to keep time deterministic in tests.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset LocalNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public DateTimeOffset LocalNow => DateTimeOffset.Now;
}
