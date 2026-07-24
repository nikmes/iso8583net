using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ISO8583Net.Message;
using ISO8583Net.Packager;

namespace ISO8583HexParser;

/// <summary>
/// Paste hex → click Parse → see parsed message or exact error.
/// Automatically strips 2-byte LI prefix if detected.
/// </summary>
public sealed class ParserForm : Form
{
    private readonly TextBox _dialectPathTextBox;
    private readonly TextBox _hexInput;
    private readonly RichTextBox _logTextBox;

    private ISOMessagePackager? _packager;
    private string _lastDialectPath = "";

    public ParserForm()
    {
        Text = "ISO 8583 Hex Parser";
        Size = new Size(950, 700);
        MinimumSize = new Size(600, 400);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Consolas", 9.75f);

        // ── Dialect row ────────────────────────────────────────────────
        var dialectLabel = new Label
        {
            Text = "Dialect:", Location = new Point(12, 14),
            Size = new Size(55, 25), TextAlign = ContentAlignment.MiddleRight
        };
        _dialectPathTextBox = new TextBox
        {
            Location = new Point(72, 12), Size = new Size(380, 25),
            ReadOnly = true, BackColor = Color.White, Text = "d8-iso8583.json"
        };
        var dialectBrowseBtn = new Button
        {
            Text = "...", Location = new Point(456, 10),
            Size = new Size(32, 28), FlatStyle = FlatStyle.Flat
        };
        dialectBrowseBtn.FlatAppearance.BorderSize = 0;
        dialectBrowseBtn.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select Dialect File",
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = ".json",
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory
            };
            if (dlg.ShowDialog() == DialogResult.OK)
                _dialectPathTextBox.Text = dlg.FileName;
        };

        var parseButton = new Button
        {
            Text = "Parse Hex", Location = new Point(498, 10),
            Size = new Size(110, 30),
            BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9.75f, FontStyle.Bold)
        };
        parseButton.FlatAppearance.BorderSize = 0;
        parseButton.Click += OnParseClick;

        // ── Hex input ──────────────────────────────────────────────────
        var hexLabel = new Label
        {
            Text = "Hex (with optional 2-byte LI prefix, spaces/tabs/newlines ignored):",
            Location = new Point(12, 52), Size = new Size(550, 20),
            ForeColor = Color.FromArgb(80, 80, 80)
        };
        _hexInput = new TextBox
        {
            Location = new Point(12, 74),
            Size = new Size(Width - 36, 200),
            Multiline = true, ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9.75f),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // ── Log output ─────────────────────────────────────────────────
        _logTextBox = new RichTextBox
        {
            Location = new Point(12, 282),
            ReadOnly = true,
            ScrollBars = RichTextBoxScrollBars.Both,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(212, 212, 212),
            BorderStyle = BorderStyle.FixedSingle, WordWrap = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                    | AnchorStyles.Left | AnchorStyles.Right
        };
        UpdateLogSize();

        // ── Layout ─────────────────────────────────────────────────────
        Controls.AddRange(new Control[] {
            dialectLabel, _dialectPathTextBox, dialectBrowseBtn, parseButton,
            hexLabel, _hexInput, _logTextBox
        });
        Resize += (_, _) =>
        {
            _hexInput.Width = ClientSize.Width - 36;
            UpdateLogSize();
        };
    }

    private void OnParseClick(object? sender, EventArgs e)
    {
        _logTextBox.Clear();

        string hex = _hexInput.Text;
        hex = hex.Replace(" ", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");

        if (hex.Length == 0)
        {
            Log("ERROR: Hex input is empty.", Color.Red);
            return;
        }
        if (hex.Length % 2 != 0)
        {
            Log("ERROR: Hex must have even number of digits.", Color.Red);
            return;
        }

        byte[] raw = HexToBytes(hex);
        Log($"Input: {raw.Length} bytes", Color.Cyan);
        LogHexDump(raw);

        // Strip 2-byte LI prefix if present
        byte[] msg;
        int li = (raw[0] << 8) | raw[1];
        if (li == raw.Length - 2)
        {
            Log($"LI prefix detected: 0x{li:X4} = {li} bytes → stripped", Color.Gray);
            msg = new byte[li];
            Array.Copy(raw, 2, msg, 0, li);
        }
        else
        {
            Log("No LI prefix — parsing entire input as raw message.", Color.Gray);
            msg = raw;
        }

        // Load dialect
        LoadPackager();
        if (_packager == null) return;

        // Parse
        try
        {
            var isoMsg = new ISOMessage(
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<ISOMessage>(),
                _packager);
            isoMsg.UnPack(msg);

            Log("", Color.White);
            Log("═══ PARSE SUCCESS ═══", Color.Lime);
            Log(isoMsg.ToString(), Color.White);
        }
        catch (Exception ex)
        {
            Log("", Color.White);
            Log("═══ PARSE FAILED ═══", Color.Red);
            Log($"  {ex.GetType().Name}: {ex.Message}", Color.Red);
            if (ex.StackTrace != null)
            {
                foreach (var line in ex.StackTrace.Split('\n'))
                    Log($"    {line.Trim()}", Color.FromArgb(255, 128, 128));
            }
        }
    }

    private void LoadPackager()
    {
        string path = _dialectPathTextBox.Text;
        if (_packager != null && path == _lastDialectPath) return;
        _lastDialectPath = path;

        bool isBuiltIn = path == "(built-in VISA)" || string.IsNullOrWhiteSpace(path);
        try
        {
            var nullLog = new Microsoft.Extensions.Logging.Abstractions.NullLogger<ISOMessagePackager>();
            _packager = isBuiltIn
                ? new ISOMessagePackager(nullLog)
                : new ISOMessagePackager(nullLog, path);
            Log($"Dialect: {(isBuiltIn ? "built-in VISA" : path)} ({_packager.GetTotalFields()} fields)", Color.Gray);
        }
        catch (Exception ex)
        {
            Log($"ERROR loading dialect: {ex.Message}", Color.Red);
            _packager = null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static byte[] HexToBytes(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < hex.Length; i += 2)
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        return bytes;
    }

    private void Log(string text, Color color)
    {
        if (_logTextBox.InvokeRequired)
        {
            _logTextBox.Invoke(() => AppendColored(text, color));
        }
        else
        {
            AppendColored(text, color);
        }
    }

    private void AppendColored(string text, Color color)
    {
        _logTextBox.SelectionStart = _logTextBox.TextLength;
        _logTextBox.SelectionLength = 0;
        _logTextBox.SelectionColor = color;
        _logTextBox.AppendText(text + Environment.NewLine);
        _logTextBox.SelectionColor = _logTextBox.ForeColor;
    }

    private void LogHexDump(byte[] data)
    {
        var sb = new StringBuilder();
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
            sb.Append('|');
        }
        Log(sb.ToString(), Color.FromArgb(180, 180, 180));
    }

    private void UpdateLogSize()
    {
        _logTextBox.Size = new Size(
            ClientSize.Width - 24,
            ClientSize.Height - _logTextBox.Top - 12);
    }
}
