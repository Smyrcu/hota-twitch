import type { FontId } from '../data/fonts.js';
import type { Presence } from '../state/presence.js';
import { sprite, text, type Card } from './display.js';

export type TextWidth = (font: FontId, value: string) => number;

interface NoticeText {
  readonly title: string;
  readonly hint: string;
}

const TEXTS: Readonly<Record<Presence, NoticeText>> = {
  active: { title: 'HotA overlay is active', hint: 'Hover a hero or a town in the panel' },
  paused: { title: 'HotA overlay is active', hint: 'Cards open on the adventure map' },
};

/** The Visions spell: the game's own picture for seeing what an army holds. */
const ICON = { src: 'assets/spells/2.png', width: 48, height: 36 } as const;

const PADDING_X = 10;
const PADDING_Y = 8;
const GAP = 8;
const TITLE_HEIGHT = 20;
const HINT_HEIGHT = 16;

/**
 * The notice a viewer sees on arrival: the Visions icon and two lines in the game's fonts on the
 * leather of a popup.
 */
export function noticeCard(presence: Presence, widthOf: TextWidth): Card {
  const copy = TEXTS[presence];
  const textWidth = Math.max(widthOf('medium', copy.title), widthOf('small', copy.hint));
  const textHeight = TITLE_HEIGHT + HINT_HEIGHT;
  const height = PADDING_Y * 2 + Math.max(ICON.height, textHeight);
  const textX = PADDING_X + ICON.width + GAP;
  const textY = Math.round((height - textHeight) / 2);
  const width = textX + Math.ceil(textWidth) + PADDING_X;
  return {
    width,
    height,
    ops: [
      { kind: 'panel', panel: 'notice', x: 0, y: 0, width, height },
      sprite(ICON.src, PADDING_X, Math.round((height - ICON.height) / 2), ICON.width, ICON.height),
      text(copy.title, 'medium', 'yellow', textX, textY),
      text(copy.hint, 'small', 'white', textX, textY + TITLE_HEIGHT),
    ],
  };
}
