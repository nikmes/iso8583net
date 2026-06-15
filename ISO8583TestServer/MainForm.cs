using System;
using System.Buffers;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ISO8583Net.Message;
using ISO8583Net.Packager;
using ISO8583Net.Utilities;
using Microsoft.Extensions.Logging;

namespace ISO8583TestServer;

public sealed class MainForm : Form
{
    private const int MaxMessageSize = 4096;
    private const int LengthPrefixSize = 2;

    // ── Controls ──────────────────────────────────────────────────────────
    private readonly NumericUpDown _portInput;
    private readonly TextBox _dialectPathTextBox;
    private readonly Button _dialectBrowseButton;
    private readonly Button _listenButton;
    private readonly TextBox _logTextBox;
    private readonly Label _statusLabel;

    // ── State ─────────────────────────────────────────────────────────────
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private ISOMessagePackager? _messagePackager;
    private ILogger? _logger;
    private int _connectionCount;

    public MainForm()
    {
        Text = "ISO 8583 Test Server";
        Size = new Size(900, 600);
        MinimumSize = new Size(500, 350);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Consolas", 9.75f);

        // ── Port input ────────────────────────────────────────────────────
        var portLabel = new Label
        {
            Text = "Port:",
            Location = new Point(12, 14),
            Size = new Size(35, 23),
            TextAlign = ContentAlignment.MiddleRight
        };

        _portInput = new NumericUpDown
        {
            Location = new Point(52, 12),
            Size = new Size(70, 23),
            Minimum = 1,
            Maximum = 65535,
            Value = 9090
        };

        // ── Dialect path input ───────────────────────────────────────────
        var dialectLabel = new Label
        {
            Text = "Dialect:",
            Location = new Point(132, 14),
            Size = new Size(50, 23),
            TextAlign = ContentAlignment.MiddleRight
        };

        _dialectPathTextBox = new TextBox
        {
            Location = new Point(186, 12),
            Size = new Size(320, 23),
            ReadOnly = true,
            BackColor = Color.White,
            Text = "(built-in VISA)"
        };

        _dialectBrowseButton = new Button
        {
            Text = "...",
            Location = new Point(510, 10),
            Size = new Size(30, 28),
            FlatStyle = FlatStyle.Flat
        };
        _dialectBrowseButton.FlatAppearance.BorderSize = 0;
        _dialectBrowseButton.Click += OnBrowseClick;

        // ── Listen / Stop button ──────────────────────────────────────────
        _listenButton = new Button
        {
            Text = "Start Listening",
            Location = new Point(550, 10),
            Size = new Size(130, 28),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9.75f, FontStyle.Bold)
        };
        _listenButton.FlatAppearance.BorderSize = 0;
        _listenButton.Click += OnListenButtonClick;

        // ── Status label ──────────────────────────────────────────────────
        _statusLabel = new Label
        {
            Text = "Idle",
            Location = new Point(690, 14),
            Size = new Size(185, 23),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.Gray
        };

        // ── Log TextBox (fills all remaining space) ──────────────────────
        _logTextBox = new TextBox
        {
            Location = new Point(12, 52),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(212, 212, 212),
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                      | AnchorStyles.Left | AnchorStyles.Right
        };
        UpdateLogSize();

        // ── Layout ────────────────────────────────────────────────────────
        var topPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(Width, 44),
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
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Event Handlers
    // ═══════════════════════════════════════════════════════════════════════

    private async void OnListenButtonClick(object? sender, EventArgs e)
    {
        if (_listener != null)
        {
            await StopListeningAsync();
        }
        else
        {
            await StartListeningAsync();
        }
    }

