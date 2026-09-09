import type { Card } from '../render/display.js';
import type { CardPainter } from '../render/painter.js';
import { placeCard } from './scale.js';
import type { Zone } from '../zones/index.js';

/** The card canvas: drawn at scale 1 and upscaled by CSS so the pixels stay hard-edged. */
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

    show(card: Card, zone: Zone, player: { width: number; height: number }, scale: number): void {
        if (this.context === null) return;
        if (this.canvas.width !== card.width || this.canvas.height !== card.height) {
            this.canvas.width = card.width;
            this.canvas.height = card.height;
        }
        this.painter.paint(this.context, card);

        const size = { width: card.width * scale, height: card.height * scale };
        const at = placeCard(zone.rect, size, player);
        this.canvas.style.left = `${at.left}px`;
        this.canvas.style.top = `${at.top}px`;
        this.canvas.style.width = `${size.width}px`;
        this.canvas.style.height = `${size.height}px`;
        this.canvas.hidden = false;
    }
}
