import { spawn } from 'node:child_process';
import { createServer } from 'node:net';
import { mkdir, open, readFile, rm, stat, symlink } from 'node:fs/promises';
import { homedir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readHeader } from './png.mjs';
import { SCREENSHOT } from './requirements.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const overlay = join(here, '..');

/**
 * The frame and the state it belongs to: a recording of a real game. The picture is the game's,
 * so it is not kept in the repository; the renderer reads it from where it lies and links it
 * into the harness's own untracked directory rather than copying it in. `HOTA_RECORDING` moves
 * the directory for anyone whose copy is elsewhere.
 */
const RECORDING = process.env.HOTA_RECORDING ?? join(homedir(), 'hota-twitch', 'overlay', 'dev');
const FRAME = 'live-2560x1440.png';
const STATE = 'snap.json';
const LINKED = join(overlay, 'dev', 'generated');

/** Breathing room around the card; the panel it belongs to sits at the right edge of the frame. */
const MARGIN = 24;

/**
 * Hovering shows the popup; the first click on the same entry expands it — a town's later clicks
 * step through the heroes standing in it, which these shots do not need. Nothing has to be undone
 * between shots: moving to another entry drops the selection the last one held.
 */
const SHOTS = [
  { name: 'hero-hover', list: 'hero', row: 7, expand: false },
  { name: 'hero-expanded', list: 'hero', row: 7, expand: true },
  { name: 'town-hover', list: 'town', row: 0, expand: false },
  { name: 'town-expanded', list: 'town', row: 0, expand: true },
];

/** The frame is tens of megabytes; only its header is needed to learn the size it was taken at. */
async function head(file) {
  const handle = await open(file);
  try {
    const buffer = Buffer.alloc(26);
    await handle.read(buffer, 0, buffer.length, 0);
    return buffer;
  } finally {
    await handle.close();
  }
}

async function freePort() {
  const server = createServer();
  await new Promise((resolve) => server.listen(0, resolve));
  const { port } = server.address();
  await new Promise((resolve) => server.close(resolve));
  return port;
}

/** Fails before anything is launched or written when the recording is not where it should be. */
export async function requireRecording() {
  for (const name of [FRAME, STATE]) {
    try {
      await stat(join(RECORDING, name));
    } catch {
      throw new Error(`the recording is missing: ${join(RECORDING, name)}`);
    }
  }
}

async function link() {
  await mkdir(LINKED, { recursive: true });
  for (const name of [FRAME, STATE]) {
    await rm(join(LINKED, name), { force: true });
    await symlink(join(RECORDING, name), join(LINKED, name));
  }
}

/** A dev server that binds but never announces itself would otherwise be waited on forever. */
const START_TIMEOUT_MS = 15_000;

function serve(port) {
  const server = spawn(process.execPath, [join(overlay, 'dev', 'serve.mjs')], {
    env: { ...process.env, PORT: String(port) },
    stdio: ['ignore', 'pipe', 'inherit'],
  });
  const ready = new Promise((resolve, reject) => {
    const timer = setTimeout(
      () => settle(() => reject(new Error(`the dev server said nothing in ${START_TIMEOUT_MS} ms`))),
      START_TIMEOUT_MS,
    );
    const settle = (outcome) => {
      clearTimeout(timer);
      server.off('error', fail);
      server.off('exit', quit);
      outcome();
    };
    const fail = (error) => settle(() => reject(error));
    const quit = (code) => settle(() => reject(new Error(`the dev server stopped with code ${code}`)));
    server.stdout.once('data', () => settle(resolve));
    server.once('error', fail);
    server.once('exit', quit);
  });
  return { server, ready };
}

/** The card repaints as its sprites arrive, so a shot is taken once two readings agree. */
async function stableCard(page) {
  let last = '';
  for (let attempt = 0; attempt < 40; attempt += 1) {
    const now = await page.evaluate(() => {
      const canvas = document.querySelector('.card');
      return canvas === null || canvas.hidden ? '' : canvas.toDataURL();
    });
    if (now !== '' && now === last) return;
    last = now;
    await page.waitForTimeout(250);
  }
  throw new Error('the card never settled');
}

const gcd = (a, b) => (b === 0 ? a : gcd(b, a % b));

/** The required size in its lowest terms, so a region can grow in whole steps of it. */
const STEP = (() => {
  const divisor = gcd(SCREENSHOT.width, SCREENSHOT.height);
  return { width: SCREENSHOT.width / divisor, height: SCREENSHOT.height / divisor };
})();

