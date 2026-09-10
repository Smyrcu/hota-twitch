import { FontStore } from '../render/fonts.js';
import { paintLabel, type LabelStyle } from '../render/text/paint.js';

/**
 * How the pages' marked-up text maps onto the game's fonts. `data-font` names one of these on an
 * element whose whole content is text; the native controls keep the system font, because a
 * select cannot be drawn from an atlas and a page of half-drawn widgets reads worse than a
 * consistent one.
 */
export const STYLES = {
  title: { font: 'big', colour: 'yellow' },
  heading: { font: 'medium', colour: 'yellow' },
  body: { font: 'small', colour: 'white' },
  hint: { font: 'small', colour: 'grey' },
  good: { font: 'small', colour: 'green' },
  bad: { font: 'small', colour: 'red' },
} as const satisfies Record<string, LabelStyle>;

export type StyleName = keyof typeof STYLES;

const isStyleName = (value: string): value is StyleName => value in STYLES;

/**
 * The game fonts hold 256 glyphs and no typographic punctuation, so text on its way to a canvas
 * is written the way they can draw it. The document keeps the original characters.
 */
const DRAWABLE: readonly (readonly [RegExp, string])[] = [
  [/\u2026/g, '...'],
  [/[\u2018\u2019]/g, "'"],
  [/[\u201c\u201d]/g, '"'],
  [/[\u2013\u2014]/g, '-'],
];

const drawable = (value: string): string =>
  DRAWABLE.reduce((text, [pattern, plain]) => text.replace(pattern, plain), value);

/**
 * One run of page text drawn with a game font. The words stay in the document for assistive
 * technology; only their appearance moves to a canvas, and if the fonts never arrive the plain
 * text is left exactly where it was.
 */
class Label {
  private readonly canvas = document.createElement('canvas');
  private readonly reader = document.createElement('span');
  private adopted = false;
  private painted = '';

  constructor(
    private readonly element: HTMLElement,
    private text: string,
    private style: LabelStyle,
  ) {
    this.reader.className = 'reader-only';
    this.canvas.setAttribute('aria-hidden', 'true');
  }

  write(text: string, style: LabelStyle): void {
    this.text = text;
    this.style = style;
  }

  paint(fonts: FontStore, supersample: number): void {
    // A hidden or unlaid-out element has no width to wrap into; it repaints when it gets one.
    const width = this.element.clientWidth;
    if (width <= 0) return;
    /*
     * Adopting an element resizes it, and that resize is observed. Repainting the same state is
     * skipped so the two cannot chase each other. This settles a change, not an oscillation: a
     * page whose height straddles the viewport can toggle a scrollbar and alternate between two
     * widths, which the browser stops on its own.
     */
    const state = [width, supersample, this.style.font, this.style.colour, this.text].join('\u0000');
    if (state === this.painted) return;

    this.reader.textContent = this.text;
    if (!paintLabel(this.canvas, fonts, drawable(this.text), this.style, width, supersample)) {
      // Without the fonts the element keeps its own text, so it is only written when it changed.
      if (!this.adopted && this.element.textContent !== this.text) this.element.textContent = this.text;
      return;
    }
    this.painted = state;
    // Empty text would still take a line of its own, so nothing is put in its place.
    this.canvas.hidden = this.text === '';
    if (this.adopted) return;
    this.element.replaceChildren(this.reader, this.canvas);
    this.adopted = true;
  }
}

/** The page's text, drawn in the game's fonts and kept that way as it changes or reflows. */
export class Typography {
  private readonly fonts = new FontStore();
  private readonly labels = new Map<HTMLElement, Label>();

  /** Adopts every `[data-font]` element under `root`. Silently does nothing without the fonts. */
  async start(root: ParentNode): Promise<void> {
    await this.fonts.load();
    for (const element of root.querySelectorAll<HTMLElement>('[data-font]')) {
      const name = element.dataset['font'] ?? '';
      if (!isStyleName(name)) continue;
      this.labels.set(element, new Label(element, element.textContent ?? '', STYLES[name]));
    }
    this.paint();
    // An observer with observation targets stays alive on its own, so nothing has to hold it.
    const observer = new ResizeObserver(() => this.paint());
    for (const element of this.labels.keys()) observer.observe(element);
  }

  /** Replaces the text of an adopted element, optionally in a different style. */
  write(element: HTMLElement, text: string, style?: StyleName): void {
    const label = this.labels.get(element);
    if (label === undefined) {
      element.textContent = text;
      return;
    }
    const named = style ?? (element.dataset['font'] ?? '');
    label.write(text, isStyleName(named) ? STYLES[named] : STYLES.body);
    label.paint(this.fonts, this.supersample());
  }

  private paint(): void {
    const supersample = this.supersample();
    for (const label of this.labels.values()) label.paint(this.fonts, supersample);
  }

  /** Whole multiple, so the atlas is never sampled at a fraction of a pixel. */
  private supersample(): number {
    const ratio = window.devicePixelRatio;
    return Number.isFinite(ratio) && ratio > 1 ? Math.ceil(ratio) : 1;
  }
}
