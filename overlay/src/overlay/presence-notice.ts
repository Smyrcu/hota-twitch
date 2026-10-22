import { noticeCard, type TextWidth } from '../render/notice.js';
import type { CardPainter } from '../render/painter.js';
import type { Presence } from '../state/presence.js';
import type { Display, GameState } from '../state/protocol.js';
import { containFit, videoRect, type Rect, type Size } from '../zones/index.js';
import { CardView } from './card-view.js';
import { PresenceWatch } from './presence.js';
import { noticeScale, placeNotice } from './scale.js';

/** Distance from the corner of the picture, in notice pixels. */
const MARGIN = 12;

/** The shape of the picture before a state has said what it is: a stream is almost always 16:9. */
const ASSUMED_PICTURE: Size = { width: 1920, height: 1080 };

/**
 * The notice that tells a viewer on arrival whether the overlay is live on this stream. It fades in
 * once the verdict is known and fades out on its own, or as soon as the viewer reaches the panel.
 */
export class PresenceNotice {
  private presence: Presence | null = null;
  private display: Display | null = null;
  private painted = '';
  private readonly view: CardView;
  private readonly watch: PresenceWatch;

  constructor(
    root: HTMLElement,
    painter: CardPainter,
    private readonly widthOf: TextWidth,
    private readonly redraw: () => void,
  ) {
    this.view = new CardView(root, painter, 'notice');
    this.view.onTransitionEnd(() => {
      if (this.presence === null) this.view.hide();
    });
    this.watch = new PresenceWatch({ show: (presence) => this.show(presence), hide: () => this.fade() });
  }

  start(): void {
    this.watch.start();
  }

  received(state: GameState): void {
    this.display = state.display;
    this.watch.received(state);
  }

  dismiss(): void {
    this.watch.dismiss();
  }

  stop(): void {
    this.watch.stop();
    this.view.hide();
  }

  /**
   * Repaints only when what the notice shows has changed: the page redraws on every state and
   * every pointer move over the panel, and the notice is the same through almost all of them.
   * A sprite that finishes loading repaints through `invalidate`.
   */
  draw(player: Size): void {
    if (this.presence === null || player.width === 0) return;
    const picture = this.picture(player);
    const key = [this.presence, picture.x, picture.y, picture.width, picture.height].join(' ');
    if (key === this.painted) return;
    this.painted = key;
    const card = noticeCard(this.presence, this.widthOf);
    const scale = noticeScale(card, picture);
    this.view.present(card, scale, (size) => placeNotice(size, picture, MARGIN * scale));
  }

  /** Forgets the last paint, for when an image the notice draws has arrived. */
  invalidate(): void {
    this.painted = '';
  }

  private picture(player: Size): Rect {
    const game = this.display ?? ASSUMED_PICTURE;
    return videoRect(game, containFit(game, player));
  }

  private show(presence: Presence): void {
    this.presence = presence;
    this.painted = '';
    this.view.toggleClass('gone', false);
    this.view.toggleClass('inactive', presence === 'inactive');
    this.redraw();
  }

  private fade(): void {
    this.presence = null;
    this.view.toggleClass('gone', true);
  }
}
