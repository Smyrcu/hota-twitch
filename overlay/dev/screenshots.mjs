import { spawn } from 'node:child_process';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from '@playwright/test';

const here = dirname(fileURLToPath(import.meta.url));
const shots = join(here, 'shots');
const port = 5199;

/**
 * The window the shots show: 1920x1080 with the HD Mod interface at 1.5, the calibration
 * reference. Only `display` differs from the hand-written document, so the fixture is derived
 * from it rather than kept as a second copy that could drift.
 */
const DISPLAY = { width: 1920, height: 1080, uiScale: 1.5 };
const fixture = 'dev/generated/state-1080p.json';

/*
 * The background is the 2560x1440 sample enlarged by half with its top-right corner kept, which
 * is the same window at 1920x1080 with the interface at 1.5: the panel hangs off that corner and
 * is multiplied by the scale. It shows the cards at the size the panel is drawn at. It does not
 * prove the anchoring model — both the crop and the zones follow the same measurements.
 */
const url =
  `http://localhost:${port}/video_overlay.html` +
  `?mock=1&state=${fixture}&bg=dev/sample-1920x1080-scale1_5.jpg`;

const state = JSON.parse(await readFile(join(here, 'state.json'), 'utf8'));
await mkdir(join(here, 'generated'), { recursive: true });
await writeFile(join(here, '..', fixture), `${JSON.stringify({ ...state, display: DISPLAY }, null, 2)}\n`);

/** The lists share one row of zones: the heroes come first, so the towns start after them. */
const firstTown = state.heroes.length;

/** A player showing the stream one to one, and one showing it at two thirds. */
const PLAYERS = [
  { width: 1920, height: 1080 },
  { width: 1280, height: 720 },
];

async function waitForServer() {
  for (let attempt = 0; attempt < 50; attempt += 1) {
    try {
      const response = await fetch(`http://localhost:${port}/video_overlay.html`);
      if (response.ok) return;
    } catch {
      /* not up yet */
    }
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  throw new Error('the dev server did not start');
}

const server = spawn(process.execPath, [join(here, 'serve.mjs')], {
  env: { ...process.env, PORT: String(port) },
  stdio: 'ignore',
});

try {
  await mkdir(shots, { recursive: true });
  await waitForServer();

  const browser = await chromium.launch();

  for (const viewport of PLAYERS) {
    const size = `${viewport.width}x${viewport.height}`;
    const page = await browser.newPage({ viewport });
    await page.goto(url);
    await page.waitForSelector('.zone', { state: 'attached' });
    await page.waitForTimeout(500);

    const zones = page.locator('.zone');
    const shoot = async (name) => {
      await page.waitForTimeout(250);
      await page.screenshot({ path: join(shots, `${name}-${size}.png`) });
      console.log(`dev/shots/${name}-${size}.png`);
    };

    await zones.nth(0).hover();
    await shoot('hero-hover');
    await zones.nth(0).click();
    await shoot('hero-expanded');

    await zones.nth(firstTown).hover();
    await shoot('town-hover');
    await zones.nth(firstTown).click();
    await shoot('town-expanded');

    await page.close();
  }

  await browser.close();
} finally {
  server.kill();
}
