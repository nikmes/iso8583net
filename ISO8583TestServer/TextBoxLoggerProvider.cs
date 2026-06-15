using System;
using System.Collections.Concurrent;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace ISO8583TestServer;

/// <summary>
/// An <see cref="ILoggerProvider"/> that writes log entries to a WinForms <see cref="TextBox"/>.
/// Thread-safe — marshals UI updates to the main thread via <see cref="Control.Invoke"/>.
/// </summary>
public sealed class TextBoxLoggerProvider : ILoggerProvider
{
    private readonly TextBox _textBox;
    private readonly Control _invokeTarget;
    private readonly ConcurrentQueue<Action> _pendingLogs = new();

    public TextBoxLoggerProvider(TextBox textBox)
    {
        _textBox = textBox;
        _invokeTarget = textBox;
    }

    public ILogger CreateLogger(string categoryName)
        => new TextBoxLogger(categoryName, this);

    public void Dispose() { }

    /// <summary>
    /// Appends a message to the TextBox. Called from any thread.
    /// </summary>
    public void Append(string message)
    {
        if (_invokeTarget.InvokeRequired)
        {
            _invokeTarget.BeginInvoke(() => AppendInternal(message));
        }
        else
        {
            AppendInternal(message);
        }
    }

    private void AppendInternal(string message)
    {
        // Normalize line endings for WinForms TextBox (needs \r\n)
        message = message.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);

        _textBox.AppendText(message);

        // Auto-scroll to bottom
        _textBox.SelectionStart = _textBox.TextLength;
        _textBox.ScrollToCaret();

        // Limit total text to prevent memory blowup
        if (_textBox.TextLength > 100_000)
        {
            _textBox.Text = _textBox.Text[^50_000..];
            _textBox.SelectionStart = _textBox.TextLength;
            _textBox.ScrollToCaret();
        }
    }

    private sealed class TextBoxLogger : ILogger
    {
        private readonly string _category;
        private readonly TextBoxLoggerProvider _provider;

        public TextBoxLogger(string category, TextBoxLoggerProvider provider)
        {
            _category = category;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string level = logLevel switch
            {
                LogLevel.Trace => "TRCE",
                LogLevel.Debug => "DBUG",
                LogLevel.Information => "INFO",
                LogLevel.Warning => "WARN",
                LogLevel.Error => "ERR ",
                LogLevel.Critical => "CRIT",
                _ => "????"
            };

            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string message = formatter(state, exception);

            _provider.Append($"{timestamp} [{level}] {message}{Environment.NewLine}");

            if (exception != null)
                _provider.Append($"       EXCEPTION: {exception}{Environment.NewLine}");
        }
    }
}
