# Version details

The text for the Twitch developer console, field by field: Version Details in the order the
form asks for them, then the two fields the pop-up asks for when the version is submitted. The
images are in `out/`; `README.md` says which file goes in which field.

## Name

```
HotA Overlay
```

## Summary

At most 140 characters (the form counts them).

```
Hover a hero or a town in the game's right-hand panel to see the popup the game shows on a right click. Click the card to expand it.
```

## Viewer Summary

At most 140 characters. Optional; shown to viewers while the extension is active.

```
Hover a hero or a town in the panel on the right to read its card: army, skills, artifacts, mage guild. Click the card to expand it.
```

## Description

At most 1024 characters (the form counts them).

```
HotA Overlay puts the game's own information on the stream, so viewers can read a hero or a town for themselves.

What a viewer sees: hovering an entry in the adventure map's right-hand panel opens the popup the game draws on a right click. A hero shows portrait, name, class, level, primary skills and army; a town its picture, hall, fort and garrison. Clicking the card expands it: secondary skills, artifacts, backpack, movement and mana, or the mage guild with the spell under research and the heroes inside. The card uses the game's own bitmaps and fonts and scales with the video. Nothing is drawn until state arrives.

What the streamer runs: a small application next to the game, HotA Twitch Reader, reads the streamer's own game and posts it to the extension's backend, which forwards it over Twitch PubSub. One token, pasted once.

What is sent: only the streamer's own heroes and towns, in-game date, player name and colour, panel scroll and window size. No opponent data, no viewer identity, no analytics.
```

## Author Name

```
Smyrcu
```

## General Category

```
Extension for Games
```

## Game Category

Type `Heroes of Might and Magic III` and take the entries the console offers. The directory
entry Horn of the Abyss is streamed under is `Heroes of Might and Magic III: The Restoration of
Erathia`; take any further Heroes III entries the list holds as well. The field allows up to
fifteen games and is required of an extension built for a game, which this one is.

## Author Email

```
<publisher fills in>
```

Required, and it needs the verification link in the mail Twitch sends afterwards, or the console
sends no notice about the review.

## Support Email

```
<publisher fills in>
```

Optional on the form, but it is the address streamers are pointed at, so it is worth filling in.

## EULA or Terms of Service URL

```
https://smyrcu.net/en/h3/twitch/terms
```

The `/en` prefix matters: the site picks a language from the visitor's location, and a reviewer
or a validator gets no redirect, so the bare address would show Polish.

## Privacy Policy URL

```
https://smyrcu.net/en/privacy#hota-overlay
```

Section 4 of the policy is about the extension: what the reader sends, what the backend keeps
and for how long, and what the streamer's token is.

## Extension Review Channel URL

```
https://www.twitch.tv/smyrcu
```

## Walkthrough Guide and Change Log

```
Change log: first version.

How the extension gets its data. The overlay draws nothing by itself. An application the
streamer runs next to Heroes of Might and Magic III: Horn of the Abyss reads the state of the
streamer's own game — their heroes and towns — and posts it to the extension backend service
at https://hota.smyrcu.net, which forwards it to the channel over Twitch's Extension PubSub.
The overlay listens on the broadcast topic and draws only while a state message is current. It requests no
viewer identity, sends no chat message and calls no other service. It takes the pointer only
inside the hover targets over the two panel lists; everywhere else on the video the player is
untouched.

What you see without a streamer. Nothing. With no state there are no hover targets and no
card, and the video is untouched. The configuration page works on its own: it generates the
streamer's token, shows it once, and reports whether state is arriving.

How to see it working. On the channel https://www.twitch.tv/smyrcu while the streamer is live.
The extension needs a running game, so the channel is live for review at <publisher gives the
availability, a window within 9AM - 5PM PT>. A recording of the overlay in use is at <publisher
fills in when the demonstration video is published>.

What to try. Move the mouse over a hero or a town in the list down the right-hand side of the
game picture: the card opens beside the list. Click it and it expands: a hero card closes again
on the next click, a town card steps through the heroes standing in the town first. The card is
sized from the video and never drawn below its own size, so it stays legible in a small player.
```
