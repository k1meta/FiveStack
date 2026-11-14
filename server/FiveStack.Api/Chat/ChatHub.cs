using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FiveStack.Api.Chat;

/// <summary>
/// Push-only: the server sends `MessageReceived` and `SquadChanged` to users (Clients.User, keyed by NameIdentifier).
/// Clients send messages over REST, so validation and rate limits live in one place.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    public override Task OnConnectedAsync()
    {
        if (UserId is { } id) Presence.Connect(id);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (UserId is { } id) Presence.Disconnect(id);
        return base.OnDisconnectedAsync(exception);
    }

    Guid? UserId => Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

/// <summary>Open hub connections per user. A user with at least one is online.</summary>
// ponytail: in-memory, one instance; Redis backplane + Redis presence if we ever scale out.
public static class Presence
{
    static readonly Dictionary<Guid, int> Connections = [];

    public static void Connect(Guid id)
    {
        lock (Connections) Connections[id] = Connections.GetValueOrDefault(id) + 1;
    }

    public static void Disconnect(Guid id)
    {
        lock (Connections)
            if (Connections.GetValueOrDefault(id) <= 1) Connections.Remove(id);
            else Connections[id]--;
    }

    public static bool IsOnline(Guid id)
    {
        lock (Connections) return Connections.ContainsKey(id);
    }

    public static Guid[] Online()
    {
        lock (Connections) return [.. Connections.Keys];
    }
}
