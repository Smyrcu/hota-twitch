import { heroClassName } from '../../data/names.js';
import { artifactIcon, creatureIcon, heroPortrait, primaryIcon, skillIcon } from '../../data/sprites.js';
import { SINGLE_LINE, SectionBuilder, type LineCounter } from '../build.js';
import { sprite, text, type Card, type DrawOp } from '../display.js';
import {
    ARMY_ROW,
    CARD_HEIGHT,
    CARD_WIDTH,
    EXPANSION,
    HEADER,
    PORTRAIT,
    PRIMARY_ROW,
    cellIconX,
    rowOrigin,
} from '../layout.js';
import type { ArmyEntry, Hero } from '../../state/protocol.js';

function armyOps(army: readonly ArmyEntry[]): DrawOp[] {
    const origin = rowOrigin(ARMY_ROW.cell, ARMY_ROW.slots);
    const ops: DrawOp[] = [];
    for (const [slot, creature, count] of army) {
        if (slot < 0 || slot >= ARMY_ROW.slots) continue;
        const x = cellIconX(origin, ARMY_ROW.cell, ARMY_ROW.icon, slot);
        const centre = origin + ARMY_ROW.cell * slot + ARMY_ROW.cell / 2;
        ops.push(sprite(creatureIcon(creature), x, ARMY_ROW.y, ARMY_ROW.icon, ARMY_ROW.icon));
        ops.push(text(String(count), 'tiny', 'white', centre, ARMY_ROW.countY, 'center'));
    }
    return ops;
}

/** The popup as the game draws it on right-click: portrait, name, primary skills, army. */
export function heroHoverCard(hero: Hero): Card {
    const primaryOrigin = rowOrigin(PRIMARY_ROW.cell, PRIMARY_ROW.count);
    const ops: DrawOp[] = [
        { kind: 'panel', panel: 'hero', x: 0, y: 0, width: CARD_WIDTH, height: CARD_HEIGHT },
        sprite(heroPortrait(hero.picture), PORTRAIT.x, PORTRAIT.y, PORTRAIT.width, PORTRAIT.height),
        text(hero.name, 'medium', 'yellow', HEADER.x, HEADER.nameY, 'left', HEADER.width),
        text(`Level ${hero.level}`, 'small', 'white', HEADER.x, HEADER.lineY),
        text(heroClassName(hero.class), 'small', 'white', HEADER.x, HEADER.subLineY, 'left', HEADER.width),
    ];

    hero.primary.forEach((value, index) => {
        const x = cellIconX(primaryOrigin, PRIMARY_ROW.cell, PRIMARY_ROW.icon, index);
        const centre = primaryOrigin + PRIMARY_ROW.cell * index + PRIMARY_ROW.cell / 2;
        ops.push(sprite(primaryIcon(index), x, PRIMARY_ROW.y, PRIMARY_ROW.icon, PRIMARY_ROW.icon));
        ops.push(text(String(value), 'small', 'gold', centre, PRIMARY_ROW.valueY, 'center'));
    });

    ops.push(...armyOps(hero.army));
    return { width: CARD_WIDTH, height: CARD_HEIGHT, ops };
}

/**
 * The click expansion: what the hero screen adds over the popup — secondary skills, the
 * artifacts on the hero and in the backpack, movement and mana.
 */
export function heroExpansion(
    hero: Hero,
    top: number,
    countLines: LineCounter = SINGLE_LINE,
): { ops: readonly DrawOp[]; height: number } {
    const section = new SectionBuilder(top + EXPANSION.paddingTop, countLines);

    section.line(`Movement ${hero.move} / ${hero.moveMax}`);
    section.line(`Mana ${hero.mana} / ${hero.manaMax}`);
    section.line(`Experience ${hero.exp}`);
    section.advance(4);

    if (hero.skills.length > 0) {
        section.heading('Skills');
        const { cell, icon, perRow } = EXPANSION.skill;
        const cells = section.grid(hero.skills.length, perRow, cell, icon + 2);
        hero.skills.forEach(([skill, level], index) => {
            const at = cells[index];
            if (at === undefined) return;
            section.push(sprite(skillIcon(skill, level), at.x + (cell - icon) / 2, at.y, icon, icon));
        });
        section.advance(4);
    }

    if (hero.equipped.length > 0) {
        section.heading('Artifacts');
        const { cell, icon, perRow } = EXPANSION.equipped;
        const equipped = [...hero.equipped].sort((left, right) => left[0] - right[0]);
        const cells = section.grid(equipped.length, perRow, cell, icon + 2);
        equipped.forEach(([, artifact], index) => {
            const at = cells[index];
            if (at === undefined) return;
            section.push(sprite(artifactIcon(artifact), at.x + (cell - icon) / 2, at.y, icon, icon));
        });
        section.advance(4);
    }

    if (hero.backpack.length > 0) {
        section.heading(`Backpack (${hero.backpack.length})`);
        const { cell, icon, perRow } = EXPANSION.backpack;
        const cells = section.grid(hero.backpack.length, perRow, cell, icon + 2);
        hero.backpack.forEach((artifact, index) => {
            const at = cells[index];
            if (at === undefined) return;
            section.push(sprite(artifactIcon(artifact), at.x + (cell - icon) / 2, at.y, icon, icon));
        });
    }

    return section.finish('expansion', CARD_WIDTH, top);
}

export function heroCard(hero: Hero, expanded: boolean, countLines: LineCounter = SINGLE_LINE): Card {
    const hover = heroHoverCard(hero);
    if (!expanded) return hover;
    const expansion = heroExpansion(hero, CARD_HEIGHT, countLines);
    return {
        width: CARD_WIDTH,
        height: CARD_HEIGHT + expansion.height,
        ops: [...hover.ops, ...expansion.ops],
    };
}

