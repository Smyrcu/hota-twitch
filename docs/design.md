# hota-twitch — design

Date: 2026-09-09. Open points are listed at the end.

## 1. Goal

A Twitch Extension (Video Overlay) for Heroes III: Horn of the Abyss. A viewer hovers a hero
or a town in the right-hand panel of the stream and sees the game's own popup rebuilt on the
video; a click expands it. The HotA team asked for two things: native look and fonts, and a
DLL plugin as the in-game part. Both are requirements.

Out of scope: anything drawn inside the game, anything about the opponent, chat commands,
statistics.

## 2. Parts

```
plugin/    C++17 DLL on NH3API, loaded into the game, posts the player's state (producer)
backend/   .NET 10 Extension Backend Service: tokens, Twitch PubSub broadcast (relay)
overlay/   TypeScript pages served by Twitch: video overlay, streamer config (consumer)
tools/     .NET 10 single-file programs: export sprites, popups and bitmap fonts from the game
deploy/    server bootstrap, systemd, Caddy; GitHub Actions in .github/workflows
docs/      this spec, docs/protocol.md (the contract), research notes from the spike
```

Data flow: game → plugin → `POST /v1/state` → backend → Twitch PubSub → overlay in every
viewer's player. Viewers never talk to the backend. The contract between the three parts is
`docs/protocol.md`; each part is buildable and testable alone against that document.

The spike (`~/hota-twitch-spike`, branch `spike` on GitHub, copies under `docs/research/`)
is the source of every game-memory finding. Its code is not ported; it is rewritten.

## 3. Overlay

### Pages

`video_overlay.html` (viewer), `config.html` (streamer, Creator Dashboard), `live_config.html`
(streamer, live dashboard; same bundle as config, status only). Built with esbuild into
`overlay/dist/`, zipped for upload to the Twitch developer console. No framework. All assets
bundled; nothing loaded from outside Twitch's CSP.

### Cards

The card is a replica of the in-game right-click popup: backgrounds `HEROQVBK.PCX` /
`TOWNQVBK.PCX` (194x186), identical field layout, text in the game's bitmap fonts, sprites
from the game. The card scales with the player like the HD Mod scales its UI; images and text
are rendered without smoothing so pixels stay crisp.

Two levels:

- **Hover** — the popup as in the game. Hero: portrait, name, class, level, four primary
  skills, army with counts. Town: picture, name, hall and fort icons, garrison army.
- **Click** — expands below in the same style. Hero: secondary skills, equipped artifacts,
  backpack, movement and mana. Town: mage guild (built levels only, spells as icons, a slot
  under research dimmed with the roll count), visiting hero; a further click shows the hero
  inside the town as a full hero card.

Only ids travel in the state; the overlay owns the tables for class names, town type names,
spell names, skill names, hall and fort names, and the sprite paths.

### Zones

Hover targets are placed over the panel's hero and town lists. The panel is anchored to the
top-right corner of the game window and drawn at the HD Mod interface scale, so a zone is
`(right offset, top offset, size)` in logical pixels multiplied by `display.uiScale`, then
mapped from the game resolution to the player size (contain-fit with letterbox offsets when
the aspect ratios differ). Measured reference: 2560x1440, scale 1 — hero rows at
`x = width − 191, y = 198 + 32·i` (64x32, 8 rows), town rows at `x = width − 54,
y = 214 + 32·i` (48x32, 7 rows); see `docs/research/spike-zones-2560x1440.json`.
Row counts for other heights are derived from the panel height; `?debug=1&bg=<image>` draws
the zones over a screenshot for calibration.

Zones exist only while `screen == "adventure"`. `heroListTop` / `townListTop` shift which
entry a row shows. Clicking a zone never reaches the player (the overlay owns the pointer
only inside zones).

### Fonts

`tools/` exports each game font (`.fnt`: 256 glyphs, 8-bit alpha) as an atlas
`overlay/assets/fonts/<name>.png` plus `<name>.json`:

```jsonc
{ "name": "smalfont", "lineHeight": 12, "glyphs": { "65": { "x": 0, "y": 0, "w": 7, "h": 12, "left": 0, "advance": 8 } } }
```

