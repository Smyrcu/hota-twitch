import { mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from '@playwright/test';
import { check } from './check.mjs';
import { createRasteriser } from './rasteriser.mjs';
import { capture, requireRecording } from './screenshots.mjs';
import { ASSETS, DOCUMENTATION, SCREENSHOT, WORDMARK_FONT } from './requirements.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const out = join(here, 'out');

/** The card's own ground, so an asset never shows a transparent or a white edge. */
const GROUND = '#241a10';

const write = async (file, bytes) => writeFile(join(out, file), bytes);

await requireRecording();
// Emptied rather than written over, so a file left by an earlier set of names is not uploaded.
await rm(out, { recursive: true, force: true });
await mkdir(out, { recursive: true });

const browser = await chromium.launch();
try {
  const rasteriser = await createRasteriser(browser, GROUND);
  if (!(await rasteriser.hasFont(WORDMARK_FONT))) {
    throw new Error(`the word mark needs the ${WORDMARK_FONT} font, which is not installed`);
  }

  for (const asset of ASSETS) {
    const svg = await readFile(join(here, asset.source), 'utf8');
    await write(asset.file, await rasteriser.svg(svg, asset.width, asset.height));
    console.log(`${asset.file}  ${asset.width}x${asset.height}  ${asset.field}`);
  }

  const screenshots = await capture(browser, rasteriser, write, (file, clip) => {
    const factor = (SCREENSHOT.width / clip.width).toFixed(3);
    console.log(
      `${file}  ${SCREENSHOT.width}x${SCREENSHOT.height}  ` +
        `from ${clip.width}x${clip.height} at ${clip.x},${clip.y} (x${factor})`,
    );
  });

  const count = await check(out, screenshots);
  console.log(`\n${count} files in listing/out meet ${DOCUMENTATION}`);
} finally {
  await browser.close();
}
