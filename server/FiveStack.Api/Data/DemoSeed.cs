using System.Text.RegularExpressions;
using Bogus;
using FiveStack.Api.Meta;
using FiveStack.Api.Squads;
using Microsoft.EntityFrameworkCore;

namespace FiveStack.Api.Data;

/// <summary>
/// Demo players and squad posts so the finder and board aren't empty on the live site. On when config "Demo:Seed" is true.
/// Every value comes from GameData. Demo users have no password, and the API refuses DMs and join requests to them.
/// </summary>
public static partial class DemoSeed
{
    public const int Players = 150;
    public const int OpenPosts = 6;

    static readonly string[] Bios =
    [
        "Comms first, frags second. Looking for a regular stack.",
        "Trying to hit Immortal this act. No tilt, no flame.",
        "Chill player, good vibes. Lurk enjoyer.",
        "Main IGL for my uni team, need a duo for ranked grind.",
        "Will flash for you. Please swing on my flash.",
        "Back from a break, warming up. Patient teammates welcome.",
        "Premier team looking for a fifth, scrims on weekends.",
        "Night owl, mostly on after 10pm.",
        "Learning Controller, happy to fill.",
        "Aim trainer every day, still whiff the easy ones.",
    ];

    // Seed flavor only: which languages show up per region. Ids are checked against GameData by the tests.
    static readonly Dictionary<string, string[]> RegionLanguages = new()
    {
        ["na"] = ["en", "es", "fr"],
        ["latam"] = ["es", "pt", "en"],
        ["br"] = ["pt", "es", "en"],
        ["eu"] = ["en", "de", "fr", "es", "pl", "tr", "it", "ru", "bs", "ar"],
        ["ap"] = ["en", "ja", "zh", "vi", "th", "id", "tl", "hi"],
        ["kr"] = ["ko", "en"],
    };

    static readonly string[] PostTitles =
    [
        "Ranked grind, need {0}", "Chill games tonight, {0} wanted", "Premier scrims, looking for {0}",
        "Duo queue climbing, {0} please", "Need a {0} who comms", "Late night stack, any {0}?",
    ];

    [GeneratedRegex("[^A-Za-z0-9]")] private static partial Regex NonAlnum();

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync(u => u.IsDemo)) return;

        var f = new Faker { Random = new Randomizer(7) }; // fixed seed: the same demo players on every fresh database
        var meta = GameData.All;
        var now = DateTimeOffset.UtcNow;

        for (var n = 1; n <= Players; n++)
        {
            var region = f.Random.WeightedRandom(meta.Regions, [0.35f, 0.08f, 0.07f, 0.35f, 0.1f, 0.05f]);
            var name = NonAlnum().Replace(f.Internet.UserName(), "");
            if (name.Length > 16) name = name[..16];
            if (name.Length < 3) name += f.Random.AlphaNumeric(3);

            int? rank = f.Random.Bool(0.05f) ? 0 : (f.Random.Int(3, 26) + f.Random.Int(3, 26) + 1) / 2; // peaks around Gold/Plat
            int? peak = rank == 0 ? null : Math.Min(rank!.Value + f.Random.Int(0, 4), 27);
            var roles = f.Random.ListItems(meta.Roles, f.Random.Int(1, 2)).Select(r => r.Id).ToArray();
            var pool = meta.Agents.Where(a => roles.Contains(a.Role)).ToList();
            var langs = RegionLanguages[region.Id];

            db.Users.Add(new AppUser
            {
                UserName = $"demo{n}", NormalizedUserName = $"DEMO{n}", SecurityStamp = Guid.NewGuid().ToString(),
                IsDemo = true,
                DisplayName = name,
                RiotName = name,
                RiotTag = f.Random.Replace(f.PickRandom("###", "####", "????", "??#")).ToUpperInvariant(),
                Region = region.Id,
                Servers = [.. f.Random.ListItems(region.Servers, f.Random.Int(1, Math.Min(3, region.Servers.Length))).Select(s => s.Id)],
                Platform = f.Random.WeightedRandom(meta.Platforms, [0.85f, 0.1f, 0.05f]).Id,
                Rank = rank,
                PeakRank = peak,
                Roles = roles,
                Agents = [.. f.Random.ListItems(pool, f.Random.Int(2, Math.Min(5, pool.Count))).Select(a => a.Id)],
                Languages = [.. new[] { langs[0], f.Random.Bool(0.4f) ? f.PickRandom(langs) : langs[0] }.Distinct()],
                Mic = f.Random.WeightedRandom(meta.Mic, [0.6f, 0.3f, 0.1f]).Id,
                Modes = [.. f.Random.ListItems(meta.Modes, f.Random.Int(1, 3)).Select(m => m.Id)],
                Playstyle = f.PickRandom(meta.Playstyles).Id,
                PlayTimes = [.. f.Random.ListItems(meta.PlayTimes, f.Random.Int(1, 3)).Select(t => t.Id)],
                Bio = f.Random.Bool(0.7f) ? f.PickRandom(Bios) : null,
                OnboardedAt = now - TimeSpan.FromDays(f.Random.Int(15, 90)),
                LastActiveAt = now - TimeSpan.FromMinutes(f.Random.Int(10, 14 * 24 * 60)),
            });
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Keep a few demo posts open on the board: drop expired ones, post fresh ones from demo hosts.</summary>
    // ponytail: two concurrent board loads can both top up (12 posts instead of 6). Harmless for demo data.
    public static async Task TopUpPostsAsync(AppDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var open = await db.SquadPosts.Where(SquadsController.IsOpen(now)).CountAsync(p => p.Host.IsDemo);
        if (open >= OpenPosts) return;

        await db.SquadPosts.Where(p => p.Host.IsDemo && p.ExpiresAt <= now).ExecuteDeleteAsync();
        var hosts = await db.Users
            .Where(u => u.IsDemo && !db.SquadPosts.Any(p => p.HostId == u.Id && p.ClosedAt == null && p.ExpiresAt > now))
            .OrderBy(_ => EF.Functions.Random())
            .Take(OpenPosts - open)
            .ToListAsync();

        var f = new Faker();
        var meta = GameData.All;
        foreach (var h in hosts)
        {
            var needed = f.Random.ListItems(meta.Roles.Where(r => !h.Roles.Contains(r.Id)).ToList(), f.Random.Int(1, 2));
            var age = TimeSpan.FromMinutes(f.Random.Int(0, 60)); // staggered so the board doesn't look batch-made
            db.SquadPosts.Add(new SquadPost
            {
                HostId = h.Id,
                Title = string.Format(f.PickRandom(PostTitles), string.Join(" + ", needed.Select(r => r.Name))),
                Mode = f.PickRandom("competitive", "competitive", "unrated", "premier", "swiftplay"),
                Region = h.Region!,
                Server = h.Servers.FirstOrDefault(),
                RankMin = h.Rank is int lo and > 0 ? Math.Max(3, lo - 3) : null,
                RankMax = h.Rank is int hi and > 0 ? Math.Min(27, hi + 3) : null,
                NeededRoles = [.. needed.Select(r => r.Id)],
                SlotsOpen = f.Random.Int(1, 4),
                MicRequired = f.Random.Bool(0.4f),
                CreatedAt = now - age,
                ExpiresAt = now - age + SquadsController.Lifetime,
            });
        }
        await db.SaveChangesAsync();
    }
}
