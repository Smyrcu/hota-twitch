# tools

.NET 10 single-file programs (`dotnet run <file>.cs`) that export Heroes III game assets into
`overlay/assets/`. See `docs/design.md` sections 3 and 6
for what the overlay expects from these assets.

Shared readers live in `H3Assets/` (a small class library referenced from each script with
`#:project H3Assets/H3Assets.csproj`): `LodArchive` (LOD directory + zlib), `PcxImage` (8bpp
indexed / 24bpp BGR bitmaps), `DefFile` (RLE sprite sheets), `BitmapFont` (`.fnt` bitmap fonts).
Tested by `H3Assets.Tests` against small synthetic files for each format variant (`dotnet test`
from `tools/`; `global.json` opts this directory into the Microsoft.Testing.Platform runner
required by the .NET 10 SDK).

All three scripts read from a hardcoded `GameDir` constant pointing at the local Heroes III
install (`Data/h3bitmap.lod`, `Data/h3sprite.lod`, `Data/HotA.lod`); update it if you run these
on a different machine. Output paths are resolved from the script file's own location
(`[CallerFilePath]`), so every command below works from any working directory.

## h3fonts.cs

Exports `bigfont`, `MedFont`, `smalfont`, `tiny`, `verd10b`, `CALLI10R` from `Data/h3bitmap.lod`
to `overlay/assets/fonts/<lowercase-name>.png` + `.json`, per spec section 3: a white,
alpha-only glyph atlas and a JSON table keyed by byte code 0-255 (`x, y, w, h, left, advance`).

```bash
dotnet run tools/h3fonts.cs -- export
dotnet run tools/h3fonts.cs -- render smalfont "Todd  Level 22  1500/1500" out.png
```

`render` is the verification path: it loads only the exported PNG + JSON (never the `.fnt`),
lays the glyphs out left to right (each glyph is blitted at `penX + left`, then `penX += advance`
for the next one) and blits them onto a dark canvas. Used to prove the exported metrics are
self-consistent.

### Format notes (`.fnt`, VCMI's `CBitmapFont`, verified byte-for-byte against the files)

- Byte 5 = line height (all glyphs are rasterised at this fixed height, including descenders —
  there is no per-glyph vertical offset in the format).
- 256 × `(int32 left, uint32 width, int32 right)` from byte 32; `advance = left + width + right`.
- 256 × `uint32` pixel offsets right after the metrics table.
- Pixel blob at a **fixed** byte offset 4128 (not file-size-dependent); one byte per pixel, row
  length = glyph width, `pixelOffset[c+1] - pixelOffset[c] == width[c] * lineHeight` (verified
  against all six fonts — `bin length == metricsBase + 256*12 + 256*4 + pixel blob length`
  exactly, with no slack, for every one of them).
- The raw byte **is** the alpha value. `bigfont` genuinely uses the full 0-255 range (real
  antialiasing); the other five fonts use only `{0, 1, 255}`, where `1` is the game's own 1px
  black drop-shadow (VCMI renders it as solid black, separately from the tinted glyph). Because
  this pipeline exports a single white+alpha atlas (spec section 3), that shadow pixel decodes
  to alpha ≈ 0.4% — effectively invisible, not black. **The native look loses its drop-shadow
  outline in this export.** If that reads as too flat once the popup backgrounds are behind the
  text, the fix is a second render pass in the overlay (e.g. a 1px down-right dark copy drawn
  before the tinted glyph), not a change to this atlas format — baking two colours into "white
  glyphs with alpha" would break the uniform white/yellow/gold tinting the spec describes.
- Only glyphs with `width > 0` (225-226 of 256 per font) get atlas space; the rest still get a
  JSON entry (`w:0,h:0,x:0,y:0`) so every byte code 0-255 is always a valid lookup.
