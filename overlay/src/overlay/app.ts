import { DEV_FONT_ROOT, FONT_ROOT } from '../data/fonts.js';
import { readParams, type OverlayParams } from '../dev/params.js';
import { pollMock } from '../dev/mock.js';
import { cardFor, type Selection } from '../render/card.js';
import { FontStore } from '../render/fonts.js';
import { CardPainter } from '../render/painter.js';
import { SpriteCache } from '../render/sprites.js';
import type { GameState } from '../state/protocol.js';
import { parseDisplayResolution, twitchExt } from '../twitch/ext.js';
import { BroadcastReceiver } from '../twitch/receiver.js';
import { computeZones, containFit, type Zone } from '../zones/index.js';
import { CardView } from './card-view.js';
import { advance, matches, selectionFor } from './interaction.js';
import { cardScale, fitScale } from './scale.js';
import { ZoneLayer } from './zone-layer.js';

interface Size {
    width: number;
    height: number;
}

export class OverlayApp {
    private state: GameState | null = null;
    private selection: Selection | null = null;
    private hoveredZone: Zone | null = null;
    private zones: readonly Zone[] = [];
    private player: Size = { width: 0, height: 0 };
    private frame = 0;

    private readonly sprites = new SpriteCache(() => this.scheduleDraw());
    private readonly fonts = new FontStore();
    private readonly countLines = (value: string, maxWidth: number): number =>
        this.fonts.lineCounter('small')(value, maxWidth);
    private readonly layer: ZoneLayer;
    private readonly view: CardView;
    private readonly status: HTMLElement | null;

    constructor(
        private readonly root: HTMLElement,
        private readonly params: OverlayParams,
    ) {
        this.layer = new ZoneLayer(root, {
            onEnter: (zone) => this.enter(zone),
            onLeave: (zone) => this.leave(zone),
            onClick: (zone) => this.click(zone),
        });
        this.view = new CardView(root, new CardPainter(this.sprites, this.fonts));
        this.status = params.debug ? this.createStatus() : null;
        if (params.background !== null) root.classList.add('calibration');
        if (params.background !== null) root.style.backgroundImage = `url("${params.background}")`;
    }

    async start(): Promise<void> {
        await this.fonts.load(this.params.mock ? [FONT_ROOT, DEV_FONT_ROOT] : [FONT_ROOT]);
        this.measure();
        window.addEventListener('resize', () => this.measure());
        if (this.params.mock) this.startMock();
        else this.startTwitch();
        this.scheduleDraw();
    }

    private createStatus(): HTMLElement {
        const element = document.createElement('div');
        element.className = 'status';
        this.root.appendChild(element);
        return element;
    }

    private startMock(): void {
        pollMock(
            (state) => this.apply(state),
            (reason) => this.report(reason),
        );
    }

    private startTwitch(): void {
        const ext = twitchExt();
        if (ext === null) {
            this.report('waiting for the Twitch extension helper');
            return;
        }
        const receiver = new BroadcastReceiver(
            (state) => this.apply(state),
            (reason) => this.report(reason),
        );
        ext.onContext((context) => {
            if (context.hlsLatencyBroadcaster !== undefined) {
                receiver.setLatencySeconds(context.hlsLatencyBroadcaster);
            }
            const resolution = parseDisplayResolution(context.displayResolution);
            if (resolution !== null && this.player.width === 0) this.setPlayer(resolution);
        });
        ext.listen('broadcast', (_target, _contentType, message) => void receiver.handle(message));
    }

    private apply(state: GameState): void {
        this.state = state;
        this.scheduleDraw();
    }

    private report(reason: string): void {
        if (this.status !== null) this.status.textContent = reason;
    }

    private measure(): void {
        this.setPlayer({ width: window.innerWidth, height: window.innerHeight });
    }

    private setPlayer(size: Size): void {
        this.player = size;
        this.scheduleDraw();
    }

    private enter(zone: Zone): void {
        this.hoveredZone = zone;
        if (this.selection === null || !matches(this.selection, zone)) this.selection = selectionFor(zone);
        this.scheduleDraw();
    }

    private leave(zone: Zone): void {
        if (this.hoveredZone !== null && matches(selectionFor(this.hoveredZone), zone)) this.hoveredZone = null;
        this.selection = null;
        this.scheduleDraw();
    }

    private click(zone: Zone): void {
        const current = this.selection !== null && matches(this.selection, zone) ? this.selection : selectionFor(zone);
        const town = zone.kind === 'town' ? (this.state?.towns[zone.entry] ?? null) : null;
        this.selection = advance(current, town);
        this.hoveredZone = zone;
        this.scheduleDraw();
    }

    private scheduleDraw(): void {
        if (this.frame !== 0) return;
        this.frame = requestAnimationFrame(() => {
            this.frame = 0;
            this.draw();
        });
    }

    private draw(): void {
        const state = this.state;
        if (state === null || this.player.width === 0) {
            this.layer.render([], this.params.debug);
            this.view.hide();
            return;
        }

        this.zones = computeZones(state, this.player);
        this.layer.render(this.zones, this.params.debug);
        this.drawStatus(state);

        const zone = this.currentZone();
        const card =
            this.selection === null || zone === null ? null : cardFor(state, this.selection, this.countLines);
        if (card === null || zone === null) {
            this.view.hide();
            return;
        }
        const fit = containFit(state.display, this.player);
        const scale = cardScale(state.display.uiScale, fit.scale);
        this.view.show(card, zone, this.player, fitScale(scale, card.height, this.player.height));
    }

    /** The live zone for the selection, so the card follows a resize or a list scroll. */
    private currentZone(): Zone | null {
        const selection = this.selection;
        if (selection === null) return null;
        return this.zones.find((zone) => matches(selection, zone)) ?? null;
    }

    private drawStatus(state: GameState): void {
        if (this.status === null) return;
        const age = Math.round((Date.now() - state.ts) / 1000);
        this.status.textContent =
            `${state.screen} · ${state.display.width}x${state.display.height} @${state.display.uiScale} · ` +
            `player ${this.player.width}x${this.player.height} · ${this.zones.length} zones · ${age}s`;
    }
}

export function startOverlay(root: HTMLElement): void {
    const app = new OverlayApp(root, readParams(window.location.search));
    void app.start();
}
