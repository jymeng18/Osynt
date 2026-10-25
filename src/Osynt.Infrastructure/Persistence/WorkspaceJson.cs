using System.Text.Json;

namespace Osynt.Infrastructure.Persistence;

public static class WorkspaceJson
{
    public static JsonSerializerOptions Options { get; } = new() { WriteIndented = true };
}
