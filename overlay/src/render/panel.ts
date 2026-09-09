import type { PanelKind } from './display.js';
import type { SpriteCache } from './sprites.js';

/** The quick-view popup bitmaps, once `tools/` exports them. */
const POPUP_SOURCES: Readonly<Record<PanelKind, string | null>> = {
  hero: 'assets/ui/popup-hero.png',
  town: 'assets/ui/popup-town.png',
  expansion: null,
};

const LEATHER = 'assets/ui/leather.png';
const CORNER = 64;
const EDGE_H = 15;
const EDGE_V = 14;

const FRAME = {
  topLeft: 'assets/ui/frame-tl.png',
  topRight: 'assets/ui/frame-tr.png',
  bottomLeft: 'assets/ui/frame-bl.png',
  bottomRight: 'assets/ui/frame-br.png',
  top: 'assets/ui/frame-t.png',
  bottom: 'assets/ui/frame-b.png',
  left: 'assets/ui/frame-l.png',
  right: 'assets/ui/frame-r.png',
} as const;

function tile(
  context: CanvasRenderingContext2D,
  image: CanvasImageSource,
  x: number,
  y: number,
  width: number,
  height: number,
  tileWidth: number,
  tileHeight: number,
): void {
  if (width <= 0 || height <= 0) return;
  context.save();
  context.beginPath();
  context.rect(x, y, width, height);
  context.clip();
  for (let dy = 0; dy < height; dy += tileHeight) {
    for (let dx = 0; dx < width; dx += tileWidth) {
      context.drawImage(image, x + dx, y + dy, tileWidth, tileHeight);
    }
  }
  context.restore();
}

/**
 * Draws the background of a card section. The hover card uses the game's own popup bitmap when
 * it is available; until then, and always for the click expansion, it uses the game's dialog
 * box: the `DIBOX128` leather fill inside the `dialgbox.def` frame.
 */
export class PanelPainter {
  constructor(private readonly sprites: SpriteCache) {}

  draw(context: CanvasRenderingContext2D, kind: PanelKind, x: number, y: number, width: number, height: number): void {
    const popup = POPUP_SOURCES[kind];
    if (popup === null) {
      this.drawDialog(context, x, y, width, height);
      return;
    }
    const bitmap = this.sprites.get(popup);
    if (bitmap !== null) context.drawImage(bitmap, x, y, width, height);
    else this.drawPopup(context, x, y, width, height);
  }

  /**
     * Stand-in for a quick-view popup bitmap: the same leather inside the thin bevel the game
     * draws around a popup, so the field positions of the card still hold.
     */
  private drawPopup(context: CanvasRenderingContext2D, x: number, y: number, width: number, height: number): void {
    this.fillLeather(context, x, y, width, height);
    context.lineWidth = 1;
    context.strokeStyle = '#1a1005';
    context.strokeRect(x + 0.5, y + 0.5, width - 1, height - 1);
    context.strokeStyle = '#8a6a34';
    context.strokeRect(x + 2.5, y + 2.5, width - 5, height - 5);
  }

  private fillLeather(context: CanvasRenderingContext2D, x: number, y: number, width: number, height: number): void {
    const leather = this.sprites.get(LEATHER);
    if (leather !== null) {
      tile(context, leather, x, y, width, height, leather.naturalWidth, leather.naturalHeight);
    } else {
      context.fillStyle = '#3b2d1c';
      context.fillRect(x, y, width, height);
    }
  }

  private drawDialog(context: CanvasRenderingContext2D, x: number, y: number, width: number, height: number): void {
    this.fillLeather(context, x, y, width, height);

    const right = x + width;
    const bottom = y + height;
    const innerWidth = width - CORNER * 2;
    const innerHeight = height - CORNER * 2;

    this.edge(context, FRAME.top, x + CORNER, y, innerWidth, EDGE_H, CORNER, EDGE_H);
    this.edge(context, FRAME.bottom, x + CORNER, bottom - EDGE_H, innerWidth, EDGE_H, CORNER, EDGE_H);
    this.edge(context, FRAME.left, x, y + CORNER, EDGE_V, innerHeight, EDGE_V, CORNER);
    this.edge(context, FRAME.right, right - EDGE_V, y + CORNER, EDGE_V, innerHeight, EDGE_V, CORNER);

    this.corner(context, FRAME.topLeft, x, y);
    this.corner(context, FRAME.topRight, right - CORNER, y);
    this.corner(context, FRAME.bottomLeft, x, bottom - CORNER);
    this.corner(context, FRAME.bottomRight, right - CORNER, bottom - CORNER);
  }

  private edge(
    context: CanvasRenderingContext2D,
    src: string,
    x: number,
    y: number,
    width: number,
    height: number,
    tileWidth: number,
    tileHeight: number,
  ): void {
    const image = this.sprites.get(src);
    if (image !== null) tile(context, image, x, y, width, height, tileWidth, tileHeight);
  }

  private corner(context: CanvasRenderingContext2D, src: string, x: number, y: number): void {
    const image = this.sprites.get(src);
    if (image !== null) context.drawImage(image, x, y);
  }
}
