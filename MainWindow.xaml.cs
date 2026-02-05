using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Clipboard = System.Windows.Clipboard;


namespace RoboUI;

public partial class MainWindow : Window
{
    private readonly ConcurrentQueue<string> _pendingLines = new();
    private readonly DispatcherTimer _flushTimer;

    private RobocopyRunner? _runner;
    private CancellationTokenSource? _cts;

    private string _lastCommandLine = "";

    private readonly ObservableCollection<JobPreset> _jobs = new();
    private readonly string _jobsPath = JobStore.GetDefaultPath();

    public MainWindow()
    {
        InitializeComponent();
        
        LoadJobsIntoList();

        _flushTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _flushTimer.Tick += (_, _) => FlushOutputToUi();
        _flushTimer.Start();
    }

    private void LoadJobsIntoList()
    {
        _jobs.Clear();
        foreach (var j in JobStore.Load(_jobsPath).OrderBy(x => x.Name))
            _jobs.Add(j);

        JobsListBox.ItemsSource = _jobs;
    }

    private void DeleteJob_Click(object sender, RoutedEventArgs e)
    {
        if (JobsListBox.SelectedItem is not JobPreset selected)
        {
            System.Windows.MessageBox.Show("Select a job to delete.", "No selection",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = System.Windows.MessageBox.Show(
            $"Delete job \"{selected.Name}\"?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        _jobs.Remove(selected);
        JobStore.Save(_jobsPath, _jobs.ToList());

        JobsListBox.ItemsSource = null;
        JobsListBox.ItemsSource = _jobs;

        StatusTextBlock.Text = $"Deleted job: {selected.Name}";
    }

    private void ReloadJobs_Click(object sender, RoutedEventArgs e)
    {
        LoadJobsIntoList();
        StatusTextBlock.Text = "Jobs reloaded.";
    }

    private void SaveJob_Click(object sender, RoutedEventArgs e)
    {
        var name = Microsoft.VisualBasic.Interaction.InputBox(
            "Job name:",
            "Save Job",
            "My Backup Job"
        );

        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        var job = CaptureJobFromUi(name);

        // Replace if same name exists
        var existing = _jobs.FirstOrDefault(j => string.Equals(j.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            var idx = _jobs.IndexOf(existing);
            _jobs[idx] = job;
        }
        else
        {
            _jobs.Add(job);
        }

        // Persist
        JobStore.Save(_jobsPath, _jobs.ToList());

        // Refresh display text (ToString)
        JobsListBox.ItemsSource = null;
        JobsListBox.ItemsSource = _jobs;

        StatusTextBlock.Text = "Job saved.";
    }
    private static long ParseByteSize(string text)
    {
        // Handles "913.12", "m", etc.
        // We assume format like: number + unit letter (m, k, g)

        text = text.Trim().ToLower();

        double value = 0;
        char unit = 'b';

        foreach (var c in text)
        {
            if (char.IsLetter(c))
            {
                unit = c;
                break;
            }
        }

        var num = new string(text.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        double.TryParse(num, out value);

        return unit switch
        {
            'k' => (long)(value * 1024),
            'm' => (long)(value * 1024 * 1024),
            'g' => (long)(value * 1024 * 1024 * 1024),
            _ => (long)value
        };
    }

    private void JobsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (JobsListBox.SelectedItem is JobPreset job)
            ApplyJobToUi(job);
    }

    private long _totalBytes = 0;
    private long _copiedBytes = 0;

    private void UpdateSummaryFromLine(string line)
    {
        var t = line.Trim();

        if (t.StartsWith("Dirs :"))
        {
            if (TryParseRoboRow(t, out var total, out var copied, out var failed))
                Dispatcher.Invoke(() => DirsSummaryText.Text = $"Copied {copied} / {total}   Failed {failed}");
        }
        else if (t.StartsWith("Files :"))
        {
            if (TryParseRoboRow(t, out var total, out var copied, out var failed))
                Dispatcher.Invoke(() => FilesSummaryText.Text = $"Copied {copied} / {total}   Failed {failed}");
        }
        else if (t.StartsWith("Bytes :"))
        {
            // Bytes row can contain units, so we just display "Copied X of Y" as raw tokens.
            if (TryParseRoboRowTokens(t, out var totalTok, out var copiedTok))
                Dispatcher.Invoke(() => BytesSummaryText.Text = $"Copied {copiedTok} / {totalTok}");
        }
        else if (t.StartsWith("Times :"))
        {
            // Times row formatting varies; simplest: show the total time token right after "Times :"
            var tokens = SplitTokens(t);
            // tokens[0]="Times", tokens[1]=":", tokens[2]=<total-time>
            var totalTime = tokens.Count >= 3 ? tokens[2] : "-";
            Dispatcher.Invoke(() => TimeSummaryText.Text = totalTime);
        }
    }

    private static bool TryParseRoboRow(string trimmedLine, out long total, out long copied, out long failed)
    {
        total = copied = failed = 0;

        var tokens = SplitTokens(trimmedLine);

        if (tokens.Count < 8) return false;

        return long.TryParse(tokens[2], out total)
            && long.TryParse(tokens[3], out copied)
            && long.TryParse(tokens[6], out failed);
    }

    private static bool TryParseRoboRowTokens(string trimmedLine, out string totalToken, out string copiedToken)
    {
        totalToken = copiedToken = "-";

        var tokens = SplitTokens(trimmedLine);
        if (tokens.Count < 6) return false;

        totalToken = $"{tokens[2]} {tokens[3]}";
        copiedToken = $"{tokens[4]} {tokens[5]}";
        return true;
    }

    private static List<string> SplitTokens(string s)
    {
        return s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private JobPreset CaptureJobFromUi(string name)
    {
        return new JobPreset
        {
            Name = name,
            Source = SourceTextBox.Text.Trim(),
            Destination = DestTextBox.Text.Trim(),

            CopySubdirsE = CopySubdirsCheck.IsChecked == true,
            MirrorMir = MirrorCheck.IsChecked == true,
            DryRunL = DryRunCheck.IsChecked == true,

            RetriesR = ParseIntOrDefault(RetriesTextBox.Text, 1),
            WaitW = ParseIntOrDefault(WaitTextBox.Text, 1),
            ThreadsMt = ParseIntOrDefault(MtTextBox.Text, 8),

            NoProgressNp = NoProgressCheck.IsChecked == true,
            Tee = TeeCheck.IsChecked == true
        };
    }

    private void ApplyJobToUi(JobPreset job)
    {
        SourceTextBox.Text = job.Source;
        DestTextBox.Text = job.Destination;

        CopySubdirsCheck.IsChecked = job.CopySubdirsE;
        MirrorCheck.IsChecked = job.MirrorMir;
        DryRunCheck.IsChecked = job.DryRunL;

        RetriesTextBox.Text = job.RetriesR.ToString();
        WaitTextBox.Text = job.WaitW.ToString();
        MtTextBox.Text = job.ThreadsMt.ToString();

        NoProgressCheck.IsChecked = job.NoProgressNp;
        TeeCheck.IsChecked = job.Tee;

        StatusTextBlock.Text = $"Loaded job: {job.Name}";
    }

    private void BrowseSource_Click(object sender, RoutedEventArgs e)
        => SourceTextBox.Text = PickFolder(SourceTextBox.Text);

    private void BrowseDest_Click(object sender, RoutedEventArgs e)
        => DestTextBox.Text = PickFolder(DestTextBox.Text);

    private static string PickFolder(string initialPath)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = false,
            CheckPathExists = true,
            FileName = "Select folder",
        };

        // If the user typed a valid folder, start there.
        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
            dialog.InitialDirectory = initialPath;

        if (dialog.ShowDialog() != true)
            return initialPath; // user cancelled, keep what they had

        return Path.GetDirectoryName(dialog.FileName) ?? initialPath;
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        ActivityBar.Value = 0;
        _totalBytes = 0;
        _copiedBytes = 0;
        var src = SourceTextBox.Text.Trim();
        var dst = DestTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(dst))
        {
            System.Windows.MessageBox.Show("Please choose both Source and Destination folders.", "Missing info",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MirrorCheck.IsChecked == true)
        {
            var result = System.Windows.MessageBox.Show(
                "Mirror (/MIR) can delete files in the destination that are not in the source.\n\nProceed?",
                "Danger: /MIR",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;
        }

        OutputTextBox.Clear();
        EnqueueLine($"Started: {DateTime.Now}");
        EnqueueLine("");

        var args = BuildRobocopyArgs(src, dst);
        _lastCommandLine = $"robocopy {args}";
        EnqueueLine(_lastCommandLine);
        EnqueueLine("");

        _cts = new CancellationTokenSource();
        _runner = new RobocopyRunner();

        SetRunningUi(true);

        _ = Task.Run(async () =>
        {
            try
            {
                var exit = await _runner.RunAsync(args, EnqueueLine, _cts.Token);

                Dispatcher.Invoke(() =>
                {
                    if (JobsListBox.SelectedItem is JobPreset selected)
                    {
                        selected.LastRun = DateTimeOffset.Now;
                        JobStore.Save(_jobsPath, _jobs.ToList());

                        // refresh visible text
                        JobsListBox.ItemsSource = null;
                        JobsListBox.ItemsSource = _jobs;
                    }
                });

                EnqueueLine("");
                EnqueueLine($"Finished: {DateTime.Now}");
                EnqueueLine($"Robocopy exit code: {exit} ({RobocopyRunner.DescribeExitCode(exit)})");
            }
            catch (OperationCanceledException)
            {
                EnqueueLine("");
                EnqueueLine("Canceled.");
            }
            catch (Exception ex)
            {
                EnqueueLine("");
                EnqueueLine("ERROR:");
                EnqueueLine(ex.ToString());
            }
            finally
            {
                Dispatcher.Invoke(() => SetRunningUi(false));
            }
        });
    }

    private void BrowseLog_Click(object sender, RoutedEventArgs e)
    {
        // WPF file save dialog
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Log files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = "robocopy-log.txt",
            OverwritePrompt = AppendLogCheck.IsChecked != true
        };

        if (dlg.ShowDialog() == true)
            LogPathTextBox.Text = dlg.FileName;
    }

    private void LogToFileCheck_Toggled(object sender, RoutedEventArgs e)
    {
        var enabled = LogToFileCheck.IsChecked == true;
        LogPathTextBox.IsEnabled = enabled;
        BrowseLogButton.IsEnabled = enabled;
        AppendLogCheck.IsEnabled = enabled;

        if (enabled && string.IsNullOrWhiteSpace(LogPathTextBox.Text))
        {
            // sensible default
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "RoboUI Logs");
            Directory.CreateDirectory(dir);
            LogPathTextBox.Text = Path.Combine(dir, "robocopy-log.txt");
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelButton.IsEnabled = false;
        StatusTextBlock.Text = "Canceling...";
        _cts?.Cancel();
        _runner?.Kill();
    }

    private void CopyCommand_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastCommandLine))
        {
            System.Windows.MessageBox.Show("No command has been built yet. Click Run first.", "Nothing to copy",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Clipboard.SetText(_lastCommandLine);
        StatusTextBlock.Text = "Command copied to clipboard.";
    }

    private void SetRunningUi(bool running)
    {
        RunButton.IsEnabled = !running;
        CancelButton.IsEnabled = running;
        StatusTextBlock.Text = running ? "Running..." : "Ready.";
        ActivityBar.Visibility = running ?  Visibility.Visible : Visibility.Collapsed;
    }

    private string BuildRobocopyArgs(string src, string dst)
    {
        // Keep Robocopy in control: we build args, but it does the work.
        var sb = new StringBuilder();

        sb.Append('"').Append(src).Append("\" ");
        sb.Append('"').Append(dst).Append("\" ");

        // Mode switches
        if (MirrorCheck.IsChecked == true)
            sb.Append("/MIR ");
        else if (CopySubdirsCheck.IsChecked == true)
            sb.Append("/E ");

        if (DryRunCheck.IsChecked == true)
            sb.Append("/L ");

        // Retry behavior
        var r = ParseIntOrDefault(RetriesTextBox.Text, 1);
        var w = ParseIntOrDefault(WaitTextBox.Text, 1);
        sb.Append($"/R:{r} /W:{w} ");

        // Threads
        var mt = ParseIntOrDefault(MtTextBox.Text, 8);
        if (mt < 1) mt = 1;
        if (mt > 128) mt = 128; // robocopy allows higher in some versions; 128 is a sane cap.
        sb.Append($"/MT:{mt} ");

        // Output behavior
        if (NoProgressCheck.IsChecked == true)
            sb.Append("/NP ");

        // Good defaults for a GUI/log viewer
        sb.Append("/NFL /NDL "); // don't spam file/dir names (big speed gain for UI/log parsing)
        // If you want full lists later, expose toggles and remove these.

        if (TeeCheck.IsChecked == true)
            sb.Append("/TEE ");

        sb.Append("/FFT "); // tolerate minor timestamp granularity differences (common across filesystems)

        if (LogToFileCheck.IsChecked == true)
        {
            var path = (LogPathTextBox.Text ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(path))
            {
                var logSwitch = AppendLogCheck.IsChecked == true ? "/LOG+:" : "/LOG:";
                sb.Append($"{logSwitch}\"{path}\" ");
            }
        }

        return sb.ToString().Trim();
    }

    private static int ParseIntOrDefault(string text, int fallback)
        => int.TryParse(text.Trim(), out var v) ? v : fallback;

    private void EnqueueLine(string line)
    {
        _pendingLines.Enqueue(line);
        UpdateSummaryFromLine(line);
    }

    private void FlushOutputToUi()
    {
        // Throttle flush: pull a chunk per tick to avoid UI churn.
        const int maxLinesPerTick = 200;
        int count = 0;

        var sb = new StringBuilder();

        while (count < maxLinesPerTick && _pendingLines.TryDequeue(out var line))
        {
            sb.AppendLine(line);
            count++;
        }

        if (sb.Length > 0)
        {
            OutputTextBox.AppendText(sb.ToString());
            OutputTextBox.ScrollToEnd();
        }
    }

    private void OutputTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {

    }
}
