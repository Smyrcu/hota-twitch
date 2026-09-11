import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { describeConnection } from '../src/config/status.js';
import {
  DEFAULT_UI_SCALE,
  UI_SCALES,
  formatUiScale,
  readUiScale,
  settleUiScale,
  uiScaleOptions,
} from '../src/config/settings.js';
import { STYLES } from '../src/config/typography.js';
import { readParams, safeBackground, safeStateSource } from '../src/dev/params.js';
import { fortIcon, primaryIcon, skillIcon, townPicture } from '../src/data/sprites.js';
import { heroClassName, spellName, townTypeName } from '../src/data/names.js';

const NOW = Date.parse('2026-09-09T22:00:30Z');

describe('connection status', () => {
  it('asks for a token first', () => {
    expect(describeConnection(false, null, NOW).connected).toBe(false);
    expect(describeConnection(false, null, NOW).text).toContain('HotA Twitch Reader');
  });

  it('waits for the first state once a token exists', () => {
    expect(describeConnection(true, null, NOW).text).toContain('waiting for the first state');
    expect(describeConnection(true, 'not a date', NOW).text).toContain('waiting for the first state');
  });

  it('reports the age of the last state', () => {
    expect(describeConnection(true, '2026-09-09T22:00:00Z', NOW)).toEqual({
      connected: true,
      text: 'Connected, last state 30 s ago.',
    });
  });

  it('warns when the game stopped sending', () => {
    const status = describeConnection(true, '2026-09-09T21:55:00Z', NOW);

    expect(status.connected).toBe(false);
    expect(status.text).toContain('330 s');
  });
});

const page = (name: string): string =>
  readFileSync(new URL(`../public/${name}`, import.meta.url), 'utf8');

describe('mock state source', () => {
  it('accepts only a bundled document', () => {
    expect(safeStateSource('dev/state-1080p.json')).toBe('dev/state-1080p.json');
    expect(safeStateSource('https://evil.example/state.json')).toBeNull();
    expect(safeStateSource('//evil.example/state.json')).toBeNull();
    expect(safeStateSource('../../etc/passwd.json')).toBeNull();
    expect(safeStateSource('dev/state.txt')).toBeNull();
    expect(safeStateSource(null)).toBeNull();
  });

  it('is read from the query string alongside the other switches', () => {
    expect(readParams('?mock=1&state=dev/state-1080p.json').state).toBe('dev/state-1080p.json');
    expect(readParams('?state=https://evil.example/x.json').state).toBeNull();
  });
});

describe('page typography', () => {
  it('marks up only styles the pages know how to draw', () => {
    for (const name of ['config.html', 'live_config.html']) {
      const used = [...page(name).matchAll(/data-font="([^"]*)"/g)].map((match) => match[1]);

      expect(used.length).toBeGreaterThan(0);
      for (const style of used) expect(Object.keys(STYLES)).toContain(style);
    }
  });

  it('draws the title big and the body small, as the game does', () => {
    expect(STYLES.title.font).toBe('big');
    expect(STYLES.heading.font).toBe('medium');
    expect(STYLES.body.font).toBe('small');
  });

  it('leaves the token selectable rather than drawing it', () => {
    expect(page('config.html')).toContain('<code data-token-value></code>');
    expect(page('config.html')).not.toMatch(/data-token-value[^>]*data-font/);
  });
});

