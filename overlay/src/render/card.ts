import type { GameState, Hero, Town } from '../state/protocol.js';
import type { LineCounter } from './build.js';
import { SINGLE_LINE } from './build.js';
import type { Card } from './display.js';
import { heroCard } from './hero/card.js';
import { townCard } from './town/card.js';

/** Which card a town zone shows; a click cycles through the heroes standing in the town. */
export type TownView = 'town' | 'garrisonHero' | 'visitingHero';

export interface Selection {
    readonly kind: 'hero' | 'town';
    readonly entry: number;
    readonly expanded: boolean;
    readonly view: TownView;
}

export function heroInTown(town: Town, view: TownView): Hero | null {
    if (view === 'garrisonHero') return town.garrisonHero;
    if (view === 'visitingHero') return town.visitingHero;
    return null;
}

export function cardFor(state: GameState, selection: Selection, countLines: LineCounter = SINGLE_LINE): Card | null {
    if (selection.kind === 'hero') {
        const hero = state.heroes[selection.entry];
        return hero === undefined ? null : heroCard(hero, selection.expanded, countLines);
    }
    const town = state.towns[selection.entry];
    if (town === undefined) return null;
    const hero = heroInTown(town, selection.view);
    return hero === null ? townCard(town, selection.expanded, countLines) : heroCard(hero, true, countLines);
}