    private void OnBrowseClick(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select ISO 8583 Dialect File",
            Filter = "JSON Dialect Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".json",
            InitialDirectory = AppDomain.CurrentDomain.BaseDirectory
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _dialectPathTextBox.Text = dialog.FileName;
        }
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_listener != null)
        {
            e.Cancel = true; // delay close while we shut down
            await StopListeningAsync();
            Close();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Server Lifecycle
    // ═══════════════════════════════════════════════════════════════════════

    private async Task StartListeningAsync()
    {
        int port = (int)_portInput.Value;

        // ── Create logger that writes to the TextBox ──────────────────────
        var loggerProvider = new TextBoxLoggerProvider(_logTextBox);
        var loggerFactory = LoggerFactory.Create(b => b
            .SetMinimumLevel(LogLevel.Debug)
            .AddProvider(loggerProvider));
        _logger = loggerFactory.CreateLogger("Server");

        _logger.LogInformation("─── ISO 8583 Test Server ───");

        // Load dialect (from file if selected, otherwise fall back to built-in VISA)
        string? dialectPath = _dialectPathTextBox.Text;
        bool isBuiltIn = dialectPath == "(built-in VISA)" || string.IsNullOrWhiteSpace(dialectPath);

        if (isBuiltIn)
        {
            _logger.LogInformation("Loading built-in VISA BASE I dialect...");
            _messagePackager = new ISOMessagePackager(_logger);
        }
        else
        {
            _logger.LogInformation("Loading dialect from [{Path}]...", dialectPath);
            _messagePackager = new ISOMessagePackager(_logger, dialectPath);
        }

        _logger.LogInformation("Dialect loaded. {TotalFields} fields defined.",
            _messagePackager.GetTotalFields());

        // Start listener
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);

        try
        {
            _listener.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start listener on port {Port}.", port);
            _listener = null;
            return;
        }

        _logger.LogInformation("Listening on port {Port}...", port);

        // Update UI
        _listenButton.Text = "Stop Listening";
        _listenButton.BackColor = Color.FromArgb(200, 40, 40);
        _portInput.Enabled = false;
        _dialectPathTextBox.Enabled = false;
        _dialectBrowseButton.Enabled = false;
        _statusLabel.Text = $"Listening on port {port}";
        _statusLabel.ForeColor = Color.FromArgb(0, 150, 0);

        // Accept loop on background thread
        _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    private async Task StopListeningAsync()
    {
        _logger?.LogInformation("Stopping listener...");

        _cts?.Cancel();
        _listener?.Stop();
        _listener = null;

        _logger?.LogInformation("Server stopped.");

        // Update UI
        _listenButton.Text = "Start Listening";
        _listenButton.BackColor = Color.FromArgb(0, 120, 215);
        _portInput.Enabled = true;
        _dialectPathTextBox.Enabled = true;
        _dialectBrowseButton.Enabled = true;
        _statusLabel.Text = "Idle";
        _statusLabel.ForeColor = Color.Gray;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _listener != null)
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                int connNum = Interlocked.Increment(ref _connectionCount);

                _logger?.LogInformation(
                    "[#{ConnNum}] Client connected: {RemoteEP}",
                    connNum, client.Client.RemoteEndPoint);

                UpdateStatus($"Active connections: {_connectionCount}");

                _ = HandleClientAsync(client, connNum, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Accept loop error.");
        }
    }

    private async Task HandleClientAsync(
        TcpClient client, int connNum, CancellationToken ct)
    {
        var remoteEp = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var lengthBuffer = new byte[LengthPrefixSize];

                while (!ct.IsCancellationRequested)
                {
                    // ── Read 2-byte big-endian length prefix ─────────────
                    try
                    {
                        await ReadExactlyAsync(stream, lengthBuffer, LengthPrefixSize, ct);
                    }
                    catch (EndOfStreamException)
                    {
                        _logger?.LogInformation("[#{ConnNum}] Disconnected.", connNum);
                        return;
                    }

                    int messageLength = (lengthBuffer[0] << 8) | lengthBuffer[1];

                    if (messageLength <= 0 || messageLength > MaxMessageSize)
                    {
                        _logger?.LogWarning(
                            "[#{ConnNum}] Invalid length: {Length} (0x{Hex})",
                            connNum, messageLength,
                            ISOUtils.Bytes2Hex(lengthBuffer, LengthPrefixSize));
                        return;
                    }

                    byte[] msgBuf = ArrayPool<byte>.Shared.Rent(messageLength);
                    try
                    {
                        await ReadExactlyAsync(stream, msgBuf, messageLength, ct);
                        var span = msgBuf.AsSpan(0, messageLength);

                        _logger?.LogInformation(
                            "[#{ConnNum}] Received {Bytes} bytes",
                            connNum, messageLength);

                        LogHexDump(connNum, span);
                        ParseAndLog(connNum, span);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(msgBuf);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[#{ConnNum}] Error.", connNum);
        }
        finally
        {
            Interlocked.Decrement(ref _connectionCount);
            UpdateStatus($"Active connections: {_connectionCount}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ISO Parsing
    // ═══════════════════════════════════════════════════════════════════════

    private void ParseAndLog(int connNum, ReadOnlySpan<byte> data)
    {
        try
        {
            var msg = new ISOMessage(_logger!, _messagePackager!);
            msg.UnPack(data.ToArray());

            // Log key fields
            string mti = msg.GetFieldValue(0);
            string pan = TryGetField(msg, 2);
            string procCode = TryGetField(msg, 3);
            string amount = TryGetField(msg, 4);
            string trace = TryGetField(msg, 11);
            string respCode = TryGetField(msg, 39);
            string field7 = TryGetField(msg, 7);
            string field24 = TryGetField(msg, 24);
            string field37 = TryGetField(msg, 37);

            _logger?.LogInformation(
                "[#{ConnNum}] ── Parsed Message ──");
            _logger?.LogInformation(
                "[#{ConnNum}] MTI={MTI} PAN={PAN} ProcCode={Proc} " +
                "Amount={Amt} F7={F7} F11={F11} F24={F24} F37={F37} RespCode={Resp}",
                connNum, mti, pan, procCode, amount,
                field7, trace, field24, field37, respCode);

            // Full human-readable field dump
            _logger?.LogInformation("[#{ConnNum}] ── Field Dump ──\n{Dump}",
                connNum, msg.ToString());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[#{ConnNum}] Parse failed.", connNum);
        }
    }

    /// <summary>
    /// Safely tries to get a field value, returning "(not set)" if the field is null.
    /// </summary>
    private static string TryGetField(ISOMessage msg, int fieldNumber)
    {
        try
        {
            var f = msg.GetField(fieldNumber);
            return f?.value ?? "(not set)";
        }
        catch
        {
            return "(err)";
        }
    }

    private void LogHexDump(int connNum, ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[#{connNum}] ── Hex Dump ({data.Length} bytes) ──");

        for (int offset = 0; offset < data.Length; offset += 16)
        {
            int rowLen = Math.Min(16, data.Length - offset);
            sb.Append($"{offset:X4}  ");

            for (int i = 0; i < 16; i++)
            {
                if (i < rowLen)
                    sb.Append($"{data[offset + i]:X2} ");
                else
                    sb.Append("   ");
                if (i == 7) sb.Append(' ');
            }

            sb.Append(" |");
            for (int i = 0; i < rowLen; i++)
            {
                byte b = data[offset + i];
                sb.Append(b is >= 32 and < 127 ? (char)b : '.');
            }
            sb.AppendLine("|");
        }

        _logger?.LogInformation(sb.ToString());
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static async Task ReadExactlyAsync(
        NetworkStream stream, byte[] buffer, int count, CancellationToken ct)
    {
        int offset = 0;
        while (offset < count)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), ct);
            if (n == 0)
                throw new EndOfStreamException(
                    $"Connection closed after {offset}/{count} bytes.");
            offset += n;
        }
    }

    private void UpdateStatus(string text)
    {
        if (InvokeRequired)
            BeginInvoke(() => _statusLabel.Text = text);
        else
            _statusLabel.Text = text;
    }

    private void UpdateLogSize()
    {
        _logTextBox.Size = new Size(
            ClientSize.Width - 24,
            ClientSize.Height - _logTextBox.Top - 12);
    }
}
