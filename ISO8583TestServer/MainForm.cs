using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ISO8583Net.Server;

namespace ISO8583TestServer;

public sealed class MainForm : Form
{
    private readonly IIso8583Server _server;

    private readonly NumericUpDown _portInput;
    private readonly TextBox _dialectPathTextBox;
    private readonly Button _dialectBrowseButton;
    private readonly Button _listenButton;
    private readonly TextBox _logTextBox;
    private readonly Label _statusLabel;

    private CancellationTokenSource? _cts;

    public MainForm(IIso8583Server server)
    {
        _server = server;

        Text = "ISO 8583 Test Server";
        Size = new Size(900, 600);
        MinimumSize = new Size(500, 350);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Consolas", 9.75f);

        var portLabel = new Label
        {
            Text = "Port:", Location = new Point(12, 14),
            Size = new Size(35, 23), TextAlign = ContentAlignment.MiddleRight
        };
        _portInput = new NumericUpDown
        {
            Location = new Point(52, 12), Size = new Size(70, 23),
            Minimum = 1, Maximum = 65535, Value = 9090
        };

        var dialectLabel = new Label
        {
            Text = "Dialect:", Location = new Point(132, 14),
            Size = new Size(50, 23), TextAlign = ContentAlignment.MiddleRight
        };
        _dialectPathTextBox = new TextBox
        {
            Location = new Point(186, 12), Size = new Size(320, 23),
            ReadOnly = true, BackColor = Color.White, Text = "(built-in VISA)"
        };
        _dialectBrowseButton = new Button
        {
            Text = "...", Location = new Point(510, 10),
            Size = new Size(30, 28), FlatStyle = FlatStyle.Flat
        };
        _dialectBrowseButton.FlatAppearance.BorderSize = 0;
        _dialectBrowseButton.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select ISO 8583 Dialect File",
                Filter = "JSON Dialect Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = ".json",
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory
            };
            if (dlg.ShowDialog() == DialogResult.OK)
                _dialectPathTextBox.Text = dlg.FileName;
        };

        _listenButton = new Button
        {
            Text = "Start Listening", Location = new Point(550, 10),
            Size = new Size(130, 28),
            BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9.75f, FontStyle.Bold)
        };
        _listenButton.FlatAppearance.BorderSize = 0;
        _listenButton.Click += OnListenClick;

        _statusLabel = new Label
        {
            Text = "Idle", Location = new Point(690, 14),
            Size = new Size(185, 23), TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.Gray
        };

        _logTextBox = new TextBox
        {
            Location = new Point(12, 52), Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(212, 212, 212),
            BorderStyle = BorderStyle.FixedSingle, WordWrap = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                    | AnchorStyles.Left | AnchorStyles.Right
        };
        UpdateLogSize();

        var topPanel = new Panel
        {
            Location = new Point(0, 0), Size = new Size(Width, 44),
            BackColor = Color.FromArgb(240, 240, 240),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        topPanel.Controls.AddRange(new Control[] {
            portLabel, _portInput,
            dialectLabel, _dialectPathTextBox, _dialectBrowseButton,
            _listenButton, _statusLabel
        });

        Controls.AddRange(new Control[] { topPanel, _logTextBox });
        Resize += (_, _) => UpdateLogSize();
        FormClosing += OnFormClosing;

        // ── Wire service callbacks to UI ──────────────────────────────────
        _server.OnLog = msg =>
        {
            if (InvokeRequired) BeginInvoke(() => AppendLog(msg));
            else AppendLog(msg);
        };
        _server.OnStatusChanged = text =>
        {
            if (InvokeRequired) BeginInvoke(() => _statusLabel.Text = text);
            else _statusLabel.Text = text;
        };
    }

    private async void OnListenClick(object? sender, EventArgs e)
    {
        if (_server.IsRunning)
        {
            _cts?.Cancel();
            await _server.StopAsync();
            SetUiRunning(false);
        }
        else
        {
            int port = (int)_portInput.Value;
            string path = _dialectPathTextBox.Text;
            bool isBuiltIn = path == "(built-in VISA)" || string.IsNullOrWhiteSpace(path);

            _cts = new CancellationTokenSource();
            SetUiRunning(true);

            await _server.StartAsync(port, isBuiltIn ? null : path, _cts.Token);

            if (!_server.IsRunning)
                SetUiRunning(false);
        }
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_server.IsRunning)
        {
            e.Cancel = true;
            _cts?.Cancel();
            await _server.StopAsync();
            Close();
        }
    }

    private void SetUiRunning(bool running)
    {
        _listenButton.Text = running ? "Stop Listening" : "Start Listening";
        _listenButton.BackColor = running
            ? Color.FromArgb(200, 40, 40) : Color.FromArgb(0, 120, 215);
        _portInput.Enabled = !running;
        _dialectPathTextBox.Enabled = !running;
        _dialectBrowseButton.Enabled = !running;
        if (!running)
        {
            _statusLabel.Text = "Idle";
            _statusLabel.ForeColor = Color.Gray;
        }
    }

    private void AppendLog(string message)
    {
        message = message.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        _logTextBox.AppendText(message + Environment.NewLine);
        _logTextBox.SelectionStart = _logTextBox.TextLength;
        _logTextBox.ScrollToCaret();
        if (_logTextBox.TextLength > 100_000)
        {
            _logTextBox.Text = _logTextBox.Text[^50_000..];
            _logTextBox.SelectionStart = _logTextBox.TextLength;
            _logTextBox.ScrollToCaret();
        }
    }

    private void UpdateLogSize()
    {
        _logTextBox.Size = new Size(
            ClientSize.Width - 24,
            ClientSize.Height - _logTextBox.Top - 12);
    }
}
