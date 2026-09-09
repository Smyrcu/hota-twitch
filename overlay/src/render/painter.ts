import type { Card, DrawOp } from './display.js';
import type { FontStore } from './fonts.js';
import { PanelPainter } from './panel.js';
import type { SpriteCache } from './sprites.js';
import { layoutText, type TextAlign } from './text/layout.js';

function anchorOffset(align: TextAlign, width: number): number {
  if (align === 'center') return -Math.round(width / 2);
  if (align === 'right') return -width;
  return 0;
}

/** Executes a display list on a canvas at scale 1; the canvas itself is upscaled by CSS. */
export class CardPainter {
  private readonly panels: PanelPainter;

  constructor(
    private readonly sprites: SpriteCache,
    private readonly fonts: FontStore,
  ) {
    this.panels = new PanelPainter(sprites);
  }

  paint(context: CanvasRenderingContext2D, card: Card): void {
    context.imageSmoothingEnabled = false;
    context.clearRect(0, 0, card.width, card.height);
    for (const op of card.ops) this.apply(context, op);
  }

  private apply(context: CanvasRenderingContext2D, op: DrawOp): void {
    switch (op.kind) {
      case 'panel':
        this.panels.draw(context, op.panel, op.x, op.y, op.width, op.height);
        return;
      case 'sprite': {
        const image = this.sprites.get(op.src);
        if (image !== null) context.drawImage(image, op.x, op.y, op.width, op.height);
        return;
      }
      case 'fill':
        context.fillStyle = op.colour;
        context.fillRect(op.x, op.y, op.width, op.height);
        return;
      case 'text':
        this.drawText(context, op);
        return;
    }
  }

  private drawText(context: CanvasRenderingContext2D, op: Extract<DrawOp, { kind: 'text' }>): void {
    const atlas = this.fonts.atlas(op.font);
    const sheet = this.fonts.sheet(op.font, op.colour);
    if (atlas === null || sheet === null) return;
    const layout = layoutText(
      atlas,
      op.text,
      op.maxWidth === undefined ? { align: op.align } : { align: op.align, maxWidth: op.maxWidth },
    );
    const originX = op.x + anchorOffset(op.align, layout.width);
    for (const glyph of layout.glyphs) {
      context.drawImage(
        sheet,
        glyph.sx,
        glyph.sy,
        glyph.sw,
        glyph.sh,
        originX + glyph.dx,
        op.y + glyph.dy,
        glyph.sw,
        glyph.sh,
      );
    }
  }
}
