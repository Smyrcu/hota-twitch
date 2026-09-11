# Extension listing

What the Twitch developer console asks for before a version can be submitted for review, the
sources of the images it asks for, and the script that renders them.

`listing.md` holds the text to paste into the form. `out/` holds the rendered files and is not
in git: run `npm run listing` in `overlay/` to produce it.

## What the console requires

The requirements below are from **Version Details** and **Moving from Hosted Test to Review** on
<https://dev.twitch.tv/docs/extensions/life-cycle/>, read on 2026-09-11. Quotations are that
page's own words. What the assets and the screenshots are, further down, is ours.

### Image assets

| Field | Requirement | File |
| --- | --- | --- |
| Logo Image | "This must be a 100x100 PNG. Do not use Twitch or Glitch logos." | `out/logo.png` |
| Taskbar Icon Image | "(Video-component Extensions only) ... This must be a 24x24 PNG." | `out/icon.png` |
| Discovery Image | "This must be a 300x200 PNG." Tips: "Don’t use a lot of text; that overwhelms the image.", "To maximize visibility, avoid having too much detail in the image.", "Avoid transparency in your PNG." | `out/discovery.png` |
| Screenshot Image | "You must include one or more screenshots ... Images can be PNG, JPG, or GIF. The minimum (and recommended) image size is 1024x768. Images must have a 4:3 aspect ratio. Files must be less than 10MB." | `out/hero-hover.png`, `out/hero-expanded.png`, `out/town-hover.png`, `out/town-expanded.png` |

### Text and reference fields

Required on Version Details: Name, Summary, Description, Author Name, General Category, the four
image fields above, Author Email, EULA or Terms of Service URL, Privacy Policy URL. Optional
there: Game Category — "required for Extensions integrated with a game", which this is, and the
field takes "up to 15 games" — and Support Email. Required when the version is submitted:
Extension Review Channel URL.

The Walkthrough Guide and Change Log sits beside it as optional, but this extension cannot be
reviewed without it. The page asks an extension that "requires a specific game environment or
backend service to be live for it to function" to "provide your availability for review times
within 9AM - 5PM PT in the Walkthrough Guide and Change Log", and warns that Twitch "will reach
out and request another review time if we find that your review channel is not live at the time
of the review".

The same table makes two further tabs required before a version can be submitted — Capabilities
and Asset Hosting. Those are the extension's own settings rather than its listing and are not
covered here, except that the Description below promises no viewer identity and no chat, which
is only true if the Capabilities toggles say so.

The page describes the fields rather than bounding them:

- Summary — "This will be viewable by streamers and viewers. It should be 1-2 brief sentences
  describing what your Extension does."
- Description — "More detail than the Summary about the functions of your Extension."
- Author Name — "The full name of the Extension author or organization that will receive credit
  in the Extensions Manager."
- Author Email — "Contact information for the Extension creator. ... After you submit this
  information, you will get a verification email. Be sure to click on the link in the email."
- Support Email — "Public contact information for support-related queries from streamers."

The General Category drop-down offers exactly these, and the page defines each one: Extension
for Games, Games in Extensions, Schedule and Countdowns, Polling and Voting, Loyalty and
Recognition, Music, Viewer Engagement, Streamer Tools.

### What the documentation does not settle

The form counts characters: Summary and Viewer Summary 140, Description 1024, Author Name and the
URLs as long as the field takes.
  description, the author name, the support email or the walkthrough. The lengths in
  `listing.md` are our own restraint, not a documented bound; the console enforces whatever it
  enforces at paste time.
- **File size of the three assets.** A limit is given only for screenshots (under 10MB). The
  caps the renderer checks against — 64 KiB for the logo, 8 KiB for the icon, 128 KiB for the
  discovery image — are ours, and only catch an asset that came out far heavier than a flat
  drawing should be. "Less than 10MB" is read as ten million bytes, the smaller of the two
  readings.
- **Whether a Video Overlay needs the taskbar icon.** The page marks that field
  "(Video-component Extensions only)" and this extension is a Video Overlay, so by the letter of
  the page it does not apply. The icon is rendered anyway: it costs nothing, and the field is
  there to fill if the console shows it.
- **The exact Game Category option.** The field matches against Twitch's own game list as you
  type, and the page names no option. The one directory entry that resolves is "Heroes of Might
  and Magic III: The Restoration of Erathia"; Horn of the Abyss has none of its own. Type
  "Heroes of Might and Magic III" and take whatever the console's list offers.

## The assets

`logo.svg`, `icon.svg` and `discovery.svg` are the sources. They are drawn here, in the ground,
leather and gold the overlay paints its card with (`#241a10`, `#3b2d1c`, `#8a6a34`, `#d4b24c`,
`#ffe794`); nothing in them is taken from the game's bitmaps, which belong to the game.

The logo and the icon are the same mark at two sizes: the card the extension draws, with a name
plate, a portrait and an army row, the icon keeping only what still reads at 24 pixels. The
discovery image shows the extension at work instead of repeating the mark — a stream, the game's
panel down its right edge, one entry lit and the card open beside it — because that is the
picture a streamer meets first. Its word mark is the one piece of type in the set: it is set in
`'Liberation Serif', Georgia, 'Times New Roman', serif`, and the renderer refuses to draw
without the first of those, so a machine missing it fails instead of quietly producing a
different asset.

## The screenshots

Twitch wants 4:3 and no stream frame is 4:3, so each screenshot is a crop of a real frame around
the card and the panel it belongs to. The frame and the state that produced it are a recording
of a real game kept outside the repository, in `~/hota-twitch/overlay/dev/live-2560x1440.png`
and `~/hota-twitch/overlay/dev/snap.json`; the renderer links them into `dev/generated/` for the
development server to serve and fails naming the path if either is missing.

The crop grows from the card to 4:3 and never below 1024x768, so a shot is either the frame's own
pixels or a reduction of a larger region — the renderer prints which, as a factor after the crop
size. The four shots are the two levels of both cards: a hero on hover and expanded, a town on
hover and expanded with its mage guild.

## Rendering

```
npm run listing
```

It builds the overlay, renders the three assets from their sources, drives the development
harness for the four screenshots, and then reads every file back and holds it against the table
above: the PNG signature, the exact size, a colour type without an alpha channel, and the size
cap. A mismatch names the file and fails the run.

## What to upload where

| Console field | File |
| --- | --- |
| Version Details → Logo Image | `out/logo.png` |
| Version Details → Taskbar Icon Image | `out/icon.png` |
| Version Details → Discovery Image | `out/discovery.png` |
| Version Details → Screenshot Image | `out/hero-hover.png`, `out/hero-expanded.png`, `out/town-hover.png`, `out/town-expanded.png` |

Every text field is in `listing.md`, in the order the form asks for them.
