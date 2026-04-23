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

    public static void Update(string path, string datasetName, BaselineDatasetSnapshot snapshot)
    {
        BaselineSnapshot? existing = null;
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            existing = System.Text.Json.JsonSerializer.Deserialize<BaselineSnapshot>(json, BaselineJson.Options);
        }
        var datasets = existing?.Datasets is not null
            ? new Dictionary<string, BaselineDatasetSnapshot>(existing.Datasets)
            : new Dictionary<string, BaselineDatasetSnapshot>();
        datasets[datasetName] = snapshot;
        Write(path, new BaselineSnapshot(
            DateTime.UtcNow,
            Environment.GetEnvironmentVariable("GIT_SHA"),
            datasets));
    }
}