The atlas holds white glyphs with alpha; the overlay tints them (white, yellow, gold — the
game's text colours) and lays text out glyph by glyph. Fonts used: `bigfont` (names),
`MedFont`, `smalfont`, `tiny` (counts).

### Twitch integration

`Twitch.ext.onAuthorized` (channel id, JWT for the config page), `onContext`
(`hlsLatencyBroadcaster`, `displayResolution`), `listen("broadcast")` with `gz:` decoding
through `DecompressionStream`. State is applied after `hlsLatencyBroadcaster` seconds so the
card matches the video the viewer sees. The config page calls the backend's `/v1/config/*`
with the helper's JWT, shows the token once after generation with a copy button, shows the
connection status (last state age), and the three installation steps.

### Code layout

`src/state` (decode, validate, model), `src/zones`, `src/render/text` (bitmap font),
`src/render/hero`, `src/render/town`, `src/data` (id tables), `src/twitch`, `src/config`
(config page), `src/dev` (mock mode: `?mock=1` polls a local JSON, `?debug=1`). Tests in
vitest: decoding, zone math, text layout, rendering against fixtures captured from the game
(`overlay/test/fixtures/*.json`).

## 4. Backend

### Responsibilities

Accept state from a streamer's plugin, map the token to the channel, broadcast through Twitch
PubSub within Twitch's limits, and serve the streamer configuration API. Nothing else.

### API

Exactly `docs/protocol.md` sections 2–5. Rate limit 2 req/s per token on ingest; broadcast
coalesced to 1 msg/s per channel; 5120-byte cap on the encoded message enforced before
sending. Twitch Helix `POST /helix/extensions/pubsub` with an external JWT (HS256, secret is
the base64-decoded extension secret, `Client-Id` header).

### Structure

Solution `backend/HotaTwitch.slnx`:

- `HotaTwitch.Domain` — `Channel` (channel id, token hash, created, last state at),
  `StreamerToken` (generate, hash, hint), broadcast policy (rate, size).
- `HotaTwitch.Application` — use cases: `IngestState`, `IssueToken`, `RevokeToken`,
  `GetChannelStatus`; ports: `IChannelRepository`, `IPubSubPublisher`, `IClock`.
- `HotaTwitch.Infrastructure` — EF Core + SQLite (`channels` table, migrations), Twitch
  Helix client, extension JWT verification and signing.
- `HotaTwitch.Api` — minimal API, endpoint filters for bearer token and Twitch JWT auth,
  health, structured logging (`LoggerMessage`), `Program.cs` composition only.
- Tests: `HotaTwitch.Domain.Tests`, `HotaTwitch.Application.Tests`,
  `HotaTwitch.Api.Tests` (WebApplicationFactory, in-memory SQLite, fake publisher):
  unknown token → 401, oversized → 413, rate limit → 429, coalescing, JWT role check,
  PubSub message format and size.

Configuration from environment: `TWITCH_CLIENT_ID`, `TWITCH_EXTENSION_SECRET`,
`ConnectionStrings__Default`, `ASPNETCORE_URLS`.

### Deployment

Server `62.238.113.238` (Ubuntu 26.04, 2 vCPU, 4 GB), DNS `hota.smyrcu.net` (Cloudflare,
proxied). Layout owned by `deploy/bootstrap.sh` (idempotent, run as root once):

```
/opt/hota-twitch/releases/<sha>/     published self-contained linux-x64 build
/opt/hota-twitch/current -> releases/<sha>
/var/lib/hota-twitch/hota.db         SQLite, owner hota
/etc/hota-twitch/env                 secrets, mode 0600, owner hota
/etc/systemd/system/hota-twitch.service   ExecStart=/opt/hota-twitch/current/HotaTwitch.Api
/etc/caddy/Caddyfile                 hota.smyrcu.net { reverse_proxy 127.0.0.1:5080 }
```

Users: `hota` (service, no login), `deploy` (SSH key from GitHub Actions; sudo limited to
`systemctl restart hota-twitch`). ufw: 22, 80, 443. Unattended upgrades on. Caddy obtains
certificates from Let's Encrypt (Cloudflare SSL mode Full).

`.github/workflows/backend.yml`: on push to `main` touching `backend/` — restore, build,
test, publish, copy to `releases/<sha>` over SSH, switch `current`, restart, verify
`https://hota.smyrcu.net/health`.

## 5. Plugin

### Behaviour

`hota-twitch.dll` reads the current human player's heroes and towns (never the opponent's),
builds the state document and posts it to the backend with the token from `hota-twitch.ini`
next to the DLL:

