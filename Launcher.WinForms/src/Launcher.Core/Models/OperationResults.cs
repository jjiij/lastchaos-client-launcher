namespace Launcher.Core.Models;

public sealed record UpdateOperationResult(bool Success, string Message, string? Version = null);
public sealed record RepairOperationResult(bool Success, string Message, int CheckedFiles = 0, int RepairedFiles = 0);
public sealed record SelfUpdateResult(bool Success, string Message, string? StagedExecutable = null);
