using System.ComponentModel.DataAnnotations;
using FiveStack.Api.Data;
using FiveStack.Api.Squads;

namespace FiveStack.Api.Chat;

/// <summary>A DM (DmKey set) or a squad chat (SquadPostId set). Exactly one of the two is set.</summary>
public class Conversation
{
    public Guid Id { get; set; }
    public Guid? SquadPostId { get; set; }
    public SquadPost? SquadPost { get; set; }
    /// <summary>Both user ids, smaller first, so a pair has one DM (unique index).</summary>
    [MaxLength(64)] public string? DmKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; } // sorts a conversation with no messages yet
    public List<ConversationMember> Members { get; set; } = [];
    public List<Message> Messages { get; set; } = [];

    public static string KeyFor(Guid a, Guid b) => a.CompareTo(b) < 0 ? $"{a:N}{b:N}" : $"{b:N}{a:N}";
}

public class ConversationMember
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public DateTimeOffset LastReadAt { get; set; } // unread = messages from others after this
}

public class Message
{
    public long Id { get; set; }
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public Guid SenderId { get; set; }
    public AppUser Sender { get; set; } = null!;
    [MaxLength(1000)] public string Body { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }
}

/// <summary>Blocker won't get DMs from Blocked, and vice versa.</summary>
public class Block
{
    public Guid BlockerId { get; set; }
    public AppUser Blocker { get; set; } = null!;
    public Guid BlockedId { get; set; }
    public AppUser Blocked { get; set; } = null!;
}