- Spec section 3's JSON example (`"lineHeight": 12`) is illustrative, written before the file
  was opened; the real per-font values (also printed by `export`) are `bigfont` 25, `medfont` 20,
  `smalfont` 16, `tiny` 11, `verd10b` 18, `calli10r` 17. Don't "fix" the exported JSON to match
  the doc's example.

## h3popups.cs

Exports `HEROQVBK.PCX` / `TOWNQVBK.PCX` from `Data/h3bitmap.lod` to
`overlay/assets/ui/popup-hero.png` / `popup-town.png` (194x186, confirmed). Both are fully
opaque 8bpp-indexed bitmaps — no palette key colour, no transparency to preserve.

```bash
dotnet run tools/h3popups.cs
```

## h3sprites.cs

General-purpose DEF explorer/exporter for regenerating individual sprite sheets once you know
the resource name. Opens `HotA.lod`, `h3sprite.lod`, `h3bitmap.lod` in that (override) order,
matching the game's own resolution order.

```bash
dotnet run tools/h3sprites.cs -- list <pattern>              # resource names matching pattern
dotnet run tools/h3sprites.cs -- frames <name.def> <outdir>  # one <index>.png per frame
```

`<index>.png` is the frame's position in the DEF's own declared frame list (summed across groups
in declaration order for a multi-group DEF — single-group icon sheets, the common case for the
table below, number cleanly from 0). A frame that fails to decode is skipped with a message on
stderr instead of aborting the rest of the export.

Note: `HotA.lod`'s entry *names* decode to garbage (the archive is encrypted, not just its
payloads) — `list`/`frames` against it will surface noise or fail to find anything meaningful;
this is expected, not a bug in the reader.

## Regenerating `overlay/assets/`

| Directory | Regenerated by | Status |
|---|---|---|
| `fonts/` | `h3fonts.cs export` | done |
| `ui/popup-hero.png`, `ui/popup-town.png` | `h3popups.cs` | done |
| `ui/hall-*.png`, `ui/fort-*.png` | `h3sprites.cs frames hallfort.def <dir>` (or `itmtl.def` — both exist in `h3sprite.lod`/`h3bitmap.lod`; confirm the right one and the frame→level mapping before trusting it) | not verified |
| `ui/frame-*.png`, `ui/leather.png` | unknown source resource — not identified | not investigated |
| `primary/*.png` | `h3sprites.cs frames PSKILL.def <dir>` (candidate; frame→stat mapping not confirmed) | not verified |
| `artifacts/*.png` | `h3sprites.cs frames Artifact.def <dir>` (candidate; frame index vs. artifact id not confirmed) | not verified |
| `spells/*.png` | `h3sprites.cs frames spells.def <dir>` (candidate) | not verified |
| `skills/*.png` | `h3sprites.cs frames Secskill.def <dir>` (candidate — secondary skill icons) | not verified |
| `creatures/*.png`, `towns/*.png`, `heroes/*.png` | **not** fully in the plaintext LOD archives | see below |

The sprite directories already committed under `overlay/assets/` came from the spike
(`docs/research/spike-h3assets.cs`, commands `sheet`/`frames`/`dumpdef`/`portraits`), which read
some sprites from `HotA.lod` **while the game was running**, via process memory (`dumpdef`,
`portraits`) rather than the archive — `HotA.lod` on disk is encrypted, so HotA-added heroes,
towns and creatures (Cove, Factory, Bulwark, and any HotA-specific artifacts/spells) only exist
in memory once the game has loaded them. `h3sprites.cs` in this repo only reads the plaintext
archives (`h3sprite.lod`, `h3bitmap.lod`) plus whatever `HotA.lod` exposes unencrypted (nothing,
in practice), so it can regenerate SoD-era content but not the HotA additions mixed into
`creatures/`, `towns/` and `heroes/` today. Rebuilding those directories exactly as committed
needs the game running and a memory reader equivalent to the spike's `dumpdef`/`portraits`
commands, which is not implemented here.
