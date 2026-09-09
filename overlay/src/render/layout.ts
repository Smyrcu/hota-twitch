/**
 * Card geometry, in the popup's own pixels (the card is drawn at scale 1 and upscaled whole).
 *
 * The hover card is the size of the game's quick-view popup bitmaps, `HEROQVBK.PCX` and
 * `TOWNQVBK.PCX` (194x186). The field positions below are provisional: they are derived from
 * the native sizes of the sprite sets in `assets/`, because the popup bitmaps that carry the
 * drawn frames have not been exported yet. When `assets/ui/popup-hero.png` and `popup-town.png`
 * land, measure the frames in them and correct this module — nothing else holds coordinates.
 *
 * The army row is the one place where the popup size and the sprite size disagree: seven slots
 * of the native 32x32 creature icon need 224px, which does not fit in a 194px popup, so army
 * icons are drawn into 24x24 boxes. See the report for the note raised with the coordinator.
 */
export const CARD_WIDTH = 194;
export const CARD_HEIGHT = 186;

/** Distance from the card edge to the content, inside the popup's drawn border. */
export const INSET = 6;
export const CONTENT_WIDTH = CARD_WIDTH - INSET * 2;

export const PORTRAIT = { x: INSET, y: INSET, width: 58, height: 64 } as const;

/** Text column to the right of the portrait. */
export const HEADER = {
  x: PORTRAIT.x + PORTRAIT.width + 6,
  nameY: INSET + 2,
  lineY: INSET + 28,
  subLineY: INSET + 42,
  get width(): number {
    return CARD_WIDTH - this.x - INSET;
  },
} as const;

export const PRIMARY_ROW = { y: 78, cell: 44, icon: 32, valueY: 112, count: 4 } as const;
export const ARMY_ROW = { y: 130, cell: 26, icon: 24, countY: 156, slots: 7 } as const;

export const BUILDING_ROW = { y: INSET + 54, icon: 38, gap: 6 } as const;

/**
 * Sections of the click expansion, drawn below the hover card at the same width on the game's
 * dialog box. Its frame is thicker than the popup's (14 px at the sides, 15 top and bottom),
 * so the content is inset past it.
 */
export const EXPANSION = {
  inset: 16,
  paddingTop: 18,
  paddingBottom: 20,
  headingHeight: 14,
  lineHeight: 14,
  skill: { cell: 40, icon: 32, perRow: 4 },
  equipped: { cell: 52, icon: 44, perRow: 3 },
  backpack: { cell: 26, icon: 24, perRow: 6 },
  spell: { cell: 52, iconWidth: 48, iconHeight: 36, perRow: 3 },
} as const;

/** Width available to the expansion content, inside the dialog frame. */
export const EXPANSION_WIDTH = CARD_WIDTH - EXPANSION.inset * 2;

/** Left edge of a row of `count` cells of `cell` width, centred in the content column. */
export function rowOrigin(cell: number, count: number): number {
  return INSET + Math.floor((CONTENT_WIDTH - cell * count) / 2);
}

/** Left edge of the sprite inside its cell. */
export function cellIconX(origin: number, cell: number, icon: number, index: number): number {
  return origin + cell * index + Math.floor((cell - icon) / 2);
}
