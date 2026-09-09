import { describe, expect, it } from 'vitest';
import { describeConnection } from '../src/config/status.js';
import { readParams, safeBackground } from '../src/dev/params.js';
import { fortIcon, primaryIcon, skillIcon, townPicture } from '../src/data/sprites.js';
import { heroClassName, spellName, townTypeName } from '../src/data/names.js';

const NOW = Date.parse('2026-09-09T22:00:30Z');

describe('connection status', () => {
  it('asks for a token first', () => {
    expect(describeConnection(false, null, NOW).connected).toBe(false);
    expect(describeConnection(false, null, NOW).text).toContain('hota-twitch.ini');
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

describe('page parameters', () => {
  it('reads the mock and debug switches', () => {
    expect(readParams('?mock=1&debug=1')).toEqual({ mock: true, debug: true, background: null });
    expect(readParams('')).toEqual({ mock: false, debug: false, background: null });
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
