import type { Card } from '../render/display.js';
import type { CardPainter } from '../render/painter.js';
import { placeCard, supersampleFactor, type Placement } from './scale.js';
import type { Rect, Size, Zone } from '../zones/index.js';

/**
 * A canvas presenting a `Card`: a hover card, or the notice. It is rasterised at a whole multiple of the size it will occupy on the
 * viewer's screen and presented at the exact fractional size the video calls for, so the browser
 * scales the finished card the same way it scales the stream.
 */
export class CardView {
  private readonly canvas: HTMLCanvasElement;
  private readonly context: CanvasRenderingContext2D | null;

  constructor(
    root: HTMLElement,
    private readonly painter: CardPainter,
    className = 'card',
  ) {
    this.canvas = document.createElement('canvas');
    this.canvas.className = className;
    this.canvas.hidden = true;
    root.appendChild(this.canvas);
    this.context = this.canvas.getContext('2d');
  }

  hide(): void {
    this.canvas.hidden = true;
  }

  toggleClass(name: string, on: boolean): void {
    this.canvas.classList.toggle(name, on);
  }

  onTransitionEnd(listener: () => void): void {
    this.canvas.addEventListener('transitionend', listener);
  }

  /** Presents the card beside the panel entry it belongs to. */
  show(card: Card, zone: Zone, video: Rect, scale: number): void {
    this.present(card, scale, (size) => placeCard(zone.rect, size, video));
  }

  /** Presents the card at `scale` wherever `place` puts a card of its presented size. */
  present(card: Card, scale: number, place: (size: Size) => Placement): void {
    if (this.context === null) return;
    // A dense display shows more pixels than the CSS size names, so it is rasterised for them.
    const density = pixelRatio();
    const supersample = supersampleFactor(scale * density);
    const pixels: Size = { width: card.width * supersample, height: card.height * supersample };
    if (this.canvas.width !== pixels.width || this.canvas.height !== pixels.height) {
      this.canvas.width = pixels.width;
      this.canvas.height = pixels.height;
    }
    this.painter.paint(this.context, card, supersample);

    const size: Size = { width: card.width * scale, height: card.height * scale };
    const at = place(size);
    // On a whole device pixel, so the downscale is not softened further by a half-pixel phase.
    this.canvas.style.left = `${snap(at.left, density)}px`;
    this.canvas.style.top = `${snap(at.top, density)}px`;
    this.canvas.style.width = `${size.width}px`;
    this.canvas.style.height = `${size.height}px`;
    this.canvas.hidden = false;
  }
}

function pixelRatio(): number {
  const ratio = window.devicePixelRatio;
  return Number.isFinite(ratio) && ratio > 0 ? ratio : 1;
}

const snap = (value: number, density: number): number => Math.round(value * density) / density;
