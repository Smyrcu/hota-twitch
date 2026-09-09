/**
 * Sprite paths inside the extension bundle. Every asset is exported by `tools/`; the sizes
 * below are the native frame sizes of the game's sprite sets and drive the card layout.
 */
export const SPRITE_SIZES = {
    heroPortrait: { width: 58, height: 64 },
    townPicture: { width: 58, height: 64 },
    creature: { width: 32, height: 32 },
    primary: { width: 32, height: 32 },
    skill: { width: 32, height: 32 },
    artifact: { width: 44, height: 44 },
    spell: { width: 48, height: 36 },
    building: { width: 38, height: 38 },
} as const;

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
export const manaIcon = (): string => 'assets/primary/mana.png';
export const experienceIcon = (): string => 'assets/primary/experience.png';

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
