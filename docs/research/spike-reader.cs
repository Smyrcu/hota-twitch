#!/usr/bin/env dotnet
#:property LangVersion=preview
#:property Nullable=enable
#:property PublishAot=false
#:property TreatWarningsAsErrors=true

// SPIKE (throwaway). Czyta stan bohaterów aktywnego gracza z działającego procesu
// HotA (Wine/Proton) przez /proc/<pid>/mem i wypisuje JSON albo wysyła go do EBS.
//
//   dotnet run reader.cs -- --once            # jeden odczyt, JSON na stdout
//   dotnet run reader.cs -- --watch           # pętla, JSON na stdout przy zmianie
//   dotnet run reader.cs -- --watch --post http://localhost:5080/state --token X
//
// Offsety: layout SoD 3.2 z H3API (HotA 1.8.0 zachowuje adresy exe — findings.md
// w ~/hota-native-banks). Tablica bohaterów walidowana, z fallbackiem na skan pamięci.

using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32.SafeHandles;

var options = CliOptions.Parse(args);
var pid = options.Pid ?? GameProcess.FindPid();
if (pid is null)
{
    Console.Error.WriteLine("Nie znaleziono procesu 'h3hota HD.exe'. Odpal grę albo podaj --pid.");
    return 2;
}

using var mem = ProcMem.Open(pid.Value);
if (!GameProcess.LooksLikeH3(mem))
{
    Console.Error.WriteLine($"PID {pid}: pod 0x400000 nie ma nagłówka MZ — to nie exe gry albo brak dostępu do /proc/{pid}/mem.");
    return 3;
}
Console.Error.WriteLine($"[reader] pid={pid}, /proc/{pid}/mem OK");

var reader = new GameStateReader(mem);
var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = options.Once,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};

if (options.Once)
{
    var state = reader.Read();
    Console.WriteLine(JsonSerializer.Serialize(state, jsonOptions));
    return 0;
}

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
// Niezmieniony stan i tak idzie do EBS co RepostInterval: po restarcie EBS ma czym rozgłaszać, a widz, który dołączył
// w spokojnej turze, dostaje stan bez czekania na zmianę w grze (PubSub nie trzyma ostatniej wiadomości).
var repostInterval = TimeSpan.FromSeconds(10);
byte[]? lastHash = null;
var lastPostAt = DateTime.MinValue;
while (true)
{
    try
    {
        var state = reader.Read();
        var json = JsonSerializer.Serialize(state, jsonOptions);
        // Hash bez timestampu — inaczej każdy odczyt wyglądałby na zmianę.
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state with { Timestamp = 0 }, jsonOptions));
        var changed = lastHash is null || !hash.AsSpan().SequenceEqual(lastHash);
        if (changed)
        {
            var bytes = Encoding.UTF8.GetByteCount(json);
            Console.Error.WriteLine($"[reader] {DateTime.Now:HH:mm:ss} zmiana stanu, {bytes} B, gracz={state.Player?.Name ?? "-"}, bohaterów={state.Heroes.Count}, walka={state.InCombat}");
        }
        if (options.PostUrl is null)
        {
            if (changed) Console.WriteLine(json);
            lastHash = hash;
        }
        else if (changed || DateTime.UtcNow - lastPostAt >= repostInterval)
        {
            var posted = await Post(http, options, json);
            lastHash = posted ? hash : null; // EBS nie przyjął stanu (np. restart) — spróbuj ponownie przy następnym odczycie
            if (posted) lastPostAt = DateTime.UtcNow;
        }
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"[reader] błąd odczytu: {ex.Message}");
        lastHash = null;
    }
    await Task.Delay(options.IntervalMs);
}

static async Task<bool> Post(HttpClient http, CliOptions options, string json)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, options.PostUrl);
    request.Headers.Add("X-Reader-Token", options.Token ?? string.Empty);
    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
    try
    {
        using var response = await http.SendAsync(request);
        if (response.IsSuccessStatusCode) return true;
        Console.Error.WriteLine($"[reader] EBS odpowiedział {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
    catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
    {
        Console.Error.WriteLine($"[reader] EBS nieosiągalny: {ex.Message}");
    }
    return false;
}

// ---------------------------------------------------------------------------

sealed record CliOptions(int? Pid, bool Once, string? PostUrl, string? Token, int IntervalMs)
{
    public static CliOptions Parse(string[] args)
    {
        int? pid = null;
        var once = false;
        string? post = null;
        string? token = null;
        var interval = 300;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pid": pid = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--once": once = true; break;
                case "--watch": once = false; break;
                case "--post": post = args[++i]; break;
                case "--token": token = args[++i]; break;
                case "--interval": interval = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                default: throw new ArgumentException($"Nieznany argument: {args[i]}");
            }
        }
        return new CliOptions(pid, once, post, token, interval);
    }
}

static class GameProcess
{
    private const string ProcessName = "h3hota HD.exe";

