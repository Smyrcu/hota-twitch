# tools

.NET 10 single-file programs (`dotnet run <file>.cs`) that export Heroes III game assets into
`overlay/assets/`. `docs/design.md` describes what the overlay expects from these assets.

Shared readers live in `H3Assets/` (a small class library referenced from each script with
`#:project H3Assets/H3Assets.csproj`):

| Type | Reads |
|---|---|
| `LodArchive` | plaintext LOD archives (`h3bitmap.lod`, `h3sprite.lod`) |
| `HotaLodArchive` | `HotA.lod`, whose directory is obfuscated |
| `PcxImage` | 8bpp indexed / 24bpp BGR bitmaps |
| `P32FImage` | HotA's 32bpp "P32F" bitmaps |
| `BitmapResource` | either bitmap container, chosen by magic |
| `DefFile` | palette-indexed RLE sprite sheets |
| `Def32File` | HotA's 32bpp "D32F" sprite sheets |
| `SpriteSheet` | either sprite sheet container, chosen by magic |
| `BitmapFont` | `.fnt` bitmap fonts |
| `ProcessMemory` | another process's address space through `/proc/<pid>/mem`, read-only |
| `PortraitTable` | the hero picture id to portrait name table held by a running game |

`H3Assets.Tests` covers them against small synthetic files built per format variant:

```bash
cd tools && dotnet run --project H3Assets.Tests/H3Assets.Tests.csproj
```

`tools/global.json` opts this directory into the Microsoft.Testing.Platform runner; without it
`dotnet test` fails outright, because the .NET 10 SDK no longer supports the VSTest target these
packages would otherwise use. Even with it, `dotnet test` reports "Zero tests ran" on SDK 10.0.301,
so the command above starts the xunit v3 in-process runner directly and is the one to rely on.

All scripts read from a hardcoded `GameDir` constant pointing at the local Heroes III install
(`Data/h3bitmap.lod`, `Data/h3sprite.lod`, `Data/HotA.lod`); update it if you run these on a
different machine.

Commands that write to a fixed location — `h3fonts.cs`, `h3popups.cs`, and `h3lodmem.cs`'s `names`
and `portraits` — resolve it from the script file's own path (`[CallerFilePath]`) and so work from
any working directory. Commands that take an output path — `export`, `frames`, `sheet` — use it
verbatim, so run them from the repository root or pass an absolute path; otherwise `frames` will
happily create a stray `overlay/assets/...` tree under wherever you are.

## HotA.lod

`HotA.lod` holds everything Horn of the Abyss adds or replaces, and the game resolves a resource
name against it before the SoD archives. Its directory is obfuscated; its payloads are not.

Each 32-byte directory entry is:

```
+0   uint nameHash       32-bit FNV-1a over the lowercased resource name
+4   uint offset ^ key
+8   uint size ^ key
+12  uint compressedSize ^ key
+16  16 bytes, opaque
```

`key` is the dword at header offset 12, a field the plaintext format does not use — `h3bitmap.lod`
leaves it zeroed and `h3sprite.lod` has `0x007E0213` there. What establishes it as the key is not
its position but the check below: decoding the directory with it makes all 5232 entries tile the
payload region exactly, which a wrong key cannot do. Payloads are plain zlib, or stored verbatim
when `compressedSize` is 0.

Because names survive only as a hash, the archive **cannot be listed** — a name can only be looked
up once it is already known. Everything else about it is ordinary, which `verify` demonstrates:

```
HotA.lod: 5232 entries, key 0xB5A4D744, data starts at 0x28E5C
contiguous entries: 5232/5232; chain ends at 0x6A380B8, file ends at 0x6A380B8
payloads: 3869 inflated to their declared size, 1363 stored verbatim, 0 failed
directory decodes exactly: every entry accounted for, no payload is encrypted
```

The entries tile the payload region exactly — the first starts where the directory ends, each
next one where the previous ended, and the last ends on the final byte of the file.

HotA also adds two 32bpp containers. Both store their pixel rows **bottom-up**, the way a Windows
DIB does; reading them top-down yields vertically mirrored images that are easy to miss on small,
roughly symmetric sprites.

