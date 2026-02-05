namespace RoboUI;

public sealed class JobPreset
{
    public string Name { get; set; } = "New Job";
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";

    public bool CopySubdirsE { get; set; } = true;
    public bool MirrorMir { get; set; } = false;
    public bool DryRunL { get; set; } = false;

    public int RetriesR { get; set; } = 1;
    public int WaitW { get; set; } = 1;
    public int ThreadsMt { get; set; } = 8;

    public bool NoProgressNp { get; set; } = true;
    public bool Tee { get; set; } = true;

    public DateTimeOffset? LastRun { get; set; }

    public override string ToString()
    {
        // What shows in the ListBox
        var lr = LastRun.HasValue ? $"  (Last: {LastRun.Value:yyyy-MM-dd HH:mm})" : "";
        return $"{Name}{lr}";
    }
}
