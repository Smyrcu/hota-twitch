import type { Card } from '../render/display.js';
import type { CardPainter } from '../render/painter.js';
import { placeCard, supersampleFactor } from './scale.js';
import type { Rect, Size, Zone } from '../zones/index.js';

/**
 * The card canvas. It is rasterised at a whole multiple of the size it will occupy on the
 * viewer's screen and presented at the exact fractional size the video calls for, so the browser
 * scales the finished card the same way it scales the stream.
 */
export class CardView {
  private readonly canvas: HTMLCanvasElement;
  private readonly context: CanvasRenderingContext2D | null;

  constructor(
    root: HTMLElement,
    private readonly painter: CardPainter,
  ) {
    this.canvas = document.createElement('canvas');
    this.canvas.className = 'card';
    this.canvas.hidden = true;
    root.appendChild(this.canvas);
    this.context = this.canvas.getContext('2d');
  }

  hide(): void {
    this.canvas.hidden = true;
  }

  show(card: Card, zone: Zone, video: Rect, scale: number): void {
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
    const at = placeCard(zone.rect, size, video);
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
