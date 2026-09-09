# Installing hota-twitch

The plugin reads your heroes and towns while you play and sends them to the extension, so that
viewers hovering the panel on your stream see the same popup the game shows you. It reads only
your own player: never your opponent's, never anything the game does not already show you.

You need Heroes III: Horn of the Abyss 1.8 with the HD Mod, and a Twitch account you stream from.

## 1. Install the extension on your channel

1. Open the Twitch Creator Dashboard, go to **Extensions**, find **hota-twitch** and install it.
2. Activate it as a **Video Overlay** on your channel.

## 2. Get your token

1. Still in the dashboard, open the extension's **Configure** page.
2. Press **Generate token**. The token is shown **once** - copy it now. It looks like
   `hts_` followed by a long string of letters and digits.

If you lose it, generate a new one on the same page; the old one stops working immediately.

## 3. Set up the plugin

Put `hota-twitch.dll` and `hota-twitch.ini` together in the same folder, open the ini in any
text editor and paste the token:

```ini
[backend]
url = https://hota.smyrcu.net
token = hts_paste-your-token-here
```

If your game is not the English one, set the code page your build stores names in, so that
letters with accents come out right on the cards:

```ini
[text]
codepage = 1250    ; 1252 English and Western, 1250 Central European, 1251 Russian
```

Everything else in the file can stay as it is.

## 4. Load the plugin

> **This step is not settled yet.** How the DLL should be loaded so that the plugin stays legal
> in lobby games is a question for the HotA team, and the answer decides which folder the two
> files go in. Until they answer, do not copy anything into your game folder. This page will be
> replaced with the exact steps once the mechanism is agreed.
>
> The DLL is written so that any of the usual mechanisms works unchanged: it exports
> `HotaTwitch_Init` for a loader that calls into it, and sets itself up on its own when it is
> loaded with `LoadLibrary`. It patches no game files and impersonates no other plugin.

## 5. Check that it works

1. Start the game and load a save.
2. Go back to the extension's **Configure** page. It should say **connected**, with the age of
   the last state in seconds.
3. Open your channel in another browser and hover a hero in the right-hand panel.

## When something is wrong

The plugin never draws anything in the game and never interrupts you; it writes what happened
to `hota-twitch.log`, next to the DLL. Open it and look at the last lines.

| What the log says | What to do |
|---|---|
| `hota-twitch.ini has no [backend] token` | Paste the token into the ini, step 3. |
| `the backend does not know this token` | Generate a new token on the Configure page and paste that one, then restart the game. |
| `the HD Mod patcher is not loaded yet` | The plugin was loaded before the HD Mod. Once step 4 is settled this should not happen; report it if it does. |
| `no connection to https://...` | The backend could not be reached. Check that you are online - the plugin keeps retrying, waiting longer between attempts, and picks up on its own once the connection is back. |
| Nothing at all in the log | The DLL was not loaded. See step 4. |

For more detail, set `level = debug` in the `[log]` section and start the game again. The debug
log also records your HD Mod version and settings, which is what a bug report needs most.

The log holds no secrets: your token is never written to it.
