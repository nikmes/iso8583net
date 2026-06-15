using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace ISO8583TestClient;

public sealed class ClientForm : Form
{
    // ── Controls ──────────────────────────────────────────────────────────
    private readonly TextBox _hostInput;
    private readonly NumericUpDown _portInput;
    private readonly Button _connectButton;
    private readonly TextBox _hexInput;
    private readonly Button _sendButton;
    private readonly TextBox _logTextBox;
    private readonly Label _statusLabel;

    // ── State ─────────────────────────────────────────────────────────────
    private TcpClient? _client;
    private NetworkStream? _stream;
    private ILogger? _logger;
    private CancellationTokenSource? _cts;

    public ClientForm()
    {
        Text = "ISO 8583 Test Client";
        Size = new Size(900, 650);
        MinimumSize = new Size(600, 400);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Consolas", 9.75f);

        // ── Host input ────────────────────────────────────────────────────
        var hostLabel = new Label
        {
            Text = "Host:", Location = new Point(12, 14),
            Size = new Size(38, 23), TextAlign = ContentAlignment.MiddleRight
        };
        _hostInput = new TextBox
        {
            Text = "127.0.0.1", Location = new Point(54, 12),
            Size = new Size(110, 23)
        };

        var portLabel = new Label
        {
            Text = "Port:", Location = new Point(172, 14),
            Size = new Size(35, 23), TextAlign = ContentAlignment.MiddleRight
        };
        _portInput = new NumericUpDown
        {
            Location = new Point(210, 12), Size = new Size(60, 23),
            Minimum = 1, Maximum = 65535, Value = 9090
        };

        // ── Connect button ────────────────────────────────────────────────
        _connectButton = new Button
        {
            Text = "Connect", Location = new Point(280, 10),
            Size = new Size(100, 28),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9.75f, FontStyle.Bold)
        };
        _connectButton.FlatAppearance.BorderSize = 0;
        _connectButton.Click += OnConnectClick;

        // ── Status ────────────────────────────────────────────────────────
        _statusLabel = new Label
        {
            Text = "Disconnected", Location = new Point(390, 14),
            Size = new Size(180, 23), TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.Gray
        };

