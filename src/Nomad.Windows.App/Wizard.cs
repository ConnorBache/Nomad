using Nomad.Core;

namespace Nomad.Windows.App;

internal sealed class Wizard : Form
{
    private readonly Panel page = new() { Dock = DockStyle.Fill, Padding = new Padding(24, 12, 24, 12) };
    private readonly Button back = new() { Text = "Back", AutoSize = true };
    private readonly Button next = new() { Text = "Next", AutoSize = true };
    private readonly Button cancel = new() { Text = "Cancel", AutoSize = true, Visible = false };
    private readonly List<string> extraPaths = [];
    private readonly List<string> exclusions = [];
    private readonly string[] profileRoots;
    private readonly string[] discoveryIssues;

    private CancellationTokenSource? activeOperation;
    private ComputerInventory? computer;
    private Inventory? inventory;
    private int step;

    public Wizard()
    {
        Text = "Nomad | Migration planning preview";
        MinimumSize = new Size(760, 550);
        Size = new Size(980, 660);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        (profileRoots, discoveryIssues) = WindowsDiscovery.ProfileSelection();

        var navigation = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(20, 6, 20, 8), AutoSize = true, WrapContents = false
        };
        navigation.Controls.AddRange([next, back, cancel]);
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.Controls.Add(page, 0, 0);
        shell.Controls.Add(navigation, 0, 1);
        Controls.Add(shell);

