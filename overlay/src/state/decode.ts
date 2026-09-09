import {
  PROTOCOL_VERSION,
  type ArmyEntry,
  type Display,
  type EquippedEntry,
  type GameDate,
  type GameState,
  type Hero,
  type Player,
  type PrimarySkills,
  type Research,
  type ScreenName,
  type SkillEntry,
  type Town,
} from './protocol.js';

export type DecodeResult =
  | { readonly ok: true; readonly state: GameState }
  | { readonly ok: false; readonly reason: string };

const SCREENS: readonly ScreenName[] = ['none', 'adventure', 'town', 'combat', 'other'];

class DecodeError extends Error {}

function fail(path: string, expected: string): never {
  throw new DecodeError(`${path}: expected ${expected}`);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function record(value: unknown, path: string): Record<string, unknown> {
  return isRecord(value) ? value : fail(path, 'an object');
}

function num(value: unknown, path: string): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fail(path, 'a finite number');
}

function int(value: unknown, path: string): number {
  const n = num(value, path);
  return Number.isInteger(n) ? n : fail(path, 'an integer');
}

function str(value: unknown, path: string): string {
  return typeof value === 'string' ? value : fail(path, 'a string');
}

function array(value: unknown, path: string): readonly unknown[] {
  return Array.isArray(value) ? value : fail(path, 'an array');
}

function tuple(value: unknown, path: string, length: number): readonly unknown[] {
  const items = array(value, path);
  return items.length === length ? items : fail(path, `${length} items, got ${items.length}`);
}

function optional<T>(value: unknown, path: string, decode: (value: unknown, path: string) => T): T | null {
  return value === null || value === undefined ? null : decode(value, path);
}

function decodeDate(value: unknown, path: string): GameDate {
  const raw = record(value, path);
  return {
    day: int(raw['day'], `${path}.day`),
    week: int(raw['week'], `${path}.week`),
    month: int(raw['month'], `${path}.month`),
  };
}

function decodeDisplay(value: unknown, path: string): Display {
  const raw = record(value, path);
  const width = int(raw['width'], `${path}.width`);
  const height = int(raw['height'], `${path}.height`);
  if (width <= 0 || height <= 0) fail(path, 'a positive resolution');
  const declared = raw['uiScale'];
  const scale = declared === undefined || declared === null ? 1 : num(declared, `${path}.uiScale`);
  if (scale <= 0) fail(`${path}.uiScale`, 'a positive scale');
  return { width, height, uiScale: scale };
}

function decodePlayer(value: unknown, path: string): Player {
  const raw = record(value, path);
  return {
    id: int(raw['id'], `${path}.id`),
    name: str(raw['name'], `${path}.name`),
    currentHero: int(raw['currentHero'], `${path}.currentHero`),
    heroListTop: int(raw['heroListTop'], `${path}.heroListTop`),
    townListTop: int(raw['townListTop'], `${path}.townListTop`),
  };
}

function decodePrimary(value: unknown, path: string): PrimarySkills {
  const items = tuple(value, path, 4);
  return [
    int(items[0], `${path}[0]`),
    int(items[1], `${path}[1]`),
    int(items[2], `${path}[2]`),
    int(items[3], `${path}[3]`),
  ];
}

function decodeSkill(value: unknown, path: string): SkillEntry {
  const items = tuple(value, path, 2);
  return [int(items[0], `${path}[0]`), int(items[1], `${path}[1]`)];
}

function decodeEquipped(value: unknown, path: string): EquippedEntry {
  const items = tuple(value, path, 2);
  return [int(items[0], `${path}[0]`), int(items[1], `${path}[1]`)];
}

function decodeArmy(value: unknown, path: string): ArmyEntry {
  const items = tuple(value, path, 3);
  return [int(items[0], `${path}[0]`), int(items[1], `${path}[1]`), int(items[2], `${path}[2]`)];
}

function list<T>(value: unknown, path: string, decode: (value: unknown, path: string) => T): readonly T[] {
  return array(value, path).map((item, index) => decode(item, `${path}[${index}]`));
}