        // ── Hex input panel ───────────────────────────────────────────────
        var hexLabel = new Label
        {
            Text = "Hex message to send (spaces are trimmed):",
            Location = new Point(12, 50), Size = new Size(300, 20),
            ForeColor = Color.FromArgb(80, 80, 80)
        };
        _hexInput = new TextBox
        {
            Location = new Point(12, 72),
            Size = new Size(Width - 36, 180),
            Multiline = true, ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9.75f),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // ── Send button ───────────────────────────────────────────────────
        _sendButton = new Button
        {
            Text = "Send", Location = new Point(12, 258),
            Size = new Size(100, 30),
            BackColor = Color.FromArgb(0, 150, 0),
            ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9.75f, FontStyle.Bold),
            Enabled = false
        };
        _sendButton.FlatAppearance.BorderSize = 0;
        _sendButton.Click += OnSendClick;

        // ── Log TextBox ───────────────────────────────────────────────────
        var logLabel = new Label
        {
            Text = "Log:",
            Location = new Point(12, 295), Size = new Size(50, 20),
            ForeColor = Color.FromArgb(80, 80, 80)
        };
        _logTextBox = new TextBox
        {
            Location = new Point(12, 315),
            Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(212, 212, 212),
            BorderStyle = BorderStyle.FixedSingle, WordWrap = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                    | AnchorStyles.Left | AnchorStyles.Right
        };
        UpdateLogSize();

        // ── Layout ────────────────────────────────────────────────────────
        Controls.AddRange(new Control[] {
            hostLabel, _hostInput, portLabel, _portInput,
            _connectButton, _statusLabel,
            hexLabel, _hexInput, _sendButton,
            logLabel, _logTextBox
        });
        Resize += (_, _) =>
        {
            _hexInput.Width = ClientSize.Width - 36;
            UpdateLogSize();
        };
        FormClosing += OnFormClosing;

        // Init logger
        var lp = new TextBoxLoggerProvider(_logTextBox);
        _logger = LoggerFactory.Create(b => b
            .SetMinimumLevel(LogLevel.Debug)
            .AddProvider(lp)).CreateLogger("Client");

        _logger.LogInformation("─── ISO 8583 Test Client ───");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Events
    // ═══════════════════════════════════════════════════════════════════════

    private async void OnConnectClick(object? sender, EventArgs e)
    {
        if (_client != null)
            await DisconnectAsync();
        else
            await ConnectAsync();
    }

    private async void OnSendClick(object? sender, EventArgs e)
    {
        if (_stream == null) return;

        string hex = _hexInput.Text;
        // Strip all whitespace
        hex = hex.Replace(" ", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");

        if (hex.Length == 0)
        {
            _logger?.LogWarning("Nothing to send — hex input is empty.");
            return;
        }
        if (hex.Length % 2 != 0)
        {
            _logger?.LogWarning("Invalid hex length — must have even number of hex digits.");
            return;
        }

        try
        {
            byte[] bytes = HexToBytes(hex);

            _logger?.LogInformation("Sending {Len} bytes.", bytes.Length);
            LogHexDump("TX", bytes);

            await _stream.WriteAsync(bytes, 0, bytes.Length);
            await _stream.FlushAsync();

            _logger?.LogInformation("Sent successfully.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Send failed.");
        }
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_client != null)
        {
            e.Cancel = true;
            await DisconnectAsync();
            Close();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Connect / Disconnect
    // ═══════════════════════════════════════════════════════════════════════

    private async Task ConnectAsync()
    {
        string host = _hostInput.Text.Trim();
        int port = (int)_portInput.Value;

        _logger?.LogInformation("Connecting to {Host}:{Port}...", host, port);

        _client = new TcpClient();
        try
        {
            await _client.ConnectAsync(host, port);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Connection failed.");
            _client.Dispose();
            _client = null;
            return;
        }

        _stream = _client.GetStream();
        _cts = new CancellationTokenSource();

        _logger?.LogInformation("Connected to {Host}:{Port}.", host, port);

        _connectButton.Text = "Disconnect";
        _connectButton.BackColor = Color.FromArgb(200, 40, 40);
        _sendButton.Enabled = true;
        _hostInput.Enabled = false;
        _portInput.Enabled = false;
        _statusLabel.Text = $"Connected to {host}:{port}";
        _statusLabel.ForeColor = Color.FromArgb(0, 150, 0);
    }

    private async Task DisconnectAsync()
    {
        _logger?.LogInformation("Disconnecting...");

        _cts?.Cancel();
        _stream?.Close();
        _client?.Close();
        _client?.Dispose();
        _client = null;
        _stream = null;
        _cts = null;

        _logger?.LogInformation("Disconnected.");

        _connectButton.Text = "Connect";
        _connectButton.BackColor = Color.FromArgb(0, 120, 215);
        _sendButton.Enabled = false;
        _hostInput.Enabled = true;
        _portInput.Enabled = true;
        _statusLabel.Text = "Disconnected";
        _statusLabel.ForeColor = Color.Gray;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static byte[] HexToBytes(string hex)
    {
        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < hex.Length; i += 2)
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        return bytes;
    }

    private void LogHexDump(string label, byte[] data)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{label}] ── {data.Length} bytes ──");
        for (int off = 0; off < data.Length; off += 16)
        {
            int rowLen = Math.Min(16, data.Length - off);
            sb.Append($"{off:X4}  ");
            for (int i = 0; i < 16; i++)
            {
                if (i < rowLen) sb.Append($"{data[off + i]:X2} ");
                else sb.Append("   ");
                if (i == 7) sb.Append(' ');
            }
            sb.Append(" |");
            for (int i = 0; i < rowLen; i++)
            {
                byte b = data[off + i];
                sb.Append(b is >= 32 and < 127 ? (char)b : '.');
            }
            sb.AppendLine("|");
        }
        _logger?.LogInformation(sb.ToString());
    }

    private void UpdateLogSize()
    {
        _logTextBox.Size = new Size(
            ClientSize.Width - 24,
            ClientSize.Height - _logTextBox.Top - 12);
    }
}
