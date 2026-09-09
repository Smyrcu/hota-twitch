import type { FontId, TextColour } from '../data/fonts.js';
import type { TextAlign } from './text/layout.js';

/**
 * The background a card section is drawn on: the game's quick-view popup bitmaps for the hover
 * card, and the dialog box (leather fill plus `dialgbox.def` frame) for the click expansion.
 */
export type PanelKind = 'hero' | 'town' | 'expansion';

export type DrawOp =
    | { readonly kind: 'panel'; readonly panel: PanelKind; readonly x: number; readonly y: number; readonly width: number; readonly height: number }
    | { readonly kind: 'sprite'; readonly src: string; readonly x: number; readonly y: number; readonly width: number; readonly height: number }
    | { readonly kind: 'fill'; readonly x: number; readonly y: number; readonly width: number; readonly height: number; readonly colour: string }
    | {
          readonly kind: 'text';
          readonly text: string;
          readonly font: FontId;
          readonly colour: TextColour;
          readonly x: number;
          readonly y: number;
          readonly align: TextAlign;
          readonly maxWidth?: number;
      };

/**
 * What a card looks like, as data. The layout pass produces it from the state; the painter
 * executes it on a canvas. Keeping the two apart makes the layout testable without a canvas.
 */
export interface Card {
    readonly width: number;
    readonly height: number;
    readonly ops: readonly DrawOp[];
}

export function sprite(src: string, x: number, y: number, width: number, height: number): DrawOp {
    return { kind: 'sprite', src, x, y, width, height };
}

export function text(
    value: string,
    font: FontId,
    colour: TextColour,
    x: number,
    y: number,
    align: TextAlign = 'left',
    maxWidth?: number,
): DrawOp {
    return maxWidth === undefined
        ? { kind: 'text', text: value, font, colour, x, y, align }
        : { kind: 'text', text: value, font, colour, x, y, align, maxWidth };
}
