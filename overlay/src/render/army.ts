import { creatureIcon } from '../data/sprites.js';
import { sprite, text, type DrawOp } from './display.js';
import { ARMY_ROW, cellIconX, rowOrigin } from './layout.js';
import type { ArmyEntry } from '../state/protocol.js';

/** The row of army slots at the bottom of both popups: creature icons with counts beneath. */
export function armyOps(army: readonly ArmyEntry[]): DrawOp[] {
  const origin = rowOrigin(ARMY_ROW.cell, ARMY_ROW.slots);
  const ops: DrawOp[] = [];
  for (const [slot, creature, count] of army) {
    if (slot < 0 || slot >= ARMY_ROW.slots) continue;
    ops.push(
      sprite(
        creatureIcon(creature),
        cellIconX(origin, ARMY_ROW.cell, ARMY_ROW.icon, slot),
        ARMY_ROW.y,
        ARMY_ROW.icon,
        ARMY_ROW.icon,
      ),
    );
    ops.push(
      text(
        String(count),
        'tiny',
        'white',
        origin + ARMY_ROW.cell * slot + ARMY_ROW.cell / 2,
        ARMY_ROW.countY,
        'center',
      ),
    );
  }
  return ops;
}
