using System.Text.Json;

namespace Starter.Module.AI.Infrastructure.Eval.Baseline;

public static class BaselineWriter
{
    public static void Write(string path, BaselineSnapshot snapshot)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, BaselineJson.Options));
    }
}
