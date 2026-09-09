import type { GameState } from '../state/protocol.js';
import {
  HERO_LIST,
  TOWN_LIST,
  containFit,
  mapRect,
  rowRectInGame,
  visibleRows,
  type ListGeometry,
  type Rect,
  type Size,
} from './geometry.js';

export * from './geometry.js';

export type ZoneKind = 'hero' | 'town';

export interface Zone {
  readonly kind: ZoneKind;
  /** Index of the row inside the list widget, top to bottom. */
  readonly row: number;
  /** Index into `state.heroes` / `state.towns`, with the list scroll offset applied. */
  readonly entry: number;
  /** Position in player pixels. */
  readonly rect: Rect;
}

function zonesForList(
  kind: ZoneKind,
  list: ListGeometry,
  state: GameState,
  player: Size,
  listTop: number,
  entryCount: number,
): Zone[] {
  const { display } = state;
  const fit = containFit(display, player);
  const rows = visibleRows(list, display.height / display.uiScale);
  const top = Math.max(0, listTop);
  const zones: Zone[] = [];
  for (let row = 0; row < rows; row += 1) {
    const entry = top + row;
    if (entry >= entryCount) break;
    zones.push({ kind, row, entry, rect: mapRect(rowRectInGame(list, display, row), fit) });
  }
  return zones;
}

/** Hover targets over the panel's hero and town lists. Empty unless the adventure map is shown. */
export function computeZones(state: GameState, player: Size): Zone[] {
  if (state.screen !== 'adventure' || state.player === null) return [];
  if (player.width <= 0 || player.height <= 0) return [];
  return [
    ...zonesForList('hero', HERO_LIST, state, player, state.player.heroListTop, state.heroes.length),
    ...zonesForList('town', TOWN_LIST, state, player, state.player.townListTop, state.towns.length),
  ];
}
