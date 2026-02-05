using System.IO;
using System.Text.Json;

namespace RoboUI;

public static class JobStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static string GetDefaultPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RoboUI"
        );
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "jobs.json");
    }

    public static List<JobPreset> Load(string path)
    {
        if (!File.Exists(path))
            return new List<JobPreset>();

        var json = File.ReadAllText(path);
        var jobs = JsonSerializer.Deserialize<List<JobPreset>>(json, Options);
        return jobs ?? new List<JobPreset>();
    }

    public static void Save(string path, List<JobPreset> jobs)
    {
        var json = JsonSerializer.Serialize(jobs, Options);
        File.WriteAllText(path, json);
    }
}
