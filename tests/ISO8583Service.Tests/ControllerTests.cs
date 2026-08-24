using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ISO8583Net.Server;
using ISO8583Net.Packager;
using ISO8583Net.Server.Pipeline;
using ISO8583Net.Server.Pipeline.Handlers;
using ISO8583Service.Controllers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ISO8583Service.Tests;

public sealed class ControllerTests
{
    private static HandlerRegistry CreateRegistry()
        => new HandlerRegistry(Array.Empty<IMessageHandler>());

    [Fact]
    public async Task Controller_SendSignOn_CallsServerMethod()
    {
        var mockServer = new MockIso8583Server
        {
            IsRunning = true,
            ConnectionCount = 2,
            Connections = new List<(int, string, DateTime)>
            {
                (1, "192.168.1.1:12345", DateTime.UtcNow),
                (2, "192.168.1.2:54321", DateTime.UtcNow)
            }
        };

        var options = new PipelineOptions();
        var registry = CreateRegistry();
        var host = new PipelineHost(options, registry, NullLoggerFactory.Instance);
        var serverOptions = Options.Create(new ServerOptions());

        var controller = new Iso8583Controller(
            mockServer, host, serverOptions,
            new NullTestLogger<Iso8583Controller>());

        var result = await controller.SendSignOn(CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        Assert.Equal(1, mockServer.SendSignOnCallCount);
    }

    [Fact]
    public async Task Controller_SendSignOn_ServerNotRunning_ReturnsBadRequest()
    {
        var mockServer = new MockIso8583Server
        {
            IsRunning = false,
            ConnectionCount = 0
        };

        var options = new PipelineOptions();
        var registry = CreateRegistry();
        var host = new PipelineHost(options, registry, NullLoggerFactory.Instance);
        var serverOptions = Options.Create(new ServerOptions());

        var controller = new Iso8583Controller(
            mockServer, host, serverOptions,
            new NullTestLogger<Iso8583Controller>());

        var result = await controller.SendSignOn(CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);
        Assert.Equal(0, mockServer.SendSignOnCallCount);
    }

    [Fact]
    public async Task Controller_SendSignOn_NoClients_ReturnsOkWithMessage()
    {
        var mockServer = new MockIso8583Server
        {
            IsRunning = true,
            ConnectionCount = 0
        };

        var options = new PipelineOptions();
        var registry = CreateRegistry();
        var host = new PipelineHost(options, registry, NullLoggerFactory.Instance);
        var serverOptions = Options.Create(new ServerOptions());

        var controller = new Iso8583Controller(
            mockServer, host, serverOptions,
            new NullTestLogger<Iso8583Controller>());

        var result = await controller.SendSignOn(CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        Assert.Equal(0, mockServer.SendSignOnCallCount); // not called — no clients
    }

    private sealed class NullTestLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private sealed class MockIso8583Server : IIso8583Server
    {
        public bool IsRunning { get; set; }
        public int ConnectionCount { get; set; }
        public int SignOnIntervalSeconds { get; set; }
        public bool SendSignOnOnConnect { get; set; }
        public bool EnablePeriodicSignOn { get; set; }
        public DialectValidationMode DialectValidationMode { get; set; }
        public TlsOptions Tls { get; set; } = new();
        public Action<string>? OnLog { get; set; }
        public Action<string>? OnStatusChanged { get; set; }
        public Action<int, byte[], string, string>? OnMessageParsed { get; set; }

        public int SendSignOnCallCount;

        public List<(int ConnNum, string RemoteEndpoint, DateTime ConnectedAt)> Connections { get; set; } = new();

        public Task StartAsync(int port, string? dialectPath, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;

        public Task SendSignOnAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref SendSignOnCallCount);
            return Task.CompletedTask;
        }
        public Task SendEchoAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SendSignOffAsync(bool disconnectAfter = false, CancellationToken ct = default)
            => Task.CompletedTask;

        public IReadOnlyList<(int ConnNum, string RemoteEndpoint, DateTime ConnectedAt)> GetConnections()
            => Connections;
    }
}
