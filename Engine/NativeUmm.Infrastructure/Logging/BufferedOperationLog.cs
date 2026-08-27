using NativeUmm.Application.Abstractions;

namespace NativeUmm.Infrastructure.Logging;

public sealed class BufferedOperationLog : IOperationLog
{
    private readonly AsyncLocal<List<OperationLogLine>?> current = new();

    public void Begin() => current.Value = [];
    public void Info(string message) => current.Value?.Add(new OperationLogLine("info", message));
    public void Warn(string message) => current.Value?.Add(new OperationLogLine("warn", message));
    public void Fail(string message) => current.Value?.Add(new OperationLogLine("error", message));
    public IReadOnlyList<OperationLogLine> Collect() => current.Value ?? [];
}