- **P32F** (single bitmap): magic, reserved, bit depth, total size, header size (40), pixel byte
  count, width, height, then BGRA rows. The row stride comes from the pixel byte count, since the
  format allows padded scanlines.
- **D32F** (sprite sheet): a 24-byte header ending in a group count, a 24-byte group record ending
  in the frame count and the bytes per pixel, a 13-byte name per frame, a dword offset per frame,
  then the frames. Each frame is a 40-byte header (data size, sheet size, the sprite's own size,
  and its margins within the sheet) followed by BGRA rows; the margins place the sprite on a
  transparent canvas.

  Only single-group sheets are decoded. Three of the 74 D32F sheets in `HotA.lod` declare 16 or 18
  groups, and the layout of the groups after the first has not been established. Group 0's tables
  sit exactly where a single-group sheet keeps them, so reading one of those as single-group would
  look like it worked while silently dropping every later group — `Def32File.Parse` rejects them
  instead. None of them holds anything the overlay uses.

## h3lodmem.cs

Exports the resources HotA adds on top of the SoD archives.

```bash
dotnet run tools/h3lodmem.cs -- verify                     # prove the HotA.lod directory decodes
dotnet run tools/h3lodmem.cs -- names                      # refresh hero-portraits.tsv
dotnet run tools/h3lodmem.cs -- portraits                  # overlay/assets/heroes[/large]/<id>.png
dotnet run tools/h3lodmem.cs -- export <name> <out.png>
dotnet run tools/h3lodmem.cs -- frames <name.def> <outdir> [--first-id N]
dotnet run tools/h3lodmem.cs -- sheet <dir> <out.png> <firstId> <lastId> [cols]
```

Everything reads the archives on disk, with one exception. Hero portraits are addressed by picture
id, and that ordering exists in no archive: `HotA.lod` keeps only name hashes, and nothing in
either archive maps an id to a name. The game holds the mapping as two pointer arrays, so `names`
reads them out of a running game (through `/proc/<pid>/mem`, read-only) and writes
`hero-portraits.tsv` — `id<TAB>small<TAB>large`, one row per picture id. `portraits` and every
later export read that file, so the game is needed only when the table has to be refreshed after a
game update.

`frames` writes `<index + first-id>.png`, skipping any frame that would land before id 0, and
reports how many files it left unchanged, replaced or added.

## h3fonts.cs

Exports `bigfont`, `MedFont`, `smalfont`, `tiny`, `verd10b`, `CALLI10R` from `Data/h3bitmap.lod`
to `overlay/assets/fonts/<lowercase-name>.png` + `.json`: a white, alpha-only glyph atlas and a
JSON table keyed by byte code 0-255 (`x, y, w, h, left, advance`).

```bash
dotnet run tools/h3fonts.cs -- export
dotnet run tools/h3fonts.cs -- render smalfont "Todd  Level 22  1500/1500" out.png
```

`render` is the verification path: it loads only the exported PNG + JSON (never the `.fnt`), lays
the glyphs out left to right (each glyph is blitted at `penX + left`, then `penX += advance` for
the next one) and blits them onto a dark canvas, proving the exported metrics are self-consistent.

### Format notes (`.fnt`, verified byte-for-byte against the files)

- Byte 5 = line height (all glyphs are rasterised at this fixed height, including descenders —
  there is no per-glyph vertical offset in the format).
- 256 × `(int32 left, uint32 width, int32 right)` from byte 32; `advance = left + width + right`.
- 256 × `uint32` pixel offsets right after the metrics table.
- Pixel blob at a **fixed** byte offset 4128 (not file-size-dependent); one byte per pixel, row
  length = glyph width, `pixelOffset[c+1] - pixelOffset[c] == width[c] * lineHeight` (holds for
  all six fonts — `bin length == metricsBase + 256*12 + 256*4 + pixel blob length` exactly, with
  no slack, for every one of them).
