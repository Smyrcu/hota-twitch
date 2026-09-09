# hota-twitch

Twitch Extension for Heroes of Might and Magic III: Horn of the Abyss.

Viewers hover a hero or a town in the right-hand panel of the stream and get the game's own
popup, rebuilt on the video: army, primary skills and level on hover; skills, artifacts,
mage guild and spell research on click.

| Directory  | What                                                       |
|------------|------------------------------------------------------------|
| `plugin/`  | DLL loaded into the game; reads the current player's state and posts it |
| `backend/` | Extension Backend Service: streamer tokens, Twitch PubSub broadcast      |
| `overlay/` | Twitch extension pages: video overlay, streamer configuration          |
| `tools/`   | Asset and font exporters (game data → overlay assets)                   |
| `deploy/`  | Server bootstrap and deployment                                         |
| `docs/`    | Design spec, protocol, research notes                                   |

See `docs/design.md`.
