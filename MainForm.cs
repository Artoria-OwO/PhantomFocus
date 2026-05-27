using System.ComponentModel;

namespace PhantomFocus;

public sealed class MainForm : Form
{
    private readonly ListView _list;
    private readonly Button _refreshBtn;
    private readonly Button _startBtn;
    private readonly Button _stopBtn;
    private readonly RadioButton _modeFake;
    private readonly RadioButton _modeForce;
    private readonly Label _statusLabel;
    private readonly TextBox _logBox;
    private readonly FocusKeeper _keeper = new();

    public MainForm()
    {
        Text = "PhantomFocus";
        Width = 880;
        Height = 580;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 480);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);

        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath))
        {
            try { Icon = Icon.ExtractAssociatedIcon(exePath); } catch { /* no icon — fine */ }
        }

        _list = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            GridLines = false,
            Dock = DockStyle.Fill
        };
        _list.Columns.Add("Window Title", 520);
        _list.Columns.Add("Process", 160);
        _list.Columns.Add("PID", 70);
        _list.DoubleClick += (_, _) => StartIfReady();

        _refreshBtn = new Button { Text = "Refresh", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(90, 28), Padding = new Padding(8, 0, 8, 0) };
        _refreshBtn.Click += (_, _) => RefreshWindows();

        _modeFake = new RadioButton { Text = "Fake Focus (recommended for AFK)", Checked = true, AutoSize = true, Margin = new Padding(0, 6, 12, 0) };
        _modeForce = new RadioButton { Text = "Force Foreground (snap back)", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };

        _startBtn = new Button { Text = "Start Keeping Focus", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(170, 30), Padding = new Padding(12, 0, 12, 0) };
        _startBtn.Click += (_, _) => StartIfReady();
        _stopBtn = new Button { Text = "Stop", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(80, 30), Padding = new Padding(12, 0, 12, 0), Enabled = false, Margin = new Padding(8, 3, 3, 3) };
        _stopBtn.Click += (_, _) => StopKeeping();

        _statusLabel = new Label
        {
            Text = "Idle.",
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(12, 0, 0, 0)
        };
        _logBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font(FontFamily.GenericMonospace, 8.5f),
            BackColor = SystemColors.ControlLightLight
        };

        var topBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8, 8, 8, 6),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        topBar.Controls.Add(_refreshBtn);
        topBar.Controls.Add(new Label { Text = "    Mode:", AutoSize = true, Margin = new Padding(8, 8, 4, 0) });
        topBar.Controls.Add(_modeFake);
        topBar.Controls.Add(_modeForce);

        var bottomBar = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(8, 6, 8, 8)
        };
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomBar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottomBar.Controls.Add(_startBtn, 0, 0);
        bottomBar.Controls.Add(_stopBtn, 1, 0);
        bottomBar.Controls.Add(_statusLabel, 2, 0);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            FixedPanel = FixedPanel.Panel2
        };
        split.Panel1.Controls.Add(_list);
        split.Panel2.Controls.Add(_logBox);

        Controls.Add(split);
        Controls.Add(bottomBar);
        Controls.Add(topBar);

        Shown += (_, _) =>
        {
            if (split.Height > 160) split.SplitterDistance = split.Height - 130;
        };

        _keeper.Log += AppendLog;

        Load += (_, _) => RefreshWindows();
        FormClosing += OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _keeper.Dispose();
    }

    private void RefreshWindows()
    {
        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            foreach (var w in WindowEnumerator.EnumerateUserWindows())
            {
                var item = new ListViewItem(new[]
                {
                    w.Title,
                    w.ProcessName,
                    w.ProcessId.ToString()
                })
                {
                    Tag = w
                };
                _list.Items.Add(item);
            }
        }
        finally
        {
            _list.EndUpdate();
        }
        AppendLog($"Listed {_list.Items.Count} windows.");
    }

    private void StartIfReady()
    {
        if (_list.SelectedItems.Count == 0)
        {
            MessageBox.Show(this, "Pick a window from the list first.", "No selection",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var info = (WindowInfo)_list.SelectedItems[0].Tag!;
        var mode = _modeFake.Checked ? FocusMode.FakeFocus : FocusMode.ForceForeground;

        _keeper.Start(info.Handle, mode);

        if (_keeper.IsRunning)
        {
            _startBtn.Enabled = false;
            _stopBtn.Enabled = true;
            _refreshBtn.Enabled = false;
            _modeFake.Enabled = false;
            _modeForce.Enabled = false;
            _list.Enabled = false;
            _statusLabel.Text = $"Active — {mode} on \"{info.Title}\"";
        }
    }

    private void StopKeeping()
    {
        _keeper.Stop();
        _startBtn.Enabled = true;
        _stopBtn.Enabled = false;
        _refreshBtn.Enabled = true;
        _modeFake.Enabled = true;
        _modeForce.Enabled = true;
        _list.Enabled = true;
        _statusLabel.Text = "Idle.";
    }

    private void AppendLog(string msg)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AppendLog), msg);
            return;
        }
        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
    }
}
