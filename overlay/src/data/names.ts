/** Hero classes 0..23; 18..23 are the HotA classes (Cove, Factory, Bulwark). */
export const HERO_CLASSES: readonly string[] = [
    'Knight', 'Cleric', 'Ranger', 'Druid', 'Alchemist', 'Wizard',
    'Demoniac', 'Heretic', 'Death Knight', 'Necromancer', 'Overlord', 'Warlock',
    'Barbarian', 'Battle Mage', 'Beastmaster', 'Witch', 'Planeswalker', 'Elementalist',
    'Captain', 'Navigator', 'Mercenary', 'Artificer', 'Chieftain', 'Elder',
];

/** Town types 0..11; 9..11 are the HotA towns. */
export const TOWN_TYPES: readonly string[] = [
    'Castle', 'Rampart', 'Tower', 'Inferno', 'Necropolis', 'Dungeon',
    'Stronghold', 'Fortress', 'Conflux', 'Cove', 'Factory', 'Bulwark',
];

export const HALL_NAMES: readonly string[] = ['Village Hall', 'Town Hall', 'City Hall', 'Capitol'];
export const FORT_NAMES: readonly string[] = ['No Fort', 'Fort', 'Citadel', 'Castle'];

export const PRIMARY_NAMES: readonly string[] = ['Attack', 'Defense', 'Power', 'Knowledge'];
export const SKILL_LEVELS: readonly string[] = ['', 'Basic', 'Advanced', 'Expert'];

export const SECONDARY_SKILLS: readonly string[] = [
    'Pathfinding', 'Archery', 'Logistics', 'Scouting', 'Diplomacy', 'Navigation', 'Leadership',
    'Wisdom', 'Mysticism', 'Luck', 'Ballistics', 'Eagle Eye', 'Necromancy', 'Estates',
    'Fire Magic', 'Air Magic', 'Water Magic', 'Earth Magic', 'Scholar', 'Tactics', 'Artillery',
    'Learning', 'Offense', 'Armorer', 'Intelligence', 'Sorcery', 'Resistance', 'First Aid',
];

/** Spells 0..69, the range a mage guild can hold. */
export const SPELLS: readonly string[] = [
    'Summon Boat', 'Scuttle Boat', 'Visions', 'View Earth', 'Disguise', 'View Air', 'Fly',
    'Water Walk', 'Dimension Door', 'Town Portal', 'Quicksand', 'Land Mine', 'Force Field',
    'Fire Wall', 'Earthquake', 'Magic Arrow', 'Ice Bolt', 'Lightning Bolt', 'Implosion',
    'Chain Lightning', 'Frost Ring', 'Fireball', 'Inferno', 'Meteor Shower', 'Death Ripple',
    'Destroy Undead', 'Armageddon', 'Shield', 'Air Shield', 'Fire Shield', 'Protection from Air',
    'Protection from Fire', 'Protection from Water', 'Protection from Earth', 'Anti-Magic',
    'Dispel', 'Magic Mirror', 'Cure', 'Resurrection', 'Animate Dead', 'Sacrifice', 'Bless',
    'Curse', 'Bloodlust', 'Precision', 'Weakness', 'Stone Skin', 'Disrupting Ray', 'Prayer',
    'Mirth', 'Sorrow', 'Fortune', 'Misfortune', 'Haste', 'Slow', 'Slayer', 'Frenzy',
    "Titan's Lightning Bolt", 'Counterstrike', 'Berserk', 'Hypnotize', 'Forgetfulness', 'Blind',
    'Teleport', 'Remove Obstacle', 'Clone', 'Fire Elemental', 'Earth Elemental',
    'Water Elemental', 'Air Elemental',
];

function lookup(table: readonly string[], id: number, unknown: string): string {
    return table[id] ?? `${unknown} ${id}`;
}

export const heroClassName = (id: number): string => lookup(HERO_CLASSES, id, 'Class');
export const townTypeName = (id: number): string => lookup(TOWN_TYPES, id, 'Town');
export const hallName = (id: number): string => lookup(HALL_NAMES, id, 'Hall');
export const fortName = (id: number): string => lookup(FORT_NAMES, id, 'Fort');
export const secondarySkillName = (id: number): string => lookup(SECONDARY_SKILLS, id, 'Skill');
export const spellName = (id: number): string => lookup(SPELLS, id, 'Spell');
export const skillLevelName = (level: number): string => SKILL_LEVELS[level] ?? `Level ${level}`;
