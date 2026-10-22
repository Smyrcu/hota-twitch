import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { NOTICE_VISIBLE_MS, PRESENCE_WAIT_MS, PresenceWatch } from '../src/overlay/presence.js';
import { noticeScale, placeNotice } from '../src/overlay/scale.js';
import { noticeCard } from '../src/render/notice.js';
import { presenceOf, type Presence } from '../src/state/presence.js';
import type { GameState } from '../src/state/protocol.js';
import { MOCK_STATE } from './fixtures/state.js';

const NO_GAME: GameState = { ...MOCK_STATE, screen: 'none', player: null, heroes: [], towns: [] };
const IN_COMBAT: GameState = { ...MOCK_STATE, screen: 'combat' };

function watch(): { watch: PresenceWatch; shown: Presence[]; hidden: () => number } {
  const shown: Presence[] = [];
  let hides = 0;
  const presence = new PresenceWatch({ show: (value) => shown.push(value), hide: () => (hides += 1) });
  return { watch: presence, shown, hidden: () => hides };
}

describe('presence of a state', () => {
  it('is active on the adventure map, paused on another screen and unknown with no game', () => {
    expect(presenceOf(MOCK_STATE)).toBe('active');
    expect(presenceOf(IN_COMBAT)).toBe('paused');
    expect(presenceOf(NO_GAME)).toBeNull();
  });
});

describe('presence watch', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('calls the overlay active as soon as a loaded game is heard', () => {
    const { watch: presence, shown } = watch();
    presence.start();

    vi.advanceTimersByTime(PRESENCE_WAIT_MS - 1);
    presence.received(MOCK_STATE);

    expect(shown).toEqual(['active']);
  });

  it('calls the overlay inactive when nothing is heard by the deadline', () => {
    const { watch: presence, shown } = watch();
    presence.start();

    vi.advanceTimersByTime(PRESENCE_WAIT_MS - 1);
    expect(shown).toEqual([]);

    vi.advanceTimersByTime(1);
    expect(shown).toEqual(['inactive']);
  });

  it('does not take a reader with no game loaded for a live overlay', () => {
    const { watch: presence, shown } = watch();
    presence.start();

    presence.received(NO_GAME);
    vi.advanceTimersByTime(PRESENCE_WAIT_MS);

    expect(shown).toEqual(['inactive']);
  });

  it('corrects a verdict a later state outranks while the notice is up, and restarts its clock', () => {
    const { watch: presence, shown, hidden } = watch();
    presence.start();
    vi.advanceTimersByTime(PRESENCE_WAIT_MS);

    vi.advanceTimersByTime(NOTICE_VISIBLE_MS - 1);
    presence.received(IN_COMBAT);
    presence.received(MOCK_STATE);
    presence.received(IN_COMBAT);

    expect(shown).toEqual(['inactive', 'paused', 'active']);
    vi.advanceTimersByTime(NOTICE_VISIBLE_MS - 1);
    expect(hidden()).toBe(0);
    vi.advanceTimersByTime(1);
    expect(hidden()).toBe(1);
  });

  it('shows nothing more once the notice has gone, however many states follow', () => {
    const { watch: presence, shown } = watch();
    presence.start();
    presence.received(IN_COMBAT);
    vi.advanceTimersByTime(NOTICE_VISIBLE_MS);

    presence.received(MOCK_STATE);
    presence.start();
    vi.advanceTimersByTime(PRESENCE_WAIT_MS);

    expect(shown).toEqual(['paused']);
  });

  it('hides the notice on its own after it has been visible for a while', () => {
    const { watch: presence, hidden } = watch();
    presence.start();
    presence.received(MOCK_STATE);

    vi.advanceTimersByTime(NOTICE_VISIBLE_MS - 1);
    expect(hidden()).toBe(0);

    vi.advanceTimersByTime(1);
    expect(hidden()).toBe(1);
  });

  it('hides the notice early when dismissed, and only once', () => {
    const { watch: presence, hidden } = watch();
    presence.start();
    presence.received(MOCK_STATE);

    presence.dismiss();
    presence.dismiss();
    vi.advanceTimersByTime(NOTICE_VISIBLE_MS);

    expect(hidden()).toBe(1);
  });

  it('ignores a dismissal before there is anything to dismiss', () => {
    const { watch: presence, shown } = watch();
    presence.start();

    presence.dismiss();
    vi.advanceTimersByTime(PRESENCE_WAIT_MS);

    expect(shown).toEqual(['inactive']);
  });

  it('shows nothing once stopped', () => {
    const { watch: presence, shown, hidden } = watch();
    presence.start();
    presence.stop();

    vi.advanceTimersByTime(PRESENCE_WAIT_MS);
    presence.received(MOCK_STATE);

    expect(shown).toEqual([]);
    expect(hidden()).toBe(0);
  });

  it('hides a notice that is up when stopped', () => {
    const { watch: presence, hidden } = watch();
    presence.start();
    presence.received(MOCK_STATE);

    presence.stop();
    vi.advanceTimersByTime(NOTICE_VISIBLE_MS);

    expect(hidden()).toBe(1);
  });
});

describe('presence notice', () => {
  const widthOf = (_font: string, value: string): number => value.length * 7;

  it('sizes itself to the longer of its two lines', () => {
    const card = noticeCard('active', widthOf);
    const panel = card.ops[0];

    expect(panel).toMatchObject({ kind: 'panel', panel: 'notice', x: 0, y: 0, width: card.width, height: card.height });
    expect(card.width).toBe(10 + 48 + 8 + 'Hover a hero or a town in the panel'.length * 7 + 10);
  });

  it('tells the viewer what to do when live and why not otherwise', () => {
    const lines = (presence: Presence): string[] =>
      noticeCard(presence, widthOf).ops.flatMap((op) => (op.kind === 'text' ? [op.text] : []));

    expect(lines('active')).toEqual(['HotA overlay is active', 'Hover a hero or a town in the panel']);
    expect(lines('paused')).toEqual(['HotA overlay is active', 'Cards open on the adventure map']);
    expect(lines('inactive')).toEqual(['HotA overlay is not active', 'No game is being shared']);
  });

  it('keeps its bitmap size in a small picture and grows with a large one', () => {
    const notice = { width: 300, height: 52 };

    expect(noticeScale(notice, { width: 640, height: 360 })).toBe(1);
    expect(noticeScale(notice, { width: 1920, height: 1080 })).toBe(1.5);
  });

  it('follows the picture rather than a tall player, and never outgrows its width', () => {
    const notice = { width: 300, height: 52 };

    expect(noticeScale(notice, { width: 360, height: 1200 })).toBe(1.2);
    expect(noticeScale(notice, { width: 240, height: 135 })).toBe(0.8);
  });

  it('sits in the bottom-right corner of the picture, not in the letterbox', () => {
    const video = { x: 0, y: 60, width: 1280, height: 600 };

    expect(placeNotice({ width: 300, height: 52 }, video, 12)).toEqual({ left: 968, top: 596 });
  });

  it('keeps to the top-left of a picture with no room for the margin', () => {
    expect(placeNotice({ width: 200, height: 40 }, { x: 10, y: 20, width: 200, height: 40 }, 12)).toEqual({
      left: 10,
      top: 20,
    });
  });
});
