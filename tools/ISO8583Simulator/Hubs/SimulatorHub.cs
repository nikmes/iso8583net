using Microsoft.AspNetCore.SignalR;

namespace ISO8583Net.Simulator.Hubs;

/// <summary>
/// SignalR hub for real-time simulator event streaming.
/// Clients subscribe to events pushed by the server via IHubContext.
/// No client-to-server methods — the REST API handles commands.
/// </summary>
public sealed class SimulatorHub : Hub
{
}
