# Version details

The text for the Twitch developer console, field by field: Version Details in the order the
form asks for them, then the two fields the pop-up asks for when the version is submitted. The
images are in `out/`; `README.md` says which file goes in which field.

## Name

```
HotA Overlay
```

## Summary

```
Viewers hover a hero or a town in the game's right-hand panel on the stream and read the card
the game itself shows on a right click. A click on the card expands it.
```

## Description

```
HotA Overlay puts the game's own information on the stream, so viewers can read a hero or a
town for themselves instead of waiting for the streamer to open a screen.

What a viewer sees. Hovering an entry in the adventure map's right-hand panel opens the popup
the game draws on a right click: for a hero the portrait, name, class, level, primary skills
and army; for a town the picture, the hall and the fort, and the garrison. Clicking the card
expands it — the hero's secondary skills, equipped artifacts, backpack, movement and mana, or
the town's mage guild with the spells in it, the slot under research and the heroes inside. The
card is drawn with the game's own bitmaps and fonts and scales with the video, never below its
own size, so it looks the way it looks in the game and stays legible in a small player. Nothing
is drawn anywhere else on the video, and nothing is drawn at all until state arrives.

What the streamer runs. An application next to the game reads the state of the streamer's own
game and posts it to this extension's backend service, which forwards it to viewers over
Twitch's Extension PubSub. The streamer generates a token on the extension's configuration page
and pastes it into that application once.

What is sent. Only the streamer's own side of the game, as the streamer sees it: their heroes
and towns with the names, levels, skills, artifacts, armies, buildings and spells on them, the
in-game date, the in-game player name and colour, how far the two panel lists are scrolled, and
the size of the game window. Nothing about the opponent is sent. The extension asks for
no viewer identity, reads nothing about a viewer, collects no analytics and writes nothing to
chat.
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
<publisher fills in>
```

Required by the form, and nothing is published for it yet: it needs a page stating the terms on
which a streamer may install and run the extension and the application that feeds it.

## Privacy Policy URL

```
https://smyrcu.net/privacy
```

The page behind that address today is the website's own policy, in Polish, and says nothing
about the extension. Before the version is submitted it has to cover, in English, what the
extension sends, what the backend service keeps and for how long, and what the streamer's token
is — otherwise a reviewer following the link reads a policy for something else.

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