/**
 * The region a shot is cut from: whole steps of the required ratio, enough of them to hold the
 * card and the panel strip beside it, centred on the pair and kept inside the frame. Counting
 * steps rather than fitting a box to the content keeps the ratio the console insists on exact
 * in both axes, so the reduction to the required size never stretches the picture.
 *
 * The box is taken from the card, not from the lists it belongs to. An expanded card is several
 * times the height of the popup, and growing the box to the top of the lists as well would
 * spend the whole frame on map and shrink the card to less than half its pixels: the card is
 * the subject, and the panel beside it is what places it.
 */
function region(card, frame) {
  const left = card.x - MARGIN;
  const right = frame.width;
  const top = card.y - MARGIN;
  const bottom = card.y + card.height + MARGIN;

  const steps = Math.max(
    SCREENSHOT.width / STEP.width,
    Math.ceil((right - left) / STEP.width),
    Math.ceil((bottom - top) / STEP.height),
  );
  const size = { width: steps * STEP.width, height: steps * STEP.height };
  if (size.width > frame.width || size.height > frame.height) {
    throw new Error(
      `a ${Math.round(right - left)}x${Math.round(bottom - top)} card and panel need a ` +
        `${size.width}x${size.height} crop, which a ${frame.width}x${frame.height} frame cannot give`,
    );
  }

  const place = (low, high, span, limit) =>
    Math.round(Math.max(0, Math.min((low + high - span) / 2, limit - span)));
  return {
    x: place(left, right, size.width, frame.width),
    y: place(top, bottom, size.height, frame.height),
    width: size.width,
    height: size.height,
  };
}

/**
 * Which hover target belongs to which list. The overlay lays the two lists out in two columns,
 * the heroes to the left of the towns, and emits a target only for a row the list actually
 * shows, so the targets are counted off the page rather than off the state: a shorter or a
 * scrolled list would otherwise silently move a town shot onto a hero.
 */
async function columns(page) {
  const placed = await page.$$eval('.zone', (nodes) =>
    nodes.map((node, index) => {
      const rect = node.getBoundingClientRect();
      return { index, x: Math.round(rect.x), y: Math.round(rect.y) };
    }),
  );
  const lefts = [...new Set(placed.map((zone) => zone.x))].sort((a, b) => a - b);
  if (lefts.length !== 2) {
    throw new Error(`the panel shows ${lefts.length} lists of hover targets, expected two`);
  }
  const column = (left) =>
    placed
      .filter((zone) => zone.x === left)
      .sort((a, b) => a.y - b.y)
      .map((zone) => zone.index);
  return { hero: column(lefts[0]), town: column(lefts[1]) };
}

export async function capture(browser, rasteriser, write, report) {
  await requireRecording();
  await link();
  const state = JSON.parse(await readFile(join(RECORDING, STATE), 'utf8'));
  const frame = readHeader(await head(join(RECORDING, FRAME)));
  if (frame === null) throw new Error(`${join(RECORDING, FRAME)} is not a PNG`);
  if (frame.width !== state.display.width || frame.height !== state.display.height) {
    throw new Error(
      `the recording disagrees with itself: the frame is ${frame.width}x${frame.height}, ` +
        `the state says ${state.display.width}x${state.display.height}`,
    );
  }

  const port = await freePort();
  const { server, ready } = serve(port);
  const files = [];
  let page = null;
  try {
    await ready;
    page = await browser.newPage({ viewport: { width: frame.width, height: frame.height } });
    await page.goto(
      `http://localhost:${port}/video_overlay.html` +
        `?mock=1&state=dev/generated/${STATE}&bg=dev/generated/${FRAME}`,
    );
    await page.waitForSelector('.zone', { state: 'attached' });
    const zones = page.locator('.zone');
    const lists = await columns(page);

    for (const shot of SHOTS) {
      const index = lists[shot.list][shot.row];
      if (index === undefined) {
        throw new Error(`the ${shot.list} list does not show a row ${shot.row}`);
      }
      await zones.nth(index).hover();
      if (shot.expand) await zones.nth(index).click();
      await stableCard(page);

      const card = await page.evaluate(() => {
        const rect = document.querySelector('.card').getBoundingClientRect();
        return { x: rect.x, y: rect.y, width: rect.width, height: rect.height };
      });
      const clip = region(card, frame);
      const png = await rasteriser.fit(
        await page.screenshot({ clip }),
        SCREENSHOT.width,
        SCREENSHOT.height,
      );
      const file = `${shot.name}.png`;
      await write(file, png);
      files.push(file);
      report(file, clip);
    }
  } finally {
    if (page !== null) await page.close();
    server.kill();
  }
  return files;
}
