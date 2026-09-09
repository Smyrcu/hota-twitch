import { describe, expect, it } from 'vitest';
import { SpriteCache } from '../src/render/sprites.js';

/** The cache needs only these three members of an image element. */
interface FakeImage {
  src: string;
  naturalWidth: number;
  addEventListener(type: string, listener: () => void): void;
  fire(type: 'load' | 'error'): void;
}

function fakeImages(): { created: FakeImage[]; create: () => HTMLImageElement } {
  const created: FakeImage[] = [];
  const create = (): HTMLImageElement => {
    const listeners = new Map<string, () => void>();
    const image: FakeImage = {
      src: '',
      naturalWidth: 0,
      addEventListener: (type, listener) => void listeners.set(type, listener),
      fire: (type) => listeners.get(type)?.(),
    };
    created.push(image);
    return image as unknown as HTMLImageElement;
  };
  return { created, create };
}

describe('sprite cache', () => {
  it('requests a sprite once and serves it after it loads', () => {
    const images = fakeImages();
    const cache = new SpriteCache(
      () => {},
      () => 0,
      images.create,
    );

    expect(cache.get('a.png')).toBeNull();
    expect(images.created).toHaveLength(1);

    images.created[0]!.naturalWidth = 32;
    images.created[0]!.fire('load');

    expect(cache.get('a.png')).not.toBeNull();
    expect(images.created).toHaveLength(1);
  });

  it('does not re-request a failed sprite on every repaint', () => {
    const images = fakeImages();
    let clock = 1000;
    const cache = new SpriteCache(
      () => {},
      () => clock,
      images.create,
    );

    cache.get('missing.png');
    images.created[0]!.fire('error');

    for (let repaint = 0; repaint < 20; repaint += 1) expect(cache.get('missing.png')).toBeNull();
    expect(images.created).toHaveLength(1);
  });

  it('retries a failed sprite once the retry window has passed', () => {
    const images = fakeImages();
    let clock = 1000;
    const cache = new SpriteCache(
      () => {},
      () => clock,
      images.create,
    );

    cache.get('missing.png');
    images.created[0]!.fire('error');

    clock += 59_999;
    cache.get('missing.png');
    expect(images.created).toHaveLength(1);

    clock += 2;
    cache.get('missing.png');
    expect(images.created).toHaveLength(2);
  });

  it('treats a load with no pixels as a failure', () => {
    const images = fakeImages();
    const cache = new SpriteCache(
      () => {},
      () => 0,
      images.create,
    );

    cache.get('empty.png');
    images.created[0]!.fire('load');

    expect(cache.get('empty.png')).toBeNull();
  });
});
