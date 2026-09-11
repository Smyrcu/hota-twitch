import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { RGB, readHeader, readPixelBytes, scanline } from './png.mjs';
import { ASSETS, SCREENSHOT } from './requirements.mjs';

/**
 * Reads back every file the renderer wrote and holds it against the console's requirements. The
 * console rejects a wrong size without saying which file was wrong, so the mismatch is named
 * here instead. The size is taken from the file rather than from what was asked for, header and
 * pixels both, so an encoder that wrote a size it did not fill is caught as well.
 */
export async function check(directory, screenshots) {
  const expected = [
    ...ASSETS.map((asset) => ({ ...asset, colourType: RGB })),
    ...screenshots.map((file) => ({ ...SCREENSHOT, file, colourType: RGB })),
  ];

  const problems = [];
  for (const want of expected) {
    const path = join(directory, want.file);
    let bytes;
    try {
      bytes = await readFile(path);
    } catch {
      problems.push(`${want.file}: not written`);
      continue;
    }
    const png = readHeader(bytes);
    if (png === null) {
      problems.push(`${want.file}: not a PNG`);
      continue;
    }
    if (png.width !== want.width || png.height !== want.height) {
      problems.push(
        `${want.file}: ${png.width}x${png.height}, the console wants ${want.width}x${want.height}`,
      );
    }
    if (png.colourType !== want.colourType) {
      problems.push(`${want.file}: colour type ${png.colourType}, expected ${want.colourType}`);
    } else {
      const pixels = readPixelBytes(bytes);
      const expected = scanline(png.width) * png.height;
      if (pixels !== expected) {
        problems.push(`${want.file}: ${pixels} bytes of image data, ${expected} for its header`);
      }
    }
    if (bytes.length > want.maxBytes) {
      problems.push(`${want.file}: ${bytes.length} bytes, over the ${want.maxBytes}-byte limit`);
    }
  }

  if (problems.length > 0) {
    throw new Error(`the listing assets do not meet the requirements:\n  ${problems.join('\n  ')}`);
  }
  return expected.length;
}
