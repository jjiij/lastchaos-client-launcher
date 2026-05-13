using Launcher.Core.Enums;

namespace Launcher.Core.Models;

public interface IProgressSnapshot
{
    UpdateState State { get; }
    int Percent { get; }
    string StatusText { get; }
    long BytesTotal { get; }
    long BytesTransferred { get; }
    double SpeedBytesPerSecond { get; }
}

public sealed class ProgressSnapshot : IProgressSnapshot
{
    public UpdateState State { get; init; }
    public int Percent { get; init; }
    public string StatusText { get; init; } = string.Empty;
    public long BytesTotal { get; init; }
    public long BytesTransferred { get; init; }
    public double SpeedBytesPerSecond { get; init; }
}