    public static int? FindPid()
    {
        foreach (var dir in Directory.EnumerateDirectories("/proc"))
        {
            var name = Path.GetFileName(dir);
            if (!int.TryParse(name, out var pid)) continue;
            try
            {
                var comm = File.ReadAllText($"/proc/{pid}/comm").Trim();
                if (comm == ProcessName || comm == ProcessName[..Math.Min(15, ProcessName.Length)]) return pid;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return null;
    }

    public static bool LooksLikeH3(ProcMem mem)
    {
        Span<byte> mz = stackalloc byte[2];
        return mem.TryRead(0x400000, mz) && mz[0] == (byte)'M' && mz[1] == (byte)'Z';
    }
}

/// <summary>Odczyt pamięci obcego procesu przez pread na /proc/pid/mem (ptrace_scope=0, ten sam uid).</summary>
sealed class ProcMem : IDisposable
{
    private readonly SafeFileHandle _handle;
    public int Pid { get; }

    private ProcMem(int pid, SafeFileHandle handle)
    {
        Pid = pid;
        _handle = handle;
    }

    public static ProcMem Open(int pid) =>
        new(pid, File.OpenHandle($"/proc/{pid}/mem", FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

    public bool TryRead(uint address, Span<byte> buffer)
    {
        try
        {
            var total = 0;
            while (total < buffer.Length)
            {
                var n = RandomAccess.Read(_handle, buffer[total..], address + total);
                if (n <= 0) return false;
                total += n;
            }
            return true;
        }
        catch (IOException) { return false; }
    }

    public byte[] Read(uint address, int length)
    {
        var buffer = new byte[length];
        if (!TryRead(address, buffer)) throw new IOException($"Nie da się odczytać 0x{address:X8} ({length} B)");
        return buffer;
    }

    public int ReadI32(uint address) => BitConverter.ToInt32(Read(address, 4));
    public uint ReadU32(uint address) => BitConverter.ToUInt32(Read(address, 4));
    public short ReadI16(uint address) => BitConverter.ToInt16(Read(address, 2));
    public sbyte ReadI8(uint address) => (sbyte)Read(address, 1)[0];
    public byte ReadU8(uint address) => Read(address, 1)[0];

    public string ReadCString(uint address, int max)
    {
        if (address == 0) return string.Empty;
        var bytes = Read(address, max);
        var len = Array.IndexOf(bytes, (byte)0);
        if (len < 0) len = max;
        return Encoding.Latin1.GetString(bytes, 0, len);
    }

    /// <summary>Regiony rw z /proc/pid/maps w dolnych 4 GB (przestrzeń 32-bitowego procesu Wine).</summary>
    public IEnumerable<(uint Start, uint End)> ReadableRegions()
    {
        foreach (var line in File.ReadLines($"/proc/{Pid}/maps"))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !parts[1].StartsWith("rw", StringComparison.Ordinal)) continue;
            var range = parts[0].Split('-');
            var start = ulong.Parse(range[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var end = ulong.Parse(range[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (start >= 0x1_0000_0000UL) continue;
            yield return ((uint)start, (uint)Math.Min(end, 0xFFFF_FFFFUL));
        }
    }

    public void Dispose() => _handle.Dispose();
}

/// <summary>Adresy i offsety SoD 3.2 (H3API). Ważne dla HotA 1.8.0 — exe zachowuje adresy.</summary>
static class Layout
{
    public const uint MainPtr = 0x699538;            // H3Main*
    public const uint ActivePlayerPtr = 0x69CCFC;    // H3Player*
    public const uint CombatManagerPtr = 0x699420;   // H3CombatManager* (pod HotA niezerowy także poza walką)
    public const uint AdventureManagerPtr = 0x6992B8; // H3AdventureManager*
    public const uint AdventureManagerDlg = 0x44;     // H3Dlg* okna mapy przygód (800x600 logiczne)
    public const uint AdventureDlgTopTown = 0x68;     // INT32 przewinięcie listy miast w panelu (2026-09-09: 0→1→2 przy przewijaniu); +0x64 = 0, kandydat na listę bohaterów
    public const uint TownManagerPtr = 0x69954C;     // H3TownManager*
    public const uint ExecutiveMgrPtr = 0x699550;    // H3ExecutiveMgr: +0 first, +4 last, +8 active_mgr
    public const uint CreatureTablePtr = 0x6747B0;   // H3CreatureInformation[] (stride 0x74)
    public const uint SecSkillTextPtr = 0x67DCF0;    // H3SecondarySkillText[] (stride 0x10)

    public const uint MainDate = 0x1F63E;            // UINT16 day, week, month
    public const uint MainPlayers = 0x20AD0;         // H3Player[8]
    public const uint PlayerSize = 0x168;
    public const uint MainHeroes = 0x21620;          // H3Hero[156]
    public const uint HeroSize = 0x492;
    public const int SodHeroCount = 156;
    public const uint MainTowns = 0x21610;           // H3Vector<H3Town>: +0 init, +4 first, +8 end, +C capacity (pod HotA nadal tu)
    public const uint TownSize = 0x168;

    // H3Town
    public const uint TownNumber = 0x00;             // UINT8
    public const uint TownOwner = 0x01;              // INT8
    public const uint TownType = 0x04;               // UINT8 frakcja
    public const uint TownGarrisonHero = 0x0C;       // INT32 id bohatera w garnizonie (-1 brak)
    public const uint TownVisitingHero = 0x10;       // INT32 id bohatera odwiedzającego (-1 brak)
    public const uint TownMageLevel = 0x14;          // INT8 poziom gildii
    public const uint TownSpells = 0x44;             // INT32[5][6] czary w gildii per krąg (ważne tylko pierwsze 5/4/3/2/1 slotów, reszta = śmieci)
    public const uint TownNameString = 0xC4;         // H3String: +4 PCHAR, +8 length
    public const uint TownGuardsTypes = 0xE0;        // H3Army garnizonu: INT32[7] typy
    public const uint TownGuardsCounts = 0xFC;       // INT32[7] liczebności
    public const uint TownBuilt = 0x150;             // UINT32 bity budynków 0-31 (7 fort, 8 cytadela, 9 zamek, 10-13 ratusze, 17 budynek specjalny = Biblioteka w Tower)
    public const uint TownHotaExtension = 0xD4;      // HotA: wskaźnik na rekord rozszerzeń miasta (w SoD bajty nieużywane)
    public static readonly int[] SpellsPerTier = [5, 4, 3, 2, 1];

    // Rekord rozszerzeń miasta HotA (0xD0 B) — badanie czarów w gildii. Namierzony 2026-09-09 przez diff pamięci.
    public const int HotaTownExtensionSize = 0xD0;
    public const uint HotaTownSlotStates = 0x10;     // INT32[5][6]: 0 slot nie istnieje, 1 zwykły czar, 2 czar w trakcie badania (nie do nauki); stan po „finalizacji" jeszcze nieobserwowany
    public const int HotaTownSlotStateResearch = 2;
    public const int HotaTownSlotStateMax = 15;      // większe wartości = to nie jest rekord HotA
    public const uint HotaTownResearchCount = 0xA4;  // INT32 liczba losowań bieżącego badania (-1, gdy miasto nie ma kręgu 4); zgadza się dla 2 losowań

    // H3Player — miasta w panelu
    public const uint PlayerTownsCount = 0x3E;       // INT8
    public const uint PlayerTowns = 0x40;            // INT8[48]

    // H3Spell (tabela przeniesiona przez HotA — reader szuka jej po napisie "Magic Arrow" = id 15)
    public const uint SpellSize = 0x88;
    public const int MagicArrowSpell = 15;

    // H3Player
    public const uint PlayerOwner = 0x00;            // INT8
    public const uint PlayerHeroCount = 0x01;        // INT8
    public const uint PlayerCurrentHero = 0x04;      // INT32
    public const uint PlayerHeroIds = 0x08;          // INT32[8]
    public const uint PlayerTopHeroIndex = 0x74;     // INT32 (przewinięcie listy w panelu)
    public const uint PlayerName = 0xCC;             // CHAR[21]
    public const uint PlayerIsHuman = 0xE1;          // BOOL8

    // H3Hero
    public const uint HeroSpellPoints = 0x18;        // INT16
    public const uint HeroId = 0x1A;                 // INT32
    public const uint HeroOwner = 0x22;              // INT8
    public const uint HeroName = 0x23;               // CHAR[13]
    public const uint HeroClass = 0x30;              // INT32
    public const uint HeroMaxMovement = 0x49;        // INT32
    public const uint HeroMovement = 0x4D;           // INT32
    public const uint HeroExperience = 0x51;         // INT32
    public const uint HeroLevel = 0x55;              // INT16
    public const uint HeroArmyTypes = 0x91;          // INT32[7]
    public const uint HeroArmyCounts = 0xAD;         // INT32[7]
    public const uint HeroSecSkills = 0xC9;          // INT8[28] poziom
    public const uint HeroSecSkillPos = 0xE5;        // INT8[28] kolejność wyświetlania
    public const uint HeroSecSkillCount = 0x101;     // INT32
    public const uint HeroBodyArtifacts = 0x12D;     // H3Artifact[19] {INT32 id, INT32 subtype}
    public const uint HeroBackpack = 0x1D4;          // H3Artifact[64]
    public const uint HeroBackpackCount = 0x3D4;     // INT8 (H3API mówi 0x3D1, na żywej grze licznik jest pod 0x3D4 = 0x1D4 + 64*8)
    public const uint HeroPrimary = 0x476;           // INT8[4] atak, obrona, moc, wiedza (bazowe)
    public const uint HeroPicture = 0x34;            // UINT8 id portretu
    public const int SecSkillSlots = 28;
    public const int BodySlots = 19;                 // 0 głowa, 1 ramiona, 2 szyja, 3-4 ręce, 5 tors, 6-7 pierścienie, 8 stopy, 9-12 misc1-4, 13-16 machiny, 17 księga, 18 misc5
    public const int BackpackSlots = 64;
    public const int ArtifactSize = 8;

    // H3CreatureInformation
    public const uint CreatureSize = 0x74;
    public const uint CreatureNameSingular = 0x14;   // LPCSTR
    public const uint CreatureNamePlural = 0x18;     // LPCSTR

    // H3SecondarySkillText
    public const uint SecSkillTextSize = 0x10;       // LPCSTR name, LPCSTR description[3]
}

sealed record GameState(
    string Version,
    long Timestamp,
    bool InGame,
    string Screen,
    bool InCombat,
    GameDate? Date,
    PlayerState? Player,
    List<HeroState> Heroes,
    List<TownState> Towns,
    string? Warning);

/// <summary>
/// Fort: 0 brak, 1 fort, 2 cytadela, 3 zamek. Hall: 0 wioska, 1 ratusz, 2 magistrat, 3 kapitol. Spells4/5 puste, gdy gildia za niska.
/// Garrison = armia bohatera garnizonowego, a gdy go nie ma — straż miasta (H3Town.guards).
/// </summary>
sealed record TownState(
    int Id,
    string Name,
    int Type,
    string? TypeName,
    int Fort,
    int Hall,
    int MageLevel,
    List<int> Spells4,
    List<int> Spells5,
    List<string> Spells4Names,
    List<string> Spells5Names,
    GuildResearch? Research,
    List<ArmySlot> Garrison,
    HeroState? GarrisonHero,
    HeroState? VisitingHero);

/// <summary>
/// Badanie czaru w gildii (HotA): Tier 1-5, Slot = indeks w kręgu; czar siedzi już w tablicy gildii, ale nie da się go nauczyć,
/// dopóki gracz nie zamknie badania. Rolls = ile razy ten slot był losowany (w mieście może trwać tylko jedno badanie naraz).
/// </summary>
sealed record GuildResearch(int Tier, int Slot, int SpellId, int Rolls);


sealed record GameDate(int Day, int Week, int Month);

/// <summary>TopHeroIndex/TopTownIndex = od którego wpisu panel pokazuje listy (po przewinięciu strzałkami).</summary>
sealed record PlayerState(int Owner, string Name, bool IsHuman, int CurrentHero, int TopHeroIndex, int TopTownIndex);

sealed record HeroState(
    int Id,
    string Name,
    int ClassId,
    string? ClassName,
    int Picture,
    int Level,
    int Experience,
    int Mana,
    int ManaMax,
    int Movement,
    int MaxMovement,
    int[] Base,
    List<SkillState> Skills,
    List<ArtifactState> Equipped,
    List<ArtifactState> Backpack,
    List<ArmySlot> Army);

sealed record SkillState(int Id, string Name, int Level);

sealed record ArtifactState(int Slot, int Id, int Subtype);

sealed record ArmySlot(int Slot, int Id, string Name, int Count);

sealed class GameStateReader(ProcMem mem)
{
    private const string Version = "spike-1";
    private static readonly string[] ClassNames =
    [
        "Knight", "Cleric", "Ranger", "Druid", "Alchemist", "Wizard", "Demoniac", "Heretic",
        "Death Knight", "Necromancer", "Overlord", "Warlock", "Barbarian", "Battle Mage",
        "Beastmaster", "Witch", "Planeswalker", "Elementalist",
        "Captain", "Navigator", "Mercenary", "Artificer", // HotA: Cove, Factory (potwierdzone na żywej grze)
        "Chieftain", "Elder", // HotA 1.8: miasto typu 11 (nazwy odczytane z pamięci gry 2026-09-09)
    ];

    private HeroTable? _heroTable;
    private readonly Dictionary<int, string> _creatureNames = new();
    private readonly Dictionary<int, string> _skillNames = new();

    public GameState Read()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var main = mem.ReadU32(Layout.MainPtr);
        if (main == 0)
        {
            _heroTable = null;
            return new GameState(Version, now, false, "none", false, null, null, [], [], "poza grą (H3Main == null)");
        }

        var screen = ReadScreen();
        var inCombat = screen == "combat";
        var date = ReadDate(main);
        var playerAddr = mem.ReadU32(Layout.ActivePlayerPtr);
        var player = ReadPlayer(main, playerAddr, out var warning);
        if (player is null)
        {
            return new GameState(Version, now, true, screen, inCombat, date, null, [], [], warning);
        }

        var heroes = new List<HeroState>();
        var count = Math.Clamp((int)mem.ReadI8(playerAddr + Layout.PlayerHeroCount), 0, 8);
        for (var i = 0; i < count; i++)
        {
            var heroId = mem.ReadI32(playerAddr + Layout.PlayerHeroIds + (uint)(4 * i));
            if (heroId < 0) continue;
            if (_heroTable is null)
            {
                _heroTable = HeroTable.Locate(mem, main, heroId, player.Owner, out var locateWarning);
                if (_heroTable is null)
                {
                    return new GameState(Version, now, true, screen, inCombat, date, player, [], [], locateWarning);
                }
            }
            var hero = ReadHero(_heroTable.AddressOf(heroId));
            if (hero.Id != heroId || hero.Name.Length == 0)
            {
                _heroTable = null; // tabela przestała pasować (np. nowa gra) — zlokalizuj ponownie
                return new GameState(Version, now, true, screen, inCombat, date, player, [], [], $"rekord bohatera {heroId} nie przeszedł walidacji, ponowna lokalizacja tablicy");
            }
            heroes.Add(hero);
        }

        return new GameState(Version, now, true, screen, inCombat, date, player, heroes, ReadTowns(main, playerAddr), warning);
    }

    /// <summary>
    /// Bieżący ekran z H3ExecutiveMgr: menedżery tworzą stos, ostatni (last_mgr, +4) obsługuje ekran.
    /// active_mgr (+8) jest niezerowy tylko w trakcie obsługi komunikatu, więc nie nadaje się do pollingu.
    /// </summary>
    private string ReadScreen()
    {
        var executive = mem.ReadU32(Layout.ExecutiveMgrPtr);
        if (executive == 0) return "none";
        var last = mem.ReadU32(executive + 4);
        if (last == 0) return "none";
        if (last == mem.ReadU32(Layout.AdventureManagerPtr)) return "adventure";
        if (last == mem.ReadU32(Layout.CombatManagerPtr)) return "combat";
        if (last == mem.ReadU32(Layout.TownManagerPtr)) return "town";
        return "other";
    }

    private GameDate ReadDate(uint main) => new(
        mem.ReadI16(main + Layout.MainDate),
        mem.ReadI16(main + Layout.MainDate + 2),
        mem.ReadI16(main + Layout.MainDate + 4));

    private PlayerState? ReadPlayer(uint main, uint playerAddr, out string? warning)
    {
        warning = null;
        var first = main + Layout.MainPlayers;
        var last = first + 7 * Layout.PlayerSize;
        if (playerAddr < first || playerAddr > last || (playerAddr - first) % Layout.PlayerSize != 0)
        {
            warning = $"ActivePlayer 0x{playerAddr:X8} poza H3Main.players (0x{first:X8}..0x{last:X8})";
            return null;
        }

        var owner = mem.ReadI8(playerAddr + Layout.PlayerOwner);
        var isHuman = mem.ReadU8(playerAddr + Layout.PlayerIsHuman) != 0;
        var name = mem.ReadCString(playerAddr + Layout.PlayerName, 21);
        if (!isHuman)
        {
            warning = $"tura AI (gracz {owner}) — dane wstrzymane";
            return null;
        }

        return new PlayerState(
            owner,
            name,
            isHuman,
            mem.ReadI32(playerAddr + Layout.PlayerCurrentHero),
            Math.Max(0, mem.ReadI32(playerAddr + Layout.PlayerTopHeroIndex)), // HotA/HD Mod trzyma tu -1 bez przewinięcia
            ReadTopTownIndex());
    }

    /// <summary>Przewinięcie listy miast w panelu: pole dialogu mapy przygód (HD Mod nie zmienia H3Player).</summary>
    private int ReadTopTownIndex()
    {
        var adventure = mem.ReadU32(Layout.AdventureManagerPtr);
        if (adventure < 0x10000) return 0;
        var dlg = mem.ReadU32(adventure + Layout.AdventureManagerDlg);
        if (dlg < 0x10000) return 0;
        var top = mem.ReadI32(dlg + Layout.AdventureDlgTopTown);
        return top is >= 0 and < 48 ? top : 0;
    }

    private HeroState ReadHero(uint hero)
    {
        var classId = mem.ReadI32(hero + Layout.HeroClass);
        var primary = mem.Read(hero + Layout.HeroPrimary, 4);

        var skills = new List<(int Pos, SkillState Skill)>();
        var levels = mem.Read(hero + Layout.HeroSecSkills, Layout.SecSkillSlots);
        var positions = mem.Read(hero + Layout.HeroSecSkillPos, Layout.SecSkillSlots);
        for (var id = 0; id < Layout.SecSkillSlots; id++)
        {
            if (levels[id] is < 1 or > 3) continue;
            skills.Add((positions[id], new SkillState(id, SkillName(id), levels[id])));
        }

        var army = new List<ArmySlot>();
        for (var slot = 0; slot < 7; slot++)
        {
            var type = mem.ReadI32(hero + Layout.HeroArmyTypes + (uint)(4 * slot));
            var amount = mem.ReadI32(hero + Layout.HeroArmyCounts + (uint)(4 * slot));
            if (type < 0 || amount <= 0) continue;
            army.Add(new ArmySlot(slot, type, CreatureName(type, amount), amount));
        }

        // Założone: sloty 0-12 (głowa..misc4) i 18 (misc5); machiny wojenne 13-16 i księga 17 pomijane.
        var equipped = new List<ArtifactState>();
        var body = mem.Read(hero + Layout.HeroBodyArtifacts, Layout.BodySlots * Layout.ArtifactSize);
        foreach (var slot in Enumerable.Range(0, 13).Append(18))
        {
            var id = BitConverter.ToInt32(body, slot * Layout.ArtifactSize);
            if (id < 0) continue;
            equipped.Add(new ArtifactState(slot, id, BitConverter.ToInt32(body, slot * Layout.ArtifactSize + 4)));
        }

        var backpack = new List<ArtifactState>();
        var backpackCount = Math.Clamp((int)mem.ReadI8(hero + Layout.HeroBackpackCount), 0, Layout.BackpackSlots);
        if (backpackCount > 0)
        {
            var pack = mem.Read(hero + Layout.HeroBackpack, backpackCount * Layout.ArtifactSize);
            for (var i = 0; i < backpackCount; i++)
            {
                var id = BitConverter.ToInt32(pack, i * Layout.ArtifactSize);
                if (id < 0) continue;
                backpack.Add(new ArtifactState(i, id, BitConverter.ToInt32(pack, i * Layout.ArtifactSize + 4)));
            }
        }

        return new HeroState(
            mem.ReadI32(hero + Layout.HeroId),
            mem.ReadCString(hero + Layout.HeroName, 13).Trim(),
            classId,
            classId >= 0 && classId < ClassNames.Length ? ClassNames[classId] : null,
            mem.ReadU8(hero + Layout.HeroPicture),
            mem.ReadI16(hero + Layout.HeroLevel),
            mem.ReadI32(hero + Layout.HeroExperience),
            mem.ReadI16(hero + Layout.HeroSpellPoints),
            MaxMana((sbyte)primary[3], levels[IntelligenceSkill]),
            mem.ReadI32(hero + Layout.HeroMovement),
            mem.ReadI32(hero + Layout.HeroMaxMovement),
            [(sbyte)primary[0], (sbyte)primary[1], (sbyte)primary[2], (sbyte)primary[3]],
            skills.OrderBy(s => s.Pos).Select(s => s.Skill).ToList(),
            equipped,
            backpack,
            army);
    }

    private static readonly string[] TownTypeNames =
    [
        "Castle", "Rampart", "Tower", "Inferno", "Necropolis", "Dungeon", "Stronghold", "Fortress", "Conflux",
        "Cove", "Factory", "Bulwark",
    ];

    private SpellTable? _spellTable;

    /// <summary>Miasta aktywnego gracza w kolejności panelu (H3Player.towns) z poziomem gildii i czarami 4./5. kręgu.</summary>
    private List<TownState> ReadTowns(uint main, uint playerAddr)
    {
        var towns = new List<TownState>();
        var first = mem.ReadU32(main + Layout.MainTowns + 4);
        var end = mem.ReadU32(main + Layout.MainTowns + 8);
        if (first < 0x10000 || end <= first) return towns;
        var total = (int)((end - first) / Layout.TownSize);
        var count = Math.Clamp((int)mem.ReadI8(playerAddr + Layout.PlayerTownsCount), 0, 48);
        var ids = mem.Read(playerAddr + Layout.PlayerTowns, 48);
        for (var i = 0; i < count; i++)
        {
            int id = (sbyte)ids[i];
            if (id < 0 || id >= total) continue;
            var town = first + (uint)id * Layout.TownSize;
            if (mem.ReadU8(town + Layout.TownNumber) != id) continue;
            var type = mem.ReadU8(town + Layout.TownType);
            var namePtr = mem.ReadU32(town + Layout.TownNameString + 4);
            var nameLen = (int)Math.Min(mem.ReadU32(town + Layout.TownNameString + 8), 31);
            var name = namePtr < 0x10000 ? $"town#{id}" : mem.ReadCString(namePtr, nameLen + 1);
            var built = mem.ReadU32(town + Layout.TownBuilt);
            var mageLevel = Math.Clamp((int)mem.ReadI8(town + Layout.TownMageLevel), 0, 5);
            var library = type == 2 && (built & (1u << 17)) != 0; // Tower: Biblioteka = +1 czar na każdym kręgu
            var spells4 = GuildSpells(town, 3, mageLevel, library);
            var spells5 = GuildSpells(town, 4, mageLevel, library);
            var research = ReadResearch(town);
            var garrisonHero = HeroInsideTown(mem.ReadI32(town + Layout.TownGarrisonHero));
            var visitingHero = HeroInsideTown(mem.ReadI32(town + Layout.TownVisitingHero));
            towns.Add(new TownState(
                id,
                name,
                type,
                type < TownTypeNames.Length ? TownTypeNames[type] : null,
                HighestBuilt(built, 7, 8, 9),
                Math.Max(0, HighestBuilt(built, 10, 11, 12, 13) - 1), // wioska zawsze jest → 0 wioska, 1 ratusz, 2 magistrat, 3 kapitol
                mageLevel,
                spells4,
                spells5,
                spells4.Select(SpellName).ToList(),
                spells5.Select(SpellName).ToList(),
                research,
                garrisonHero?.Army ?? ReadArmy(town + Layout.TownGuardsTypes, town + Layout.TownGuardsCounts),
                garrisonHero,
                visitingHero));
        }
        return towns;
    }

    /// <summary>
    /// Badanie czarów (HotA): H3Town+0xD4 wskazuje rekord rozszerzeń ze stanami slotów gildii; stan 2 = czar w tym slocie
    /// jest w trakcie badania. Brak sensownego rekordu (SoD, śmieci) → brak badania.
    /// </summary>
    private GuildResearch? ReadResearch(uint town)
    {
        var ext = mem.ReadU32(town + Layout.TownHotaExtension);
        var record = new byte[Layout.HotaTownExtensionSize];
        if (ext < 0x10000 || ext % 4 != 0 || !mem.TryRead(ext, record)) return null;
        GuildResearch? research = null;
        for (var tier = 0; tier < 5; tier++)
        {
            for (var slot = 0; slot < 6; slot++)
            {
                var state = BitConverter.ToInt32(record, (int)Layout.HotaTownSlotStates + (tier * 6 + slot) * 4);
                if (state is < 0 or > Layout.HotaTownSlotStateMax) return null;
                if (state == Layout.HotaTownSlotStateResearch && research is null)
                {
                    var spell = mem.ReadI32(SpellSlotAddress(town, tier, slot));
                    var rolls = Math.Max(0, BitConverter.ToInt32(record, (int)Layout.HotaTownResearchCount));
                    research = new GuildResearch(tier + 1, slot, spell, rolls);
                }
            }
        }
        return research;
    }

    /// <summary>Bohater w mieście nie jest na liście gracza w panelu — czytamy go wprost z tablicy bohaterów (pełna karta, bo widz może ją kliknąć).</summary>
    private HeroState? HeroInsideTown(int heroId)
    {
        if (heroId < 0 || _heroTable is null) return null;
        return ReadHero(_heroTable.AddressOf(heroId));
    }

    /// <summary>Najwyższy zbudowany z podanych budynków: 0 = żaden, 1.. = pozycja na liście.</summary>
    private static int HighestBuilt(uint built, params int[] buildings)
    {
        var level = 0;
        for (var i = 0; i < buildings.Length; i++)
        {
            if ((built & (1u << buildings[i])) != 0) level = i + 1;
        }
        return level;
    }

    private List<int> GuildSpells(uint town, int tier, int mageLevel, bool library)
    {
        var list = new List<int>();
        if (mageLevel < tier + 1) return list; // niezbudowany krąg — nie zdradzamy wylosowanych czarów
        var slots = Layout.SpellsPerTier[tier] + (library ? 1 : 0);
        for (var slot = 0; slot < slots; slot++)
        {
            var id = mem.ReadI32(SpellSlotAddress(town, tier, slot));
            if (id >= 0) list.Add(id);
        }
        return list;
    }

    private static uint SpellSlotAddress(uint town, int tier, int slot) => town + Layout.TownSpells + (uint)((tier * 6 + slot) * 4);

    private List<ArmySlot> ReadArmy(uint typesAddr, uint countsAddr)
    {
        var army = new List<ArmySlot>();
        for (var slot = 0; slot < 7; slot++)
        {
            var type = mem.ReadI32(typesAddr + (uint)(4 * slot));
            var amount = mem.ReadI32(countsAddr + (uint)(4 * slot));
            if (type < 0 || amount <= 0) continue;
            army.Add(new ArmySlot(slot, type, CreatureName(type, amount), amount));
        }
        return army;
    }

    private string SpellName(int id)
    {
        _spellTable ??= SpellTable.Locate(mem);
        return _spellTable?.Name(mem, id) ?? $"spell#{id}";
    }

    private const int IntelligenceSkill = 24;

    /// <summary>Maks. many: wiedza×10 × (1 + Inteligencja); HotA: 20/35/50% (SoD miało 25/50/100%). Bez bonusów z artefaktów.</summary>
    private static int MaxMana(int knowledge, int intelligenceLevel)
    {
        double[] bonus = [0, 0.20, 0.35, 0.50];
        var level = Math.Clamp(intelligenceLevel, 0, 3);
        return (int)Math.Floor(Math.Max(0, knowledge) * 10 * (1 + bonus[level]));
    }

    private string CreatureName(int id, int amount)
    {
        var key = amount == 1 ? id : -id - 1;
        if (_creatureNames.TryGetValue(key, out var cached)) return cached;
        var table = mem.ReadU32(Layout.CreatureTablePtr);
        var entry = table + (uint)id * Layout.CreatureSize;
        var ptr = mem.ReadU32(entry + (amount == 1 ? Layout.CreatureNameSingular : Layout.CreatureNamePlural));
        var name = ptr == 0 ? $"creature#{id}" : mem.ReadCString(ptr, 64);
        _creatureNames[key] = name;
        return name;
    }

    private string SkillName(int id)
    {
        if (_skillNames.TryGetValue(id, out var cached)) return cached;
        var table = mem.ReadU32(Layout.SecSkillTextPtr);
        var ptr = mem.ReadU32(table + (uint)id * Layout.SecSkillTextSize);
        var name = ptr == 0 ? $"skill#{id}" : mem.ReadCString(ptr, 64);
        _skillNames[id] = name;
        return name;
    }
}

static class MemoryScan
{
    /// <summary>Adresy wystąpień wzorca w regionach rw (dolne 4 GB), z wyrównaniem.</summary>
    public static List<uint> FindPattern(ProcMem mem, byte[] pattern, int alignment)
    {
        var hits = new List<uint>();
        const int chunk = 4 * 1024 * 1024;
        foreach (var (start, end) in mem.ReadableRegions())
        {
            for (var pos = start; pos < end; pos += (uint)(chunk - 64))
            {
                var len = (int)Math.Min(chunk, end - pos);
                var buffer = new byte[len];
                if (!mem.TryRead(pos, buffer)) break;
                var span = buffer.AsSpan();
                var idx = 0;
                while (idx < len)
                {
                    var rel = span[idx..].IndexOf(pattern);
                    if (rel < 0) break;
                    var address = pos + (uint)(idx + rel);
                    if (address % alignment == 0) hits.Add(address);
                    idx += rel + 1;
                }
            }
        }
        return hits;
    }

    public static bool IsPrintableName(ReadOnlySpan<byte> raw)
    {
        var len = raw.IndexOf((byte)0);
        if (len <= 0) return false;
        if (!char.IsLetter((char)raw[0])) return false;
        foreach (var b in raw[..len])
        {
            if (b < 0x20 || b > 0x7E) return false;
        }
        return true;
    }
}

/// <summary>Tablica H3Spell (stride 0x88, +0 LPCSTR name). HotA ją przenosi — szukamy napisu "Magic Arrow" (id 15) i wskaźników do niego.</summary>
sealed class SpellTable
{
    public uint Base { get; }
    public int Count { get; }
    private readonly Dictionary<int, string> _names = new();

    private SpellTable(uint @base, int count)
    {
        Base = @base;
        Count = count;
    }

    public static SpellTable? Locate(ProcMem mem)
    {
        var sw = Stopwatch.StartNew();
        foreach (var text in MemoryScan.FindPattern(mem, Encoding.ASCII.GetBytes("Magic Arrow\0"), 1))
        {
            foreach (var reference in MemoryScan.FindPattern(mem, BitConverter.GetBytes(text), 4))
            {
                var candidate = reference - (uint)(Layout.MagicArrowSpell * Layout.SpellSize);
                var count = ValidCount(mem, candidate);
                if (count <= Layout.MagicArrowSpell) continue;
                Console.Error.WriteLine($"[reader] tabela czarów @0x{candidate:X8}, {count} wpisów ({sw.ElapsedMilliseconds} ms)");
                return new SpellTable(candidate, count);
            }
        }
        Console.Error.WriteLine($"[reader] tabeli czarów nie znaleziono ({sw.ElapsedMilliseconds} ms)");
        return null;
    }

    private static int ValidCount(ProcMem mem, uint @base)
    {
        Span<byte> pointer = stackalloc byte[4];
        Span<byte> text = stackalloc byte[32];
        for (var id = 0; id < 256; id++)
        {
            if (!mem.TryRead(@base + (uint)id * Layout.SpellSize, pointer)) return id;
            var ptr = BitConverter.ToUInt32(pointer);
            if (ptr < 0x10000 || !mem.TryRead(ptr, text) || !MemoryScan.IsPrintableName(text)) return id;
        }
        return 256;
    }

    public string? Name(ProcMem mem, int id)
    {
        if (id < 0 || id >= Count) return null;
        if (_names.TryGetValue(id, out var cached)) return cached;
        var ptr = mem.ReadU32(Base + (uint)id * Layout.SpellSize);
        var name = ptr < 0x10000 ? $"spell#{id}" : mem.ReadCString(ptr, 48);
        _names[id] = name;
        return name;
    }
}

/// <summary>Lokalizacja tablicy H3Hero: najpierw offset SoD w H3Main, potem skan pamięci po id/owner/nazwie.</summary>
sealed class HeroTable
{
    public uint Base { get; }
    public uint Stride { get; }
    public string Source { get; }

    private HeroTable(uint @base, uint stride, string source)
    {
        Base = @base;
        Stride = stride;
        Source = source;
    }

    public uint AddressOf(int heroId) => Base + (uint)heroId * Stride;

    public static HeroTable? Locate(ProcMem mem, uint main, int knownHeroId, int owner, out string? warning)
    {
        warning = null;
        var sod = new HeroTable(main + Layout.MainHeroes, Layout.HeroSize, "SoD offset H3Main+0x21620");
        if (IsValidRecord(mem, sod.AddressOf(knownHeroId), knownHeroId, owner))
        {
            Console.Error.WriteLine($"[reader] tablica bohaterów: {sod.Source}, base=0x{sod.Base:X8}, stride=0x{sod.Stride:X}");
            return sod;
        }

        Console.Error.WriteLine($"[reader] offset SoD nie pasuje (hero {knownHeroId}) — skan pamięci...");
        var sw = Stopwatch.StartNew();
        var hits = ScanForHero(mem, knownHeroId, owner);
        Console.Error.WriteLine($"[reader] skan: {hits.Count} trafień w {sw.ElapsedMilliseconds} ms");

        // Kopii rekordu bywa dużo (snapshoty HD Moda, bufory sieciowe). Żywa tablica to ta,
        // na którą wskazują globalne wskaźniki kodu gry — liczymy referencje do adresu bazowego.
        var candidates = new List<HeroTable>();
        foreach (var hit in hits)
        {
            var stride = DeriveStride(mem, hit, knownHeroId);
            if (stride is null) continue;
            candidates.Add(new HeroTable(hit - (uint)knownHeroId * stride.Value, stride.Value, $"skan pamięci @0x{hit:X8}"));
        }
        if (candidates.Count == 0)
        {
            warning = $"nie znaleziono tablicy bohaterów (hero {knownHeroId}, owner {owner})";
            return null;
        }

        sw.Restart();
        var references = CountReferences(mem, candidates.Select(c => c.Base).Distinct().ToList());
        var ranked = candidates
            .DistinctBy(c => c.Base)
            .OrderByDescending(c => references.GetValueOrDefault(c.Base))
            .ThenBy(c => c.Base)
            .ToList();
        Console.Error.WriteLine($"[reader] kandydaci ({ranked.Count}, referencje policzone w {sw.ElapsedMilliseconds} ms):");
        foreach (var c in ranked.Take(6))
        {
            Console.Error.WriteLine($"[reader]   base=0x{c.Base:X8} stride=0x{c.Stride:X} refs={references.GetValueOrDefault(c.Base)}");
        }

        var chosen = ranked[0];
        if (references.GetValueOrDefault(chosen.Base) == 0)
        {
            warning = "żadna kopia tablicy bohaterów nie ma referencji — wybrana pierwsza, dane mogą być nieaktualne";
        }
        Console.Error.WriteLine($"[reader] tablica bohaterów: {chosen.Source}, base=0x{chosen.Base:X8}, stride=0x{chosen.Stride:X}");
        return chosen;
    }

    /// <summary>Stride z sąsiedniego rekordu (id+1 albo id-1) zamiast zakładania 0x492.</summary>
    private static uint? DeriveStride(ProcMem mem, uint hit, int heroId)
    {
        Span<byte> id = stackalloc byte[4];
        foreach (var stride in CandidateStrides())
        {
            if (mem.TryRead(hit + stride + Layout.HeroId, id) && BitConverter.ToInt32(id) == heroId + 1) return stride;
            if (heroId > 0 && hit >= stride && mem.TryRead(hit - stride + Layout.HeroId, id) && BitConverter.ToInt32(id) == heroId - 1) return stride;
        }
        return null;
    }

    private static Dictionary<uint, int> CountReferences(ProcMem mem, List<uint> bases)
    {
        var counts = bases.ToDictionary(b => b, _ => 0);
        var patterns = bases.Select(b => (Base: b, Bytes: BitConverter.GetBytes(b))).ToList();
        const int chunk = 4 * 1024 * 1024;
        foreach (var (start, end) in mem.ReadableRegions())
        {
            for (var pos = start; pos < end; pos += (uint)(chunk - 4))
            {
                var len = (int)Math.Min(chunk, end - pos);
                var buffer = new byte[len];
                if (!mem.TryRead(pos, buffer)) break;
                foreach (var (@base, bytes) in patterns)
                {
                    foreach (var found in FindAll(buffer, bytes))
                    {
                        if (((pos + (uint)found) & 3) == 0) counts[@base]++;
                    }
                }
            }
        }
        return counts;
    }

    private static IEnumerable<uint> CandidateStrides()
    {
        yield return Layout.HeroSize;
        for (uint delta = 1; delta <= 0x80; delta++)
        {
            yield return Layout.HeroSize + delta;
        }
    }

    private static bool IsValidRecord(ProcMem mem, uint address, int heroId, int owner)
    {
        Span<byte> head = stackalloc byte[0x60];
        if (!mem.TryRead(address, head)) return false;
        if (BitConverter.ToInt32(head[(int)Layout.HeroId..]) != heroId) return false;
        if ((sbyte)head[(int)Layout.HeroOwner] != owner) return false;
        var level = BitConverter.ToInt16(head[(int)Layout.HeroLevel..]);
        if (level < 1 || level > 108) return false;
        return IsPrintableName(head.Slice((int)Layout.HeroName, 13));
    }

    private static bool IsPrintableName(ReadOnlySpan<byte> raw)
    {
        var len = raw.IndexOf((byte)0);
        if (len <= 0) return false;
        foreach (var b in raw[..len])
        {
            if (b < 0x20) return false;
        }
        return true;
    }

    private static List<uint> ScanForHero(ProcMem mem, int heroId, int owner)
    {
        var hits = new List<uint>();
        var pattern = BitConverter.GetBytes(heroId);
        const int chunk = 4 * 1024 * 1024;
        foreach (var (start, end) in mem.ReadableRegions())
        {
            for (var pos = start; pos < end; pos += (uint)(chunk - 0x60))
            {
                var len = (int)Math.Min(chunk, end - pos);
                var buffer = new byte[len];
                if (!mem.TryRead(pos, buffer)) break;
                foreach (var found in FindAll(buffer, pattern))
                {
                    var recordStart = (long)found - Layout.HeroId;
                    if (recordStart < 0 || recordStart + 0x60 > len) continue;
                    var record = pos + (uint)recordStart;
                    if (IsValidRecord(mem, record, heroId, owner)) hits.Add(record);
                }
            }
        }
        return hits;
    }

    private static List<int> FindAll(byte[] buffer, byte[] pattern)
    {
        var result = new List<int>();
        var span = buffer.AsSpan();
        var idx = 0;
        while (idx < span.Length)
        {
            var rel = span[idx..].IndexOf(pattern);
            if (rel < 0) break;
            result.Add(idx + rel);
            idx += rel + 1;
        }
        return result;
    }
}
