# Protocol

Contract between `plugin/` (producer), `backend/` (relay) and `overlay/` (consumer).
Version `1`. Any incompatible change bumps `v` and is made on all three sides in one change.

## 1. Game state document

JSON, UTF-8, camelCase. Ids are the game's own ids (creature, artifact, spell, skill, class,
town type, hero picture). Names are sent only where the overlay has no table: hero names and
town names. Everything else is looked up in the overlay.

```jsonc
{
  "v": 1,
  "ts": 1788907728157,            // producer clock, unix ms
  "screen": "adventure",          // "none" | "adventure" | "town" | "combat" | "other"
  "date": { "day": 1, "week": 3, "month": 1 },
  "display": { "width": 2560, "height": 1440, "uiScale": 1 },
  "player": {                     // null when screen is "none"
    "id": 0,                      // player colour index 0..7
    "name": "HaveFunMate",
    "currentHero": 184,           // hero id or -1
    "heroListTop": 0,             // first visible index in the panel's hero list
    "townListTop": 0              // first visible index in the panel's town list
  },
  "heroes": [ /* Hero */ ],       // the player's heroes in panel order
  "towns":  [ /* Town */ ]        // the player's towns in panel order
}
```

`screen` is `none` when no game is loaded; then `player` is `null` and `heroes` / `towns` are empty
arrays. The overlay shows cards only on `adventure`.
`display` describes the game window and the HD Mod interface scale (1 = pixel-exact 800x600
widgets, 2 = doubled, fractional values allowed). When the producer cannot read the scale it
omits `uiScale` and the consumer assumes 1.

### Hero

```jsonc
{
  "id": 184,
  "name": "Todd",
  "class": 21,                    // hero class id (0..23 in HotA 1.8)
  "picture": 196,                 // portrait id
  "level": 22,
  "exp": 132486,
  "mana": 27, "manaMax": 390,
  "move": 1500, "moveMax": 1500,
  "primary": [26, 25, 25, 26],    // attack, defense, power, knowledge — effective values as the game's popup shows them (artifacts included)
  "skills": [[7, 3], [19, 2]],    // [skill id, level 1..3], in the hero's slot order
  "equipped": [[0, 12], [18, 143]],  // [body slot, artifact id]; slots 0..12 and 18 only — 13..17 (war machines, spellbook) omitted
  "backpack": [5, 7, 122],        // artifact ids in backpack order
  "army": [[0, 13, 3], [1, 110, 15]] // [slot 0..6, creature id, count]; empty slots omitted
}
```

### Town

```jsonc
{
  "id": 1,
  "name": "New Dolere",
  "type": 10,                     // town type 0..11 (HotA 1.8: 9 Cove, 10 Factory, 11 Bulwark)
  "fort": 3,                      // 0 none, 1 fort, 2 citadel, 3 castle
  "hall": 3,                      // 0 village hall, 1 town hall, 2 city hall, 3 capitol
  "guild": 5,                     // mage guild level 0..5
  "spells": [[15, 27], [], [], [38, 9], [36]],   // exactly five arrays, guild levels 1..5; an unbuilt level is []; only real slots (Library included)
  "research": { "level": 4, "slot": 1, "spell": 55, "rolls": 2 },  // null when no research is open
  "garrison": [[0, 13, 1]],       // army in the town's own garrison slots (see note)
  "garrisonHero": { /* Hero */ }, // null when empty
  "visitingHero": { /* Hero */ }  // null when empty
}
```

Note on `garrison`: when a hero stands in the garrison the game keeps the army on the hero and
the town's own slots are empty; the consumer shows `garrisonHero.army` in that case.

`research` describes HotA spell research that is open in the mage guild: the spell already
sits in `spells[level-1][slot]`, but heroes cannot learn it until the streamer closes the
research. `rolls` is the number of rolls made on that slot so far.

## 2. Producer → backend

```
POST https://hota.smyrcu.net/v1/state
Authorization: Bearer <streamer token>
Content-Type: application/json

<game state document>
```

| Status | Meaning                                                     |
|--------|-------------------------------------------------------------|
| 202    | accepted                                                     |
| 400    | not a valid state document                                   |
| 401    | unknown or revoked token                                     |
| 413    | document larger than 64 KiB                                  |
| 429    | more than 2 requests per second on this token; retry later   |

The producer posts when the state changes and at least every 10 s while a game is loaded.
It never posts more than twice per second. Network errors are logged and retried on the next
tick; nothing is queued.

Streamer tokens are 32 random bytes, base64url, prefixed `hts_`. The backend stores only a
SHA-256 hash.

## 3. Backend → viewers (Twitch PubSub)

The backend broadcasts to the channel bound to the token, at most once per second per channel,
coalescing to the latest document.

Message: the string `gz:` followed by base64 of gzip(state JSON). The encoded message must not
exceed 5120 bytes; a larger document is dropped with a warning and the previous one stays.

Target `broadcast`, sent with an external JWT (`role: external`, `channel_id`,
`pubsub_perms.send: ["broadcast"]`) signed with the extension secret.

## 4. Overlay configuration API (streamer)

Called from the extension's configuration page. Authenticated with the JWT that the Twitch
Extension Helper hands to the page (`onAuthorized`); the backend verifies HS256 with the
extension secret and requires `role == "broadcaster"`.

```
GET    /v1/config/channel   -> { "hasToken": true, "tokenHint": "hts_ab…", "lastStateAt": "2026-09-09T22:00:00Z" | null }
POST   /v1/config/token     -> { "token": "hts_…" }      // generates or rotates; shown once
DELETE /v1/config/token     -> 204
```

## 5. Health

```
GET /health -> { "ok": true }
```
