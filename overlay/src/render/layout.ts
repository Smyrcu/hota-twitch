/**
 * Card geometry, in the popup's own pixels (the card is drawn at scale 1 and upscaled whole).
 *
 * The hover card is the game's quick-view popup bitmap, `assets/ui/popup-hero.png` and
 * `popup-town.png` (194x186, exported from `HEROQVBK.PCX` / `TOWNQVBK.PCX`). Those bitmaps
 * carry the drawn frames of every field and, on the hero popup, the four primary-skill icons,
 * so the numbers below were measured off them: the content sits inside the gold frame at
 * x 9..184, y 10..177, and each field rectangle is the recessed box drawn in the bitmap.
 */
export const CARD_WIDTH = 194;
export const CARD_HEIGHT = 186;

/** Content area inside the frame drawn in the popup bitmap. */
export const INSET = 9;
export const CONTENT_WIDTH = CARD_WIDTH - INSET * 2;

/** Top-left box: hero portrait or town picture, both 58x64 sprite sets. */
export const PORTRAIT = { x: 10, y: 12, width: 58, height: 64 } as const;

/** Wide box to the right of the picture. */
export const HEADER = { x: 73, nameY: 12, lineY: 34, width: 110 } as const;

/**
 * The hero popup draws the four primary-skill icons itself, so only the values are placed,
 * centred under each icon.
 */
export const PRIMARY_ROW = { y: 60, first: 85, cell: 28, count: 4 } as const;

/** The popup draws the spell-point icon; the value goes under it. */
export const MANA_FIELD = { centre: 167, y: 102 } as const;

/**
 * The seven army slots as the popup frames them: three boxes above four below. `x` is the left
 * edge of the first box and `cell` the pitch; `y` and `icon` are the box interior; `count` is the
 * strip of bare leather under the row.
 *
 * All of it was read off the bitmap's brightness profile, where every frame runs shadow,
 * highlight, falloff: the upper boxes hold y 85..116 and their frame ends at y 118, the lower
 * boxes hold y 133..164 inside a frame that starts at y 130, and the golden border closes the
 * content at y 178. That leaves two strips of leather, y 119..129 and y 167..177, each exactly
 * one `tiny` line tall — which is where the counts go, clear of every frame the bitmap draws.
 */
export const ARMY_ROWS = [
  { y: 85, x: 45, cell: 36, slots: 3, icon: 32, count: { top: 119, bottom: 130 } },
  { y: 133, x: 27, cell: 36, slots: 4, icon: 32, count: { top: 167, bottom: 178 } },
] as const;

/** Hall and fort icons on the town popup, in the boxes right of the picture. */
export const BUILDING_ROW = { y: 43, x: 74, icon: 34, gap: 38 } as const;

/**
 * Sections of the click expansion, drawn below the hover card at the same width on the game's
 * dialog box. Its frame is thicker than the popup's (14 px at the sides, 15 top and bottom),
 * so the content is inset past it.
 */
export const EXPANSION = {
  inset: 16,
  paddingTop: 18,
  paddingBottom: 20,
  headingHeight: 18,
  lineHeight: 18,
  skill: { cell: 40, icon: 32, perRow: 4 },
  equipped: { cell: 52, icon: 44, perRow: 3 },
  backpack: { cell: 26, icon: 24, perRow: 6 },
  spell: { cell: 52, iconWidth: 48, iconHeight: 36, perRow: 3 },
} as const;

/** Width available to the expansion content, inside the dialog frame. */
export const EXPANSION_WIDTH = CARD_WIDTH - EXPANSION.inset * 2;

/** Left edge of the sprite inside its cell. */
export function cellIconX(origin: number, cell: number, icon: number, index: number): number {
  return origin + cell * index + Math.floor((cell - icon) / 2);
}

export interface ArmySlot {
  readonly x: number;
  readonly y: number;
  /** Horizontal middle of the box, where the count is centred. */
  readonly centre: number;
  readonly icon: number;
  readonly countY: number;
}

/** Where army slot `slot` (0..6) sits: the popup frames three boxes above four. */
export function armySlot(slot: number): ArmySlot | null {
  let remaining = slot;
  for (const row of ARMY_ROWS) {
    if (remaining < row.slots) {
      const x = row.x + row.cell * remaining;
      return { x, y: row.y, centre: x + row.icon / 2, icon: row.icon, countY: row.count.top };
    }
    remaining -= row.slots;
  }
  return null;
}
