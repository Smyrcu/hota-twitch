import type { Selection, TownView } from '../render/card.js';
import type { Town } from '../state/protocol.js';
import type { Zone } from '../zones/index.js';

export function selectionFor(zone: Zone): Selection {
  return { kind: zone.kind, entry: zone.entry, expanded: false, view: 'town' };
}

export function matches(selection: Selection, zone: Zone): boolean {
  return selection.kind === zone.kind && selection.entry === zone.entry;
}

function townViews(town: Town): TownView[] {
  const views: TownView[] = ['town'];
  if (town.garrisonHero !== null) views.push('garrisonHero');
  if (town.visitingHero !== null) views.push('visitingHero');
  return views;
}

/**
 * What a click does: a hero card expands and collapses; a town card expands, then steps through
 * the heroes standing in the town, then collapses back to the popup.
 */
export function advance(selection: Selection, town: Town | null): Selection {
  if (selection.kind === 'hero' || town === null) {
    return { ...selection, expanded: !selection.expanded };
  }
  if (!selection.expanded) return { ...selection, expanded: true, view: 'town' };

  const views = townViews(town);
  const next = views[views.indexOf(selection.view) + 1];
  return next === undefined
    ? { ...selection, expanded: false, view: 'town' }
    : { ...selection, expanded: true, view: next };
}
