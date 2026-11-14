using System.Text.Json;

namespace FiveStack.Api.Meta;

public record Option(string Id, string Name);
public record Region(string Id, string Name, Option[] Servers);
public record Rank(int Tier, string Name, string Icon);
public record Agent(string Id, string Name, string Role, string Icon);

public record MetaDto(
    Region[] Regions, Option[] Platforms, Rank[] Ranks, Option[] Roles, Agent[] Agents, Option[] Languages,
    Option[] Mic, Option[] Modes, Option[] Playstyles, Option[] PlayTimes);

/// <summary>The one source of truth for game data. The client reads it from GET /api/meta.</summary>
public static class GameData
{
    // Latest competitive-tier set on valorant-api.com; icons are {base}/{tier}/smallicon.png.
    const string TierIcons = "https://media.valorant-api.com/competitivetiers/03621f52-342b-cf4e-4f86-9350a49c6d04";

    static Option O(string id, string name) => new(id, name);

    static readonly string[] TierNames = ["Iron", "Bronze", "Silver", "Gold", "Platinum", "Diamond", "Ascendant", "Immortal"];

    // Tier numbers match valorant-api: 0 = Unranked, 3 = Iron 1 ... 26 = Immortal 3, 27 = Radiant.
    static readonly Rank[] Ranks =
    [
        new(0, "Unranked", $"{TierIcons}/0/smallicon.png"),
        .. Enumerable.Range(3, 24)
            .Select(t => new Rank(t, $"{TierNames[(t - 3) / 3]} {(t - 3) % 3 + 1}", $"{TierIcons}/{t}/smallicon.png")),
        new(27, "Radiant", $"{TierIcons}/27/smallicon.png"),
    ];

    // Snapshot of https://valorant-api.com/v1/agents?isPlayableCharacter=true, mapped to { id, name, role, icon }.
    // Regenerate when a new agent ships (id = lowercase name without punctuation, e.g. "kayo").
    static readonly Agent[] Agents = JsonSerializer.Deserialize<Agent[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Meta", "agents.json")), JsonSerializerOptions.Web)!;

    public static readonly MetaDto All = new(
        Regions:
        [
            new("na", "North America", [O("us-west-nca", "US West (N. California)"), O("us-west-or", "US West (Oregon)"), O("us-central", "US Central (Texas)"), O("us-east-va", "US East (N. Virginia)"), O("us-east-ga", "US East (Georgia)"), O("us-central-il", "US Central (Illinois)")]),
            new("latam", "Latin America", [O("mexico", "Mexico City"), O("miami", "Miami"), O("santiago", "Santiago")]),
            new("br", "Brazil", [O("sao-paulo", "São Paulo")]),
            new("eu", "Europe", [O("frankfurt", "Frankfurt"), O("paris", "Paris"), O("london", "London"), O("madrid", "Madrid"), O("stockholm", "Stockholm"), O("warsaw", "Warsaw"), O("istanbul", "Istanbul"), O("bahrain", "Bahrain"), O("tel-aviv", "Tel Aviv")]),
            new("ap", "Asia-Pacific", [O("hong-kong", "Hong Kong"), O("singapore", "Singapore"), O("tokyo", "Tokyo"), O("sydney", "Sydney"), O("mumbai", "Mumbai")]),
            new("kr", "Korea", [O("seoul", "Seoul")]),
        ],
        Platforms: [O("pc", "PC"), O("console", "Console")],
        Ranks: Ranks,
        Roles: [O("duelist", "Duelist"), O("initiator", "Initiator"), O("controller", "Controller"), O("sentinel", "Sentinel")],
        Agents: Agents,
        Languages:
        [
            O("en", "English"), O("es", "Spanish"), O("pt", "Portuguese"), O("fr", "French"), O("de", "German"),
            O("it", "Italian"), O("pl", "Polish"), O("tr", "Turkish"), O("ru", "Russian"), O("ar", "Arabic"),
            O("bs", "Bosnian/Croatian/Serbian"), O("ja", "Japanese"), O("ko", "Korean"), O("zh", "Chinese"),
            O("vi", "Vietnamese"), O("th", "Thai"), O("id", "Indonesian"), O("tl", "Filipino"), O("hi", "Hindi"),
        ],
        Mic: [O("always", "Always on mic"), O("sometimes", "Sometimes"), O("never", "No mic")],
        Modes: [O("competitive", "Competitive"), O("unrated", "Unrated"), O("premier", "Premier"), O("swiftplay", "Swiftplay"), O("spike-rush", "Spike Rush"), O("deathmatch", "Deathmatch"), O("customs", "Customs / 10-mans")],
        Playstyles: [O("chill", "Chill"), O("balanced", "Balanced"), O("tryhard", "Tryhard")],
        PlayTimes: [O("weekday-morning", "Weekday mornings"), O("weekday-afternoon", "Weekday afternoons"), O("weekday-evening", "Weekday evenings"), O("weekday-night", "Weekday late nights"), O("weekend-day", "Weekend days"), O("weekend-evening", "Weekend evenings"), O("weekend-night", "Weekend late nights")]);

    static HashSet<string> Ids(IEnumerable<Option> o) => o.Select(x => x.Id).ToHashSet();

    public static readonly HashSet<string> RegionIds = All.Regions.Select(r => r.Id).ToHashSet();
    public static readonly HashSet<string> PlatformIds = Ids(All.Platforms);
    public static readonly HashSet<int> RankTiers = All.Ranks.Select(r => r.Tier).ToHashSet();
    public static readonly HashSet<string> RoleIds = Ids(All.Roles);
    public static readonly HashSet<string> AgentIds = All.Agents.Select(a => a.Id).ToHashSet();
    public static readonly HashSet<string> LanguageIds = Ids(All.Languages);
    public static readonly HashSet<string> MicIds = Ids(All.Mic);
    public static readonly HashSet<string> ModeIds = Ids(All.Modes);
    public static readonly HashSet<string> PlaystyleIds = Ids(All.Playstyles);
    public static readonly HashSet<string> PlayTimeIds = Ids(All.PlayTimes);

    public static HashSet<string> ServerIds(string? region) =>
        Ids(All.Regions.FirstOrDefault(r => r.Id == region)?.Servers ?? []);
}
