import { creatureIcon } from '../data/sprites.js';
import { sprite, text, type DrawOp } from './display.js';
import { armySlot } from './layout.js';
import type { ArmyEntry } from '../state/protocol.js';

/** The army slots the popup frames: a creature icon in each box with its count beneath. */
export function armyOps(army: readonly ArmyEntry[]): DrawOp[] {
  const ops: DrawOp[] = [];
  for (const [slot, creature, count] of army) {
    const at = armySlot(slot);
    if (at === null) continue;
    ops.push(sprite(creatureIcon(creature), at.x, at.y, at.icon, at.icon));
    ops.push(text(String(count), 'tiny', 'white', at.centre, at.countY, 'center'));
  }
  return ops;
}