- The raw byte **is** the alpha value. `bigfont` uses the full 0-255 range (real antialiasing);
  the other five use only `{0, 1, 255}`, where `1` is the game's own 1px black drop-shadow. Because
  this pipeline exports a single white+alpha atlas, that shadow pixel decodes to alpha ≈ 0.4% —
  effectively invisible, not black. **The native look loses its drop-shadow outline in this
  export.** If that reads as too flat once the popup backgrounds are behind the text, the fix is a
  second render pass in the overlay (e.g. a 1px down-right dark copy drawn before the tinted
  glyph), not a change to this atlas format — baking two colours into "white glyphs with alpha"
  would break the uniform white/yellow/gold tinting.
- Only glyphs with `width > 0` (225-226 of 256 per font) get atlas space; the rest still get a
  JSON entry (`w:0,h:0,x:0,y:0`) so every byte code 0-255 is always a valid lookup.
- Per-font line heights, also printed by `export`: `bigfont` 25, `medfont` 20, `smalfont` 16,
  `tiny` 11, `verd10b` 18, `calli10r` 17.

## h3popups.cs

Exports `HEROQVBK.PCX` / `TOWNQVBK.PCX` from `Data/h3bitmap.lod` to
`overlay/assets/ui/popup-hero.png` / `popup-town.png` (194x186). Both are fully opaque 8bpp-indexed
bitmaps — no palette key colour, no transparency to preserve.

```bash
dotnet run tools/h3popups.cs
```

## h3sprites.cs

DEF explorer/exporter for the plaintext archives, used to regenerate SoD-era sprite sheets once
the resource name is known.

```bash
dotnet run tools/h3sprites.cs -- list <pattern>              # resource names matching pattern
dotnet run tools/h3sprites.cs -- frames <name.def> <outdir>  # one <index>.png per frame
```

`<index>.png` is the frame's position in the DEF's own declared frame list (summed across groups in
declaration order for a multi-group DEF — single-group icon sheets number cleanly from 0). A frame
that fails to decode is skipped with a message on stderr instead of aborting the rest of the export.

`list` covers `h3sprite.lod` and `h3bitmap.lod` only. `HotA.lod` cannot be listed at all, and the
HotA replacement of a sheet is what the game actually draws, so use `h3lodmem.cs frames` for
anything HotA touches.

## Regenerating `overlay/assets/`

Sprite ids in `overlay/assets/` are the game's own ids, which are not always the frame index in
the sheet. The offsets below were established by rendering the SoD sheets and matching them
pixel-for-pixel against known-correct icons.

| Directory | Regenerated by | Frame to id |
|---|---|---|
| `fonts/` | `h3fonts.cs export` | — |
| `ui/popup-hero.png`, `ui/popup-town.png` | `h3popups.cs` | — |
| `heroes/`, `heroes/large/` | `h3lodmem.cs portraits` | by picture id, from `hero-portraits.tsv` |
| `creatures/` | `h3lodmem.cs frames CPRSMALL.def overlay/assets/creatures --first-id -2` | `id = frame - 2` |
| `artifacts/` | `h3lodmem.cs frames Artifact.def overlay/assets/artifacts --first-id 0` | `id = frame` |
| `spells/` | `h3lodmem.cs frames SpellInt.def overlay/assets/spells --first-id -1` | `id = frame - 1` |
| `ui/hall-*.png`, `ui/fort-*.png` | `h3sprites.cs frames hallfort.def <dir>` (or `itmtl.def` — both exist; confirm which one and the frame-to-level mapping before trusting it) | not confirmed |
| `ui/frame-*.png`, `ui/leather.png` | source resource not identified | — |
| `primary/*.png` | `h3sprites.cs frames PSKILL.def <dir>` (candidate; frame-to-stat mapping not confirmed) | not confirmed |
| `skills/*.png` | `h3sprites.cs frames Secskill.def <dir>` (candidate) | not confirmed |
| `towns/*.png` | source resource not identified | — |

Current counts: heroes 245 small and 245 large (picture ids 0-244), creatures 200 (0-199),
artifacts 167 (0-166), spells 91 (0-90).

One asset does not match its slot: picture id 180, `HCVC02.pcx`, decodes to 58x65 where every
other large portrait is 58x64. The extra row carries image data rather than padding, so the export
keeps it as the archive stores it and the overlay is responsible for clipping it to the slot.