function decodeHero(value: unknown, path: string): Hero {
  const raw = record(value, path);
  return {
    id: int(raw['id'], `${path}.id`),
    name: str(raw['name'], `${path}.name`),
    class: int(raw['class'], `${path}.class`),
    picture: int(raw['picture'], `${path}.picture`),
    level: int(raw['level'], `${path}.level`),
    exp: int(raw['exp'], `${path}.exp`),
    mana: int(raw['mana'], `${path}.mana`),
    manaMax: int(raw['manaMax'], `${path}.manaMax`),
    move: int(raw['move'], `${path}.move`),
    moveMax: int(raw['moveMax'], `${path}.moveMax`),
    primary: decodePrimary(raw['primary'], `${path}.primary`),
    skills: list(raw['skills'], `${path}.skills`, decodeSkill),
    equipped: list(raw['equipped'], `${path}.equipped`, decodeEquipped),
    backpack: list(raw['backpack'], `${path}.backpack`, int),
    army: list(raw['army'], `${path}.army`, decodeArmy),
  };
}

function decodeResearch(value: unknown, path: string): Research {
  const raw = record(value, path);
  return {
    level: int(raw['level'], `${path}.level`),
    slot: int(raw['slot'], `${path}.slot`),
    spell: int(raw['spell'], `${path}.spell`),
    rolls: int(raw['rolls'], `${path}.rolls`),
  };
}

function decodeTown(value: unknown, path: string): Town {
  const raw = record(value, path);
  return {
    id: int(raw['id'], `${path}.id`),
    name: str(raw['name'], `${path}.name`),
    type: int(raw['type'], `${path}.type`),
    fort: int(raw['fort'], `${path}.fort`),
    hall: int(raw['hall'], `${path}.hall`),
    guild: int(raw['guild'], `${path}.guild`),
    spells: list(raw['spells'], `${path}.spells`, (level, levelPath) => list(level, levelPath, int)),
    research: optional(raw['research'], `${path}.research`, decodeResearch),
    garrison: list(raw['garrison'], `${path}.garrison`, decodeArmy),
    garrisonHero: optional(raw['garrisonHero'], `${path}.garrisonHero`, decodeHero),
    visitingHero: optional(raw['visitingHero'], `${path}.visitingHero`, decodeHero),
  };
}

function decodeScreen(value: unknown, path: string): ScreenName {
  const name = str(value, path);
  const known = SCREENS.find((screen) => screen === name);
  return known ?? fail(path, `one of ${SCREENS.join(', ')}`);
}

function decodeState(value: unknown): GameState {
  const raw = record(value, 'state');
  const version = int(raw['v'], 'state.v');
  if (version !== PROTOCOL_VERSION) {
    throw new DecodeError(`state.v: expected protocol version ${PROTOCOL_VERSION}, got ${version}`);
  }
  return {
    v: version,
    ts: int(raw['ts'], 'state.ts'),
    screen: decodeScreen(raw['screen'], 'state.screen'),
    date: decodeDate(raw['date'], 'state.date'),
    display: decodeDisplay(raw['display'], 'state.display'),
    player: decodePlayer(raw['player'], 'state.player'),
    heroes: list(raw['heroes'], 'state.heroes', decodeHero),
    towns: list(raw['towns'], 'state.towns', decodeTown),
  };
}

/** Validates a parsed state document against docs/protocol.md. Never throws. */
export function decode(value: unknown): DecodeResult {
  try {
    return { ok: true, state: decodeState(value) };
  } catch (error) {
    if (error instanceof DecodeError) return { ok: false, reason: error.message };
    throw error;
  }
}

/** Parses and validates a JSON state document. Never throws. */
export function decodeJson(text: string): DecodeResult {
  let parsed: unknown;
  try {
    parsed = JSON.parse(text) as unknown;
  } catch {
    return { ok: false, reason: 'state: not valid JSON' };
  }
  return decode(parsed);
}
