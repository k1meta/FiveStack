using System.Diagnostics.Metrics;
using FiveStack.Api.Chat;

namespace FiveStack.Api;

/// <summary>Product metrics on the "FiveStack" meter. OpenTelemetry exports them when it's on (see Program.cs).</summary>
// ponytail: one static Meter; switch to IMeterFactory if tests ever need to read these.
public static class AppMetrics
{
    public static readonly Meter Meter = new("FiveStack");

    public static readonly Counter<long> MessagesSent =
        Meter.CreateCounter<long>("fivestack.messages.sent", description: "Chat messages sent. Tag kind: dm or squad.");

    public static readonly Counter<long> SquadsCreated =
        Meter.CreateCounter<long>("fivestack.squads.created", description: "Squad posts created. Tags: mode, region.");

    public static readonly Counter<long> JoinRequests =
        Meter.CreateCounter<long>("fivestack.squads.join_requests", description: "Requests to join a squad.");

    static readonly ObservableGauge<int> OnlineUsers =
        Meter.CreateObservableGauge("fivestack.users.online", () => Presence.Online().Length,
            description: "Players with an open chat connection.");
}
