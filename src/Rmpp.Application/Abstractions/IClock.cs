namespace Rmpp.Application.Abstractions;

/// <summary>为会话、历史合并和任务时间提供可测试时钟。</summary>
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
