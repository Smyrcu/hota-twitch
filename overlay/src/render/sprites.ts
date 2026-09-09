const RETRY_AFTER_MS = 60_000;

interface Entry {
    readonly image: HTMLImageElement;
    ready: boolean;
    failedAt: number;
}

/**
 * Loads sprites once and keeps them. A sprite that fails to load is remembered so the card
 * draws an empty slot instead of asking for it again on every repaint; the request is retried
 * at most once a minute, which is what a mid-game asset update needs.
 */
export class SpriteCache {
    private readonly entries = new Map<string, Entry>();

    constructor(
        private readonly onLoaded: () => void,
        private readonly now: () => number = () => Date.now(),
    ) {}

    /** The image when it is ready to draw, null while it loads or after it failed. */
    get(src: string): HTMLImageElement | null {
        const existing = this.entries.get(src);
        if (existing !== undefined) {
            if (existing.ready) return existing.image;
            if (existing.failedAt === 0 || this.now() - existing.failedAt < RETRY_AFTER_MS) return null;
            this.entries.delete(src);
        }
        this.load(src);
        return null;
    }

    private load(src: string): void {
        const image = new Image();
        const entry: Entry = { image, ready: false, failedAt: 0 };
        this.entries.set(src, entry);
        image.addEventListener('load', () => {
            entry.ready = image.naturalWidth > 0;
            entry.failedAt = entry.ready ? 0 : this.now();
            this.onLoaded();
        });
        image.addEventListener('error', () => {
            entry.failedAt = this.now();
            this.onLoaded();
        });
        image.src = src;
    }
}
