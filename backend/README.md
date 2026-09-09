# HotaTwitch backend

The Extension Backend Service. It takes the player's state from the game plugin, keeps the
streamer tokens, and broadcasts the state to the extension's viewers through Twitch PubSub.
Viewers never talk to this service.

The wire contract is `docs/protocol.md`. This project implements sections 2 to 5 of it and
nothing else.

## Layout

| Project | What is in it |
|---------|---------------|
| `src/HotaTwitch.Domain` | `Channel`, `StreamerToken`, the broadcast payload and the protocol's limits |
| `src/HotaTwitch.Application` | Use cases and the ports they need: repository, publisher, clock |
| `src/HotaTwitch.Infrastructure` | EF Core on SQLite, the Twitch Helix client and the extension JWTs |
| `src/HotaTwitch.Api` | Minimal API endpoints and the composition root |
| `tests/…` | Domain, Application and API tests |

## Running it locally

The service needs no Twitch account to run: `TWITCH_FAKE_PUBSUB=true` replaces the Helix client
with one that writes to the log what it would have broadcast.

```bash
cd backend
TWITCH_CLIENT_ID=local \
TWITCH_EXTENSION_SECRET="$(head -c 32 /dev/urandom | base64)" \
TWITCH_OWNER_USER_ID=1000 \
TWITCH_FAKE_PUBSUB=true \
ConnectionStrings__Default="Data Source=$PWD/hota-twitch.db" \
ASPNETCORE_URLS=http://127.0.0.1:5080 \
dotnet run --project src/HotaTwitch.Api
```

`dotnet build` and `dotnet test` cover the whole solution. Tests run on Microsoft.Testing.Platform
(selected in `global.json`), so run `dotnet test` bare; `--filter` and `--nologo` belong to the
VSTest runner and confuse this one.

Migrations are applied at startup, so the first run creates the database file. To add one:

```bash
cd backend
dotnet tool restore
dotnet ef migrations add <Name> --project src/HotaTwitch.Infrastructure --output-dir Persistence/Migrations
```

## Environment

| Variable | Required | What it is |
|----------|----------|------------|
| `TWITCH_CLIENT_ID` | yes | The extension's client id, sent as the `Client-Id` header to Twitch |
| `TWITCH_EXTENSION_SECRET` | yes | The extension secret in base64, exactly as the developer console shows it. Signs the JWT the backend sends to Twitch and verifies the JWT the extension helper issues to the configuration page |
| `TWITCH_OWNER_USER_ID` | yes | Twitch user id of the extension owner; it becomes the `user_id` claim of the external JWT |
| `ConnectionStrings__Default` | no | SQLite connection string, `Data Source=hota-twitch.db` by default. On the server it points at `/var/lib/hota-twitch/hota.db` |
| `ASPNETCORE_URLS` | no | Where Kestrel listens; the server runs it behind Caddy on `127.0.0.1:5080` |
| `TWITCH_ALLOWED_ORIGINS` | no | Extra CORS origins, comma separated. The extension's own origin is always allowed; this is for the local developer rig Twitch serves on `https://localhost:8080` |
| `TWITCH_FAKE_PUBSUB` | no | `true` logs broadcasts instead of sending them. Never set on the server |

The three required variables are checked at startup, so a missing or malformed one stops the
service instead of surfacing later as failing requests.

In production these live in `/etc/hota-twitch/env` (mode 0600, owner `hota`). They never enter
the repository.

## Who may call what

`POST /v1/state` is called by the plugin over plain HTTP, so it needs no CORS. The `/v1/config`
endpoints are called from the configuration page that Twitch serves at
`https://<client-id>.ext-twitch.tv`, which makes every one of them cross-origin, and the
`Authorization` header turns them into preflighted requests. Only that origin, plus anything
`TWITCH_ALLOWED_ORIGINS` adds, is allowed, and only for GET, POST and DELETE.

## How a streamer gets connected

1. The streamer opens the extension's configuration page on Twitch. The page holds the JWT that
   `Twitch.ext.onAuthorized` gave it and sends it as `Authorization: Bearer <jwt>`.
2. `POST /v1/config/token` mints 32 random bytes, shows them once as `hts_…`, and stores only
   their SHA-256 hash together with a short hint. A second call rotates the token: the previous
   one stops working immediately.
3. The streamer pastes the token into `hota-twitch.ini` next to the plugin DLL.
4. The plugin posts the state to `POST /v1/state` with that token as a bearer credential. The
   backend hashes what it receives and looks the channel up by the hash, so the plain token
   exists only in the streamer's file.
5. `GET /v1/config/channel` answers `hasToken`, `tokenHint` and `lastStateAt`, which is what the
   configuration page shows as the connection status.
6. `DELETE /v1/config/token` forgets the channel altogether. Without a token there is nothing
   left to relay.

## What the service enforces

- A body over 64 KiB is refused with 413, whether or not it announces a `Content-Length`.
- Two ingest requests per second per token; the third inside the same second gets 429 and a
  `Retry-After`.
- A body that is not a version 1 state document gets 400. The backend checks `v`, `ts`, `screen`,
  `player.id`, `heroes` and `towns`; everything else it relays untouched.
- Broadcasts are coalesced to one per second per channel, the newest document winning. Channels are
  published side by side, so one slow call to Twitch does not hold the others back, and the client
  gives up on a broadcast after five seconds. A document whose `gz:` message would pass 5120 bytes
  is dropped with a warning and the previous broadcast stays; the producer still gets 202, because
  the document itself was accepted.
- A token that is rotated or revoked while a state post is in flight wins: the post is answered 401
  and nothing is broadcast, rather than the old token being written back into the database.

## Two decisions worth stating

The state document and its `gz:` encoding live in `HotaTwitch.Domain`, not in Infrastructure,
although they look like transport. Relaying that document *is* what this service is for: the
protocol is the domain here, and the limits, the encoding and what counts as a valid document are
one set of rules that belong together.

`Microsoft.Testing.Platform` is selected in `global.json` because VSTest no longer runs xunit v3 on
the .NET 10 SDK.
