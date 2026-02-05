using System.Diagnostics;

namespace RoboUI;

public sealed class RobocopyRunner
{
    private Process? _proc;

    public async Task<int> RunAsync(string robocopyArgs, Action<string> onLine, CancellationToken ct)
    {
        // robocopy.exe is in System32, so it’s normally on PATH.
        var psi = new ProcessStartInfo
        {
            FileName = "robocopy",
            Arguments = robocopyArgs,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        // Start
        if (!_proc.Start())
            throw new InvalidOperationException("Failed to start robocopy process.");

        // Read streams concurrently
        var stdoutTask = Task.Run(async () =>
        {
            while (!_proc.HasExited && !ct.IsCancellationRequested)
            {
                var line = await _proc.StandardOutput.ReadLineAsync();
                if (line is null) break;
                onLine(line);
            }
        }, ct);

        var stderrTask = Task.Run(async () =>
        {
            while (!_proc.HasExited && !ct.IsCancellationRequested)
            {
                var line = await _proc.StandardError.ReadLineAsync();
                if (line is null) break;
                onLine("[ERR] " + line);
            }
        }, ct);

        // Wait for exit
        await _proc.WaitForExitAsync(ct);

        // Ensure readers finish
        try { await Task.WhenAll(stdoutTask, stderrTask); } catch { /* ignore */ }

        return _proc.ExitCode;
    }

    public void Kill()
    {
        try
        {
            if (_proc is { HasExited: false })
                _proc.Kill(entireProcessTree: true);
        }
        catch { /* ignore */ }
    }

    // Robocopy exit codes are bitfields; “success” can be 0..7.
    public static string DescribeExitCode(int code)
    {
        if (code < 0) return "Unknown";

        var parts = new List<string>();

        if ((code & 1) != 0) parts.Add("Files copied");
        if ((code & 2) != 0) parts.Add("Extra files/dirs detected");
        if ((code & 4) != 0) parts.Add("Mismatched files/dirs detected");
        if ((code & 8) != 0) parts.Add("Some files could not be copied (errors)");
        if ((code & 16) != 0) parts.Add("Serious error");
        if (parts.Count == 0) parts.Add("No changes / no errors");

        var verdict = code <= 7 ? "OK" : "Problem";
        return $"{verdict}: {string.Join(", ", parts)}";
    }
}
