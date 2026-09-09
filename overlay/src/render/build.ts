import type { TextColour } from '../data/fonts.js';
import { text, type DrawOp, type PanelKind } from './display.js';
import { EXPANSION, EXPANSION_WIDTH } from './layout.js';

/**
 * How many lines a string takes in the expansion's font once it is wrapped. The card layout is
 * pure, so it is told rather than measuring: the page passes a counter backed by the loaded
 * atlas, and the default assumes one line, which is what the fixtures use.
 */
export type LineCounter = (value: string, maxWidth: number) => number;

export const SINGLE_LINE: LineCounter = () => 1;

/** Accumulates draw operations while tracking the vertical cursor of a stacked section. */
export class SectionBuilder {
  private readonly ops: DrawOp[] = [];

  constructor(
    private cursor: number,
    private readonly countLines: LineCounter = SINGLE_LINE,
  ) {}

  get y(): number {
    return this.cursor;
  }

  push(...ops: readonly DrawOp[]): void {
    this.ops.push(...ops);
  }

  advance(by: number): void {
    this.cursor += by;
  }

  heading(label: string): void {
    this.write(label, 'yellow', EXPANSION.headingHeight);
  }

  line(label: string): void {
    this.write(label, 'white', EXPANSION.lineHeight);
  }

  note(label: string): void {
    this.write(label, 'grey', EXPANSION.lineHeight);
  }

  private write(label: string, colour: TextColour, lineHeight: number): void {
    this.push(text(label, 'small', colour, EXPANSION.inset, this.cursor, 'left', EXPANSION_WIDTH));
    this.cursor += Math.max(1, this.countLines(label, EXPANSION_WIDTH)) * lineHeight;
  }

  /** Lays out a grid of equally sized cells and returns the position of each. */
  grid(count: number, perRow: number, cell: number, cellHeight: number): { x: number; y: number }[] {
    const cells: { x: number; y: number }[] = [];
    for (let index = 0; index < count; index += 1) {
      cells.push({
        x: EXPANSION.inset + (index % perRow) * cell,
        y: this.cursor + Math.floor(index / perRow) * cellHeight,
      });
    }
    if (count > 0) this.cursor += Math.ceil(count / perRow) * cellHeight;
    return cells;
  }

  finish(panel: PanelKind, width: number, top: number): { ops: readonly DrawOp[]; height: number } {
    const height = this.cursor - top + EXPANSION.paddingBottom;
    return {
      ops: [{ kind: 'panel', panel, x: 0, y: top, width, height }, ...this.ops],
      height,
    };
  }
}