        AcceptButton = next;
        back.Click += (_, _) => ShowStep(step - 1);
        next.Click += async (_, _) => await AdvanceAsync();
        cancel.Click += (_, _) => activeOperation?.Cancel();
        FormClosing += (_, _) => activeOperation?.Cancel();
        ShowStep(0);
    }

    private void ShowStep(int index)
    {
        step = index;
        while (page.Controls.Count > 0)
            page.Controls[0].Dispose();

        string title;
        string description;
        Control? body;
        switch (step)
        {
            case 0:
                title = "Plan your move to Arch Linux";
                description = "Review your computer and choose the Windows files you want to keep. " +
                              "This preview scans file information and exports a plan. A future migration requires a verified backup on a separate device.";
                body = null;
                break;
            case 1:
                title = "Computer check";
                description = "Review the hardware and storage details Windows provided. Compatibility testing is pending.";
                body = HardwareView(computer!);
                break;
            case 2:
                title = "Choose what to keep";
                description = "Your Windows profile and discovered Windows folders are selected by default. " +
                              "You can add files, folders, or drive roots, and explicitly exclude items.";
                body = SelectionView();
                break;
            case 3:
                title = "Windows applications";
                description = "These names come from Windows' installed-programs list. Selected program folders will be archived as files. The preview reports possible Linux equivalents.";
                body = ApplicationView(computer!.Hardware?.InstalledApps ?? []);
                break;
            default:
                title = "Review your plan";
                description = "Target: Arch Linux with KDE Plasma. The export includes original Windows paths, " +
                              "proposed archive paths, and hardware details. Keep it private.";
                body = ReviewView(inventory!);
                break;
        }

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Text = title, AutoSize = true, Font = new Font(Font.FontFamily, 20, FontStyle.Bold),
            Margin = new Padding(0, 8, 0, 12)
        }, 0, 0);
        var intro = new Label { Text = description, AutoSize = true, Margin = new Padding(0, 0, 0, 16) };
        intro.MaximumSize = new Size(Math.Max(300, page.ClientSize.Width - page.Padding.Horizontal - 8), 0);
        layout.SizeChanged += (_, _) => intro.MaximumSize = new Size(Math.Max(300, layout.ClientSize.Width - 8), 0);
        layout.Controls.Add(intro, 0, 1);
        if (body is not null)
            layout.Controls.Add(body, 0, 2);
        page.Controls.Add(layout);

        back.Enabled = step > 0;
        next.Text = step switch { 0 => "Check this computer", 3 => "Scan selected files", 4 => "Export plan", _ => "Next" };
    }

    private async Task AdvanceAsync()
    {
        switch (step)
        {
            case 0:
                await RunOperationAsync("Checking computer…", async token =>
                {
                    computer = await WindowsDiscovery.InspectAsync(token);
                    token.ThrowIfCancellationRequested();
                    ShowStep(1);
                });
                break;
            case 1:
                ShowStep(2);
                break;
            case 2:
                ShowStep(3);
                break;
            case 3:
                await RunOperationAsync("Scanning files…", async token =>
                {
                    string[] roots = profileRoots.Concat(extraPaths).ToArray();
                    string[] omitted = exclusions.ToArray();
                    var ids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (VolumeInfo volume in computer!.Hardware?.Volumes ?? [])
                    {
                        if (volume.DriveLetter is not null && volume.VolumeId is not null)
                            ids.TryAdd(volume.DriveLetter, volume.VolumeId);
                    }
                    inventory = await Task.Run(() => InventoryScanner.Scan(roots, omitted, ids, discoveryIssues, token), token);
                    token.ThrowIfCancellationRequested();
                    ShowStep(4);
                });
                break;
            case 4:
                Export();
                break;
        }
    }

    private async Task RunOperationAsync(string label, Func<CancellationToken, Task> operation)
    {
        using var source = new CancellationTokenSource();
        activeOperation = source;
        string previousLabel = next.Text;
        next.Text = label;
        next.Enabled = back.Enabled = page.Enabled = false;
        cancel.Visible = true;
        try
        {
            await operation(source.Token);
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            // The current wizard page is still usable after cancellation.
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (!IsDisposed)
                MessageBox.Show(this, "Windows could not finish this step. Review your selections and try again.",
                    "Nomad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            activeOperation = null;
            if (!IsDisposed)
            {
                if (next.Text == label)
                    next.Text = previousLabel;
                next.Enabled = page.Enabled = true;
                back.Enabled = step > 0;
                cancel.Visible = false;
            }
        }
    }

    private static Control HardwareView(ComputerInventory details)
    {
        HardwareDetails? hardware = details.Hardware;
        var lines = new List<string>
        {
            $"Windows: {hardware?.Edition ?? details.WindowsVersion}, build {details.WindowsBuild}",
            $"Architecture: {details.Architecture}",
            $"Firmware: {details.Firmware}",
            $"Secure Boot: {details.SecureBoot?.ToString() ?? "Unknown"}",
            $"Windows volume: {details.WindowsDrive ?? "Unknown"}",
            $"Windows volume encryption: {hardware?.WindowsEncryption ?? "Unknown"}",
            $"Memory: {FormatBytes(hardware?.MemoryBytes)}",
            $"Processor: {Describe(hardware?.Processors)}",
            $"Graphics: {Describe(hardware?.Graphics)}",
            $"Storage controllers: {Describe(hardware?.StorageControllers)}",
            $"Network adapters: {Describe(hardware?.NetworkAdapters)}",
            "", "Disks and volumes"
        };
        foreach (DiskInfo disk in hardware?.Disks ?? [])
            lines.Add($"Disk {disk.Number}: {disk.Model}, {FormatBytes(disk.Size)}, {disk.PartitionStyle}, {disk.HealthStatus}");
        foreach (PartitionInfo partition in hardware?.Partitions ?? [])
            lines.Add($"Disk {partition.DiskNumber}, partition {partition.PartitionNumber}: {partition.DriveLetter ?? "No drive letter"}, {FormatBytes(partition.Size)}, {partition.GptType}");
        foreach (VolumeInfo volume in hardware?.Volumes ?? [])
            lines.Add($"{volume.DriveLetter ?? "No drive letter"} {volume.Label}: {volume.FileSystem}, {FormatBytes(volume.FreeBytes)} free");
        lines.AddRange(details.Issues.Select(issue => $"\r\n{issue}"));
        return new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Text = string.Join("\r\n", lines), AccessibleName = "Computer inventory" };
    }

    private Control SelectionView()
    {
        var list = new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
            MultiSelect = false, AccessibleName = "Selected Windows paths"
        };
        list.Columns.Add("Selection", 145);
        list.Columns.Add("Windows path", 550);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        var remove = new Button { Text = "Remove selected", AutoSize = true, Enabled = false };
        AddButton("Add file", () => PickFile(extraPaths));
        AddButton("Add folder or drive", () => PickFolder(extraPaths));
        AddButton("Exclude file", () => PickFile(exclusions));
        AddButton("Exclude folder", () => PickFolder(exclusions));
        actions.Controls.Add(remove);
        list.SelectedIndexChanged += (_, _) => remove.Enabled = list.SelectedItems.Count == 1 && list.SelectedItems[0].Tag is List<string>;
        remove.Click += (_, _) =>
        {
            if (list.SelectedItems.Count == 1 && list.SelectedItems[0].Tag is List<string> paths)
            {
                paths.Remove(list.SelectedItems[0].SubItems[1].Text);
                RefreshRows();
            }
        };

        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(actions, 0, 0);
        panel.Controls.Add(list, 0, 1);
        RefreshRows();
        return panel;

        void AddButton(string text, Action action)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += (_, _) => { action(); RefreshRows(); };
            actions.Controls.Add(button);
        }

        void RefreshRows()
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (string root in profileRoots)
                list.Items.Add(new ListViewItem(["Default", root]));
            foreach (string path in extraPaths)
                list.Items.Add(new ListViewItem(["Additional", path]) { Tag = extraPaths });
            foreach (string path in exclusions)
                list.Items.Add(new ListViewItem(["Excluded", path]) { Tag = exclusions });
            list.EndUpdate();
        }
    }

    private static Control ReviewView(Inventory result)
    {
        int blocked = result.Entries.Count(entry => entry.Status == "blocked");
        int excluded = result.Entries.Count(entry => entry.Status == "excluded");
        int files = result.Entries.Count(entry => entry.Kind == "file" && entry.Status == "discovered");
        decimal bytes = result.Entries.Where(entry => entry.Status == "discovered").Sum(entry => (decimal)(entry.SizeBytes ?? 0));
        var summary = new Label
        {
            AutoSize = true, Text = $"{files:N0} files discovered ({FormatBytes(bytes)}) · {blocked:N0} blocked entries · {excluded:N0} exclusions\n" +
                                    string.Join("\n", result.DiscoveryIssues), Margin = new Padding(0, 0, 0, 10)
        };
        InventoryEntry[] rows = result.Entries.OrderBy(entry => entry.Status != "blocked").ToArray();
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, VirtualMode = true, AllowUserToAddRows = false,
            AllowUserToDeleteRows = false, RowHeadersVisible = false, AccessibleName = "Scanned Windows files"
        };
        grid.Columns.Add("status", "Status");
        grid.Columns.Add("path", "Windows path");
        grid.Columns.Add("note", "Details");
        grid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        grid.Columns[1].FillWeight = 65;
        grid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        grid.Columns[2].FillWeight = 35;
        foreach (DataGridViewColumn column in grid.Columns)
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
        grid.CellValueNeeded += (_, eventArgs) =>
        {
            InventoryEntry entry = rows[eventArgs.RowIndex];
            eventArgs.Value = eventArgs.ColumnIndex switch { 0 => entry.Status, 1 => entry.WindowsPath, _ => entry.Note };
        };
        grid.RowCount = rows.Length;

        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(summary, 0, 0);
        panel.Controls.Add(grid, 0, 1);
        return panel;
    }

    private static Control ApplicationView(string[] apps)
    {
        if (apps.Length == 0)
            return new Label { Dock = DockStyle.Top, Text = "Windows provided no application names.", AutoSize = true };

        var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, AccessibleName = "Windows applications and Linux equivalents" };
        list.Columns.Add("Windows application", 300);
        list.Columns.Add("Possible Linux equivalent", 330);
        foreach (string app in apps)
        {
            string candidate = app switch
            {
                "Google Chrome" or "Microsoft Edge" => "Chromium",
                "Steam" => "Steam",
                _ when app.StartsWith("Mozilla Firefox", StringComparison.Ordinal) => "Firefox",
                _ when app.StartsWith("Microsoft Visual Studio Code", StringComparison.Ordinal) => "Code - OSS",
                _ when app.StartsWith("Microsoft Office", StringComparison.Ordinal) || app.StartsWith("LibreOffice", StringComparison.Ordinal) => "LibreOffice",
                _ => "—"
            };
            list.Items.Add(new ListViewItem([app, candidate]));
        }
        return list;
    }

    private void PickFile(List<string> paths)
    {
        using var dialog = new OpenFileDialog { CheckFileExists = true, Multiselect = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            foreach (string path in dialog.FileNames)
                AddPath(paths, path);
    }

    private void PickFolder(List<string> paths)
    {
        using var dialog = new FolderBrowserDialog { ShowNewFolderButton = false };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            AddPath(paths, dialog.SelectedPath);
    }

    private void AddPath(List<string> paths, string path)
    {
        try
        {
            string normalized = WindowsPath.Normalize(path);
            if (!paths.Contains(normalized, StringComparer.Ordinal))
                paths.Add(normalized);
        }
        catch (ArgumentException error)
        {
            MessageBox.Show(this, error.Message, "Nomad", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void Export()
    {
        using var dialog = new SaveFileDialog
        {
            FileName = "migration.nomad-plan.json", Filter = "Nomad plan (*.nomad-plan.json)|*.nomad-plan.json",
            DefaultExt = "nomad-plan.json", OverwritePrompt = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            byte[] bytes = PlanExport.Serialize(computer!, inventory!);
            using (var file = new FileStream(dialog.FileName, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                file.Write(bytes);
            MessageBox.Show(this, "The migration plan was saved.", "Nomad", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "Nomad could not save the plan. Choose a new file name in a folder you can write to.",
                "Nomad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string Describe(string[]? items) => items is { Length: > 0 } ? string.Join(", ", items) : "Unknown";

    private static string FormatBytes(decimal? bytes) => bytes is null ? "Unknown" : $"{bytes.Value / 1_073_741_824:N2} GiB";
}
