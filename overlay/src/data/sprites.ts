/** Sprite paths inside the extension bundle; every asset is exported by `tools/`. */
const PRIMARY_ICONS: readonly string[] = ['attack', 'defense', 'power', 'knowledge'];

export const heroPortrait = (picture: number): string => `assets/heroes/large/${picture}.png`;
export const creatureIcon = (creature: number): string => `assets/creatures/${creature}.png`;
export const artifactIcon = (artifact: number): string => `assets/artifacts/${artifact}.png`;
export const spellIcon = (spell: number): string => `assets/spells/${spell}.png`;
export const skillIcon = (skill: number, level: number): string => `assets/skills/${skill}_${level}.png`;
export const hallIcon = (hall: number): string => `assets/ui/hall-${hall}.png`;

/**
 * `itmcl.def` holds only the three fortifications, so the frame is one below the protocol's
 * value (1 fort, 2 citadel, 3 castle); a town with no fort shows no icon at all.
 */
export function fortIcon(fort: number): string | null {
  return fort > 0 ? `assets/ui/fort-${fort - 1}.png` : null;
}

export function primaryIcon(index: number): string {
  return `assets/primary/${PRIMARY_ICONS[index] ?? 'attack'}.png`;
}

/**
 * Town pictures come from `itpt.def`: frames 0..23 are the fortified towns (two per type) and
 * 24..47 the unfortified ones, so a type maps to `(fort > 0 ? 0 : 24) + type * 2`.
 */
export function townPicture(type: number, fort: number): string {
  return `assets/towns/${(fort > 0 ? 0 : 24) + type * 2}.png`;
}
