using System;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace ISO8583TestClient;

public sealed class TextBoxLoggerProvider : ILoggerProvider
{
    private readonly TextBox _textBox;
    private readonly Control _invokeTarget;

    public TextBoxLoggerProvider(TextBox textBox)
    {
        _textBox = textBox;
        _invokeTarget = textBox;
    }

    public ILogger CreateLogger(string categoryName)
        => new TextBoxLogger(categoryName, this);

    public void Dispose() { }

    public void Append(string message)
    {
        if (_invokeTarget.InvokeRequired)
            _invokeTarget.BeginInvoke(() => AppendInternal(message));
        else
            AppendInternal(message);
    }

    private void AppendInternal(string message)
    {
        message = message.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);

        _textBox.AppendText(message);
        _textBox.SelectionStart = _textBox.TextLength;
        _textBox.ScrollToCaret();
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

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string level = logLevel switch
            {
                LogLevel.Trace => "TRCE", LogLevel.Debug => "DBUG",
                LogLevel.Information => "INFO", LogLevel.Warning => "WARN",
                LogLevel.Error => "ERR ", LogLevel.Critical => "CRIT",
                _ => "????"
            };
            string ts = DateTime.Now.ToString("HH:mm:ss.fff");
            string msg = formatter(state, exception);
            _provider.Append($"{ts} [{level}] {msg}{Environment.NewLine}");
            if (exception != null)
                _provider.Append($"       EXCEPTION: {exception}{Environment.NewLine}");
        }
    }
}