describe('interface scale', () => {
  it('offers the steps the HD Mod itself has', () => {
    expect(UI_SCALES).toEqual([1, 1.25, 1.5, 1.75, 2, 3, 4]);
  });

  it('keeps the scale the backend sent when the contract allows it', () => {
    expect(readUiScale(1)).toBe(1);
    expect(readUiScale(1.75)).toBe(1.75);
    expect(readUiScale(4)).toBe(4);
  });

  it('falls back to a plain 1 for anything the contract does not allow', () => {
    expect(readUiScale(undefined)).toBe(DEFAULT_UI_SCALE);
    expect(readUiScale(null)).toBe(DEFAULT_UI_SCALE);
    expect(readUiScale('1.5')).toBe(DEFAULT_UI_SCALE);
    expect(readUiScale(0.5)).toBe(DEFAULT_UI_SCALE);
    expect(readUiScale(5)).toBe(DEFAULT_UI_SCALE);
    expect(readUiScale(Number.NaN)).toBe(DEFAULT_UI_SCALE);
  });

  it('writes a scale the way the contract spells it', () => {
    expect(UI_SCALES.map(formatUiScale)).toEqual(['1', '1.25', '1.5', '1.75', '2', '3', '4']);
  });

  it('offers a channel its own scale when it is not one of the steps', () => {
    expect(uiScaleOptions(1.5)).toEqual(UI_SCALES);
    expect(uiScaleOptions(1.4)).toEqual([1, 1.25, 1.4, 1.5, 1.75, 2, 3, 4]);
    // Each value is offered once, however often it is read back.
    expect(uiScaleOptions(1.4)).toHaveLength(UI_SCALES.length + 1);
  });

  it('holds the streamer choice until the backend answers with it', () => {
    // Nothing in flight: whatever the channel holds is what is shown.
    expect(settleUiScale(null, 1.5)).toEqual({ show: 1.5, chosen: null });
    // A poll landing while the choice is on its way must not undo it.
    expect(settleUiScale(2, 1.5)).toEqual({ show: 2, chosen: 2 });
    // Once the channel reports the choice, it stops being held.
    expect(settleUiScale(2, 2)).toEqual({ show: 2, chosen: null });
  });
});

describe('page parameters', () => {
  it('reads the mock and debug switches', () => {
    expect(readParams('?mock=1&debug=1')).toEqual({ mock: true, debug: true, background: null, state: null });
    expect(readParams('')).toEqual({ mock: false, debug: false, background: null, state: null });
  });

  it('accepts only a bundled image as the calibration background', () => {
    expect(safeBackground('dev/sample-2560x1440.jpg')).toBe('dev/sample-2560x1440.jpg');
    expect(safeBackground('assets/ui/popup-hero.png')).toBe('assets/ui/popup-hero.png');
    expect(safeBackground('https://example.com/x.png')).toBeNull();
    expect(safeBackground('//example.com/x.png')).toBeNull();
    expect(safeBackground('/etc/passwd')).toBeNull();
    expect(safeBackground('../../secret.png')).toBeNull();
    expect(safeBackground('javascript:alert(1)')).toBeNull();
    expect(safeBackground('page.html')).toBeNull();
    expect(safeBackground(null)).toBeNull();
  });

  it('rejects values that escape the CSS url() the background is written into', () => {
    const payloads = [
      'a.png"),url("https://evil.example/beacon.png',
      '\thttps://evil.example/beacon.png',
      'htt\tps://evil.example/beacon.png',
      '\\68ttps://evil.example/beacon.png',
      'a.png"); background: red; x:("',
      'a.png\n.png',
    ];

    for (const payload of payloads) expect(safeBackground(payload)).toBeNull();
  });
});

describe('id tables', () => {
  it('names the HotA classes and towns', () => {
    expect(heroClassName(21)).toBe('Artificer');
    expect(heroClassName(22)).toBe('Chieftain');
    expect(heroClassName(23)).toBe('Elder');
    expect(townTypeName(9)).toBe('Cove');
    expect(townTypeName(11)).toBe('Bulwark');
  });

  it('names the spells the spike confirmed in the game', () => {
    expect(spellName(15)).toBe('Magic Arrow');
    expect(spellName(9)).toBe('Town Portal');
    expect(spellName(59)).toBe('Berserk');
  });

  it('falls back for ids beyond the tables', () => {
    expect(heroClassName(99)).toBe('Class 99');
    expect(spellName(70)).toBe('Spell 70');
  });

  it('maps town pictures to the fortified and unfortified frame sets', () => {
    expect(townPicture(0, 3)).toBe('assets/towns/0.png');
    expect(townPicture(10, 3)).toBe('assets/towns/20.png');
    expect(townPicture(10, 0)).toBe('assets/towns/44.png');
  });

  it('maps skill and primary icons', () => {
    expect(skillIcon(7, 3)).toBe('assets/skills/7_3.png');
    expect(primaryIcon(0)).toBe('assets/primary/attack.png');
    expect(primaryIcon(3)).toBe('assets/primary/knowledge.png');
  });
});

describe('fortification icons', () => {
  it('maps the protocol levels onto the three exported frames', () => {
    expect(fortIcon(0)).toBeNull();
    expect(fortIcon(1)).toBe('assets/ui/fort-0.png');
    expect(fortIcon(2)).toBe('assets/ui/fort-1.png');
    expect(fortIcon(3)).toBe('assets/ui/fort-2.png');
  });
});
