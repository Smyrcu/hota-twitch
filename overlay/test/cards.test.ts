import { describe, expect, it } from 'vitest';
import { cardFor } from '../src/render/card.js';
import { heroHoverCard } from '../src/render/hero/card.js';
import { garrisonArmy, townHoverCard } from '../src/render/town/card.js';
import { CARD_HEIGHT, CARD_WIDTH, EXPANSION } from '../src/render/layout.js';
import type { LineCounter } from '../src/render/build.js';
import { layoutText } from '../src/render/text/layout.js';
import { TEST_ATLAS } from './fixtures/atlas.js';
import { MOCK_STATE } from './fixtures/state.js';

const state = MOCK_STATE;

const hero = (index: number) => state.heroes[index]!;
const town = (index: number) => state.towns[index]!;

const sources = (card: { ops: readonly { kind: string }[] }): string[] =>
  card.ops.filter((op): op is { kind: 'sprite'; src: string } => op.kind === 'sprite').map((op) => op.src);

describe('hero card', () => {
  it('is the size of the game popup and draws the portrait, primary skills and army', () => {
    const card = heroHoverCard(hero(0));

    expect(card.width).toBe(CARD_WIDTH);
    expect(card.height).toBe(CARD_HEIGHT);
    expect(sources(card)).toEqual([
      'assets/heroes/large/196.png',
      'assets/primary/attack.png',
      'assets/primary/defense.png',
      'assets/primary/power.png',
      'assets/primary/knowledge.png',
      'assets/creatures/13.png',
      'assets/creatures/110.png',
      'assets/creatures/116.png',
      'assets/creatures/118.png',
      'assets/creatures/120.png',
      'assets/creatures/127.png',
      'assets/creatures/131.png',
    ]);
  });

  it('keeps every army slot inside the card', () => {
    const card = heroHoverCard(hero(0));
    for (const op of card.ops) {
      if (op.kind !== 'sprite') continue;
      expect(op.x).toBeGreaterThanOrEqual(0);
      expect(op.x + op.width).toBeLessThanOrEqual(CARD_WIDTH);
      expect(op.y + op.height).toBeLessThanOrEqual(CARD_HEIGHT);
    }
  });

  it('ignores army entries outside the seven slots', () => {
    const broken = { ...hero(1), army: [[9, 14, 3]] as const };

    expect(sources(heroHoverCard(broken)).filter((src) => src.startsWith('assets/creatures/'))).toEqual([]);
  });

  it('matches the layout snapshot', () => {
    expect(cardFor(state, { kind: 'hero', entry: 0, expanded: false, view: 'town' })).toMatchSnapshot();
  });

  it('matches the expanded layout snapshot, with skills, artifacts and backpack', () => {
    expect(cardFor(state, { kind: 'hero', entry: 0, expanded: true, view: 'town' })).toMatchSnapshot();
  });

  it('grows the card by the expansion', () => {
    const expanded = cardFor(state, { kind: 'hero', entry: 0, expanded: true, view: 'town' });
    expect(expanded?.height).toBeGreaterThan(CARD_HEIGHT);
    expect(expanded?.width).toBe(CARD_WIDTH);
  });
});

describe('town card', () => {
  it('draws the picture for the fortified type, the hall and the fort', () => {
    const drawn = sources(townHoverCard(town(0)));

    expect(drawn[0]).toBe('assets/towns/20.png');
    expect(drawn).toContain('assets/ui/hall-3.png');
    expect(drawn).toContain('assets/ui/fort-2.png');
  });

  it('uses the unfortified picture set and no fort icon when the town has no fort', () => {
    const drawn = sources(townHoverCard(town(2)));

    expect(drawn[0]).toBe('assets/towns/42.png');
    expect(drawn.some((src) => src.startsWith('assets/ui/fort-'))).toBe(false);
  });

  it('shows the garrison hero army when a hero holds the garrison', () => {
    expect(garrisonArmy(town(1))).toEqual(town(1).garrisonHero?.army);
    expect(garrisonArmy(town(0))).toEqual(town(0).garrison);
  });

  it('matches the layout snapshot', () => {
    expect(cardFor(state, { kind: 'town', entry: 0, expanded: false, view: 'town' })).toMatchSnapshot();
  });

  it('matches the expanded layout snapshot, with the mage guild and the open research', () => {
    expect(cardFor(state, { kind: 'town', entry: 0, expanded: true, view: 'town' })).toMatchSnapshot();
  });

  it('dims the researched spell and shows its roll count', () => {
    const card = cardFor(state, { kind: 'town', entry: 0, expanded: true, view: 'town' });
    const fills = card?.ops.filter((op) => op.kind === 'fill') ?? [];
    const rolls = card?.ops.filter((op) => op.kind === 'text' && op.text === '?2') ?? [];

    expect(fills).toHaveLength(1);
    expect(rolls).toHaveLength(1);
  });

  it('shows only the built guild levels', () => {
    const card = cardFor(state, { kind: 'town', entry: 2, expanded: true, view: 'town' });
    const levels = card?.ops.filter((op) => op.kind === 'text' && op.text.startsWith('Level ')) ?? [];

    expect(levels).toHaveLength(1);
  });

  it('shows the hero standing in the town as a full hero card', () => {
    const card = cardFor(state, { kind: 'town', entry: 1, expanded: true, view: 'garrisonHero' });

    expect(sources(card!)[0]).toBe('assets/heroes/large/45.png');
  });

  it('renders nothing for an entry the state does not have', () => {
    expect(cardFor(state, { kind: 'town', entry: 9, expanded: false, view: 'town' })).toBeNull();
    expect(cardFor(state, { kind: 'hero', entry: 9, expanded: false, view: 'town' })).toBeNull();
  });
});

describe('expansion line spacing', () => {
  it('reserves a row per wrapped line so sections never overlap', () => {
    const oneLine = cardFor(state, { kind: 'town', entry: 0, expanded: true, view: 'town' });
    const wrapped = cardFor(state, { kind: 'town', entry: 0, expanded: true, view: 'town' }, () => 2);

    expect(wrapped!.height).toBeGreaterThan(oneLine!.height);
  });

  it('counts the lines the loaded atlas actually produces', () => {
    const counter: LineCounter = (value, maxWidth) => layoutText(TEST_ATLAS, value, { maxWidth }).lines;

    expect(counter('AAAA BBBB', 20)).toBe(2);
    const card = cardFor(state, { kind: 'town', entry: 0, expanded: true, view: 'town' }, counter);
    const lines = card!.ops.filter((op) => op.kind === 'text' && op.x === EXPANSION.inset);

    for (let index = 1; index < lines.length; index += 1) {
      expect(lines[index]!.y).toBeGreaterThan(lines[index - 1]!.y);
    }
  });
});
