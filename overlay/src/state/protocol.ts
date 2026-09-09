export const PROTOCOL_VERSION = 1;

export type ScreenName = 'none' | 'adventure' | 'town' | 'combat' | 'other';

export interface GameDate {
  readonly day: number;
  readonly week: number;
  readonly month: number;
}

export interface Display {
  readonly width: number;
  readonly height: number;
  /** HD Mod interface scale; 1 when the producer cannot read it. */
  readonly uiScale: number;
}

export interface Player {
  readonly id: number;
  readonly name: string;
  readonly currentHero: number;
  readonly heroListTop: number;
  readonly townListTop: number;
}

/** [skill id, level 1..3] */
export type SkillEntry = readonly [skill: number, level: number];
/** [body slot 0..18, artifact id] */
export type EquippedEntry = readonly [slot: number, artifact: number];
/** [slot 0..6, creature id, count] */
export type ArmyEntry = readonly [slot: number, creature: number, count: number];
/** attack, defense, power, knowledge */
export type PrimarySkills = readonly [number, number, number, number];

export interface Hero {
  readonly id: number;
  readonly name: string;
  readonly class: number;
  readonly picture: number;
  readonly level: number;
  readonly exp: number;
  readonly mana: number;
  readonly manaMax: number;
  readonly move: number;
  readonly moveMax: number;
  readonly primary: PrimarySkills;
  readonly skills: readonly SkillEntry[];
  readonly equipped: readonly EquippedEntry[];
  readonly backpack: readonly number[];
  readonly army: readonly ArmyEntry[];
}

export interface Research {
  readonly level: number;
  readonly slot: number;
  readonly spell: number;
  readonly rolls: number;
}

export interface Town {
  readonly id: number;
  readonly name: string;
  readonly type: number;
  readonly fort: number;
  readonly hall: number;
  readonly guild: number;
  /** Per guild level 1..5: spell ids of the built levels only. */
  readonly spells: readonly (readonly number[])[];
  readonly research: Research | null;
  readonly garrison: readonly ArmyEntry[];
  readonly garrisonHero: Hero | null;
  readonly visitingHero: Hero | null;
}

export interface GameState {
  readonly v: number;
  readonly ts: number;
  readonly screen: ScreenName;
  readonly date: GameDate;
  readonly display: Display;
  readonly player: Player;
  readonly heroes: readonly Hero[];
  readonly towns: readonly Town[];
}
