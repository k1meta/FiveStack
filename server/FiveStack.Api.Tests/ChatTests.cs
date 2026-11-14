using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FiveStack.Api.Tests;

[Collection("api")]
public class ChatTests(ApiFactory factory)
{
    private static async Task<string> StartDm(HttpClient from, string toId)
    {
        var res = await from.PostAsync($"/api/conversations/dm/{toId}", null);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string conversation, string body) =>
        client.PostAsJsonAsync($"/api/conversations/{conversation}/messages", new { body });

    private static async Task<JsonElement> Conversation(HttpClient client, string id) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/conversations")).EnumerateArray()
        .Single(c => c.GetProperty("id").GetString() == id);

    [Fact]
    public async Task Dm_round_trip_with_unread_counts_and_outsiders_locked_out()
    {
        var (a, aId) = await factory.Player("chat-a@example.com");
        var (b, bId) = await factory.Player("chat-b@example.com");
        var (c, _) = await factory.Player("chat-c@example.com");

        var dm = await StartDm(a, bId);
        Assert.Equal(dm, await StartDm(b, aId)); // one DM per pair, whoever starts it
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsync($"/api/conversations/dm/{aId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(a, dm, "   ")).StatusCode);
        (await Send(a, dm, "  gg, duo?  ")).EnsureSuccessStatusCode();

        var seenByB = await Conversation(b, dm);
        Assert.Equal(1, seenByB.GetProperty("unread").GetInt32());
        Assert.Equal(aId, seenByB.GetProperty("other").GetProperty("id").GetString());
        Assert.Equal(0, (await Conversation(a, dm)).GetProperty("unread").GetInt32()); // own messages don't count

        var messages = await b.GetFromJsonAsync<JsonElement>($"/api/conversations/{dm}/messages");
        Assert.Equal("gg, duo?", messages[0].GetProperty("body").GetString());
        (await b.PostAsync($"/api/conversations/{dm}/read", null)).EnsureSuccessStatusCode();
        Assert.Equal(0, (await Conversation(b, dm)).GetProperty("unread").GetInt32());

        // Not a member: same 404 as a conversation that doesn't exist.
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/conversations/{dm}/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(c, dm, "let me in")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync($"/api/conversations/{dm}/read", null)).StatusCode);
    }

    [Fact]
    public async Task Blocked_player_cant_start_or_send_dms_until_unblocked()
    {
        var (a, aId) = await factory.Player("block-a@example.com");
        var (b, bId) = await factory.Player("block-b@example.com");
        var dm = await StartDm(a, bId);

        (await b.PutAsync($"/api/blocks/{aId}", null)).EnsureSuccessStatusCode();
        Assert.Equal([aId], (await b.GetFromJsonAsync<string[]>("/api/blocks"))!);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.PostAsync($"/api/conversations/dm/{bId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(a, dm, "hello?")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(b, dm, "blocking works both ways")).StatusCode);

        (await b.DeleteAsync($"/api/blocks/{aId}")).EnsureSuccessStatusCode();
        (await Send(a, dm, "hello again")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Squad_chat_has_the_host_and_accepted_players_only()
    {
        var (host, _) = await factory.Player("sqchat-host@example.com");
        var (yes, yesId) = await factory.Player("sqchat-yes@example.com");
        var (no, noId) = await factory.Player("sqchat-no@example.com");
        var res = await host.PostAsJsonAsync("/api/squads", new { title = "Chatty", mode = "competitive", region = "br", slotsOpen = 2 });
        var post = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var chat = (await host.GetFromJsonAsync<JsonElement>($"/api/squads/{post}")).GetProperty("conversationId").GetString()!;
        Assert.Equal("Chatty", (await Conversation(host, chat)).GetProperty("title").GetString());

        (await yes.PostAsync($"/api/squads/{post}/requests", null)).EnsureSuccessStatusCode();
        (await no.PostAsync($"/api/squads/{post}/requests", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await Send(yes, chat, "early")).StatusCode); // pending isn't in yet
        (await host.PutAsJsonAsync($"/api/squads/{post}/requests/{yesId}", new { status = "accepted" })).EnsureSuccessStatusCode();
        (await host.PutAsJsonAsync($"/api/squads/{post}/requests/{noId}", new { status = "declined" })).EnsureSuccessStatusCode();

        Assert.Equal(chat, (await yes.GetFromJsonAsync<JsonElement>($"/api/squads/{post}")).GetProperty("conversationId").GetString());
        (await Send(yes, chat, "hi team")).EnsureSuccessStatusCode();
        Assert.Equal(1, (await Conversation(host, chat)).GetProperty("unread").GetInt32());

        Assert.Equal(JsonValueKind.Null, (await no.GetFromJsonAsync<JsonElement>($"/api/squads/{post}")).GetProperty("conversationId").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await no.GetAsync($"/api/conversations/{chat}/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(no, chat, "pls")).StatusCode);
    }

    [Fact]
    public async Task Hub_requires_sign_in()
    {
        var res = await factory.CreateClient().PostAsync("/hubs/chat/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