```ini
[backend]
url = https://hota.smyrcu.net
token = hts_...
[log]
file = hota-twitch.log
level = info
```

Snapshots are taken on the game thread from a hook on the adventure-map update (NH3API
HiHook), at most every 300 ms, and handed to a worker thread that posts over WinHTTP (works
under Wine/Proton). Posting rules as in `docs/protocol.md` section 2. Nothing is drawn in the
game; no input is intercepted.

### Loading

Undecided — waits for the HotA team's answer (owner asked them how a plugin should be loaded
to stay lobby-legal). The DLL is written so that every mechanism works unchanged: it exports
`HotaTwitch_Init` (for an import-table loader) and also initialises from `DllMain` process
attach when `HotaTwitch_Init` has not been called (for `LoadLibrary` by the HD Mod). Nothing
in this repository patches game files or impersonates other plugins.

### Memory layout

HotA 1.8 keeps the SoD 3.2 executable but relocates several structures; NH3API types cover the
stable part. The spike established (see `docs/research/spike-reader.cs`): hero table found by
scan and pointer-reference ranking; spell table found
through the "Magic Arrow" string; `H3Town + 0xD4` points to a HotA record with mage-guild slot
states and the research roll counter; backpack count at `+0x3D4`; town-list scroll offset in
the adventure dialog (`+0x44 → +0x68`); portrait tables through `hps000kn.pcx` /
`hpl000kn.pcx`; game resolution at `0x69FE68/0x69FE6C`. The plugin documents what it uses in
`docs/hota-memory-layout.md` (English) and reads through named constants, never magic
numbers inline.

Open research for the plugin: where the HD Mod keeps the interface scale (needed for
`display.uiScale`); until found the field is omitted.

### Build

CMake, C++17, targets MinGW (i686, cross-compiled on Linux) and MSVC; NH3API as a git
submodule under `plugin/external/NH3API`; JSON through a small in-repo writer (no runtime
dependencies); clang-format. `.github/workflows/release.yml`: on tag `v*` builds the DLL,
packs `hota-twitch.dll` + `hota-twitch.ini` + `INSTALL.md`, builds the overlay zip, publishes a
GitHub Release and uploads the zips to Cloudflare R2 (download links used by
`smyrcu.net/h3/twitch`).

## 6. Tools

.NET 10 single-file programs (`dotnet run <file>.cs`): LOD reader, DEF/PCX decoders, `.fnt`
exporter, and the in-memory sprite dump for assets that exist only inside the running game
(HotA-encrypted `HotA.lod`). Output goes to `overlay/assets/`. Assets are committed; the
tools are how they are regenerated after a game update.

## 7. Distribution

Streamer flow: install the extension on Twitch → open its configuration → generate the token
and copy it → paste it into `hota-twitch.ini` next to the DLL → start the game. The config
page shows "connected, last state N s ago" once data flows. Download page:
`smyrcu.net/h3/twitch` in the site repository (separate change), linking to the R2 files.

Legal stance (owner's decision): game bitmaps and fonts are bundled as fan content.

## 8. Open points

1. Plugin loading mechanism — HotA team's answer.
2. HD Mod interface scale in memory — plugin research.
3. Row counts of the panel lists at resolutions other than 2560x1440 — calibrate with
   screenshots; overlay ships a calibration mode.
4. Twitch developer console: extension registration, Testing Base URI, allowlist, secrets —
   owner's actions; secrets go to `/etc/hota-twitch/env`, never to the repository.
5. Cloudflare R2 bucket and API token — owner.
