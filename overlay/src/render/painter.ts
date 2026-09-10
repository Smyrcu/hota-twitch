import type { Card, DrawOp } from './display.js';
import type { FontStore } from './fonts.js';
import { PanelPainter } from './panel.js';
import type { SpriteCache } from './sprites.js';
import { alignOffset, layoutText } from './text/layout.js';
import { blitGlyphs } from './text/paint.js';

/** Executes a display list on a canvas, optionally magnified by a whole factor. */
export class CardPainter {
  private readonly panels: PanelPainter;

  constructor(
    private readonly sprites: SpriteCache,
    private readonly fonts: FontStore,
  ) {
    this.panels = new PanelPainter(sprites);
  }

  /**
   * `supersample` magnifies every operation by a whole factor. Sizing a canvas resets its
   * context, so the transform and the smoothing flag are set here, after the caller has sized it.
   */
  paint(context: CanvasRenderingContext2D, card: Card, supersample = 1): void {
    context.setTransform(supersample, 0, 0, supersample, 0, 0);
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
    const layout = layoutText(atlas, op.text, {
      align: op.align,
      ...(op.maxWidth === undefined ? {} : { maxWidth: op.maxWidth }),
      ...(op.maxLines === undefined ? {} : { maxLines: op.maxLines }),
    });
    blitGlyphs(context, sheet, layout.glyphs, op.x + alignOffset(op.align, layout.width), op.y);
  }
}
