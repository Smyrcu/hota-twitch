import { spawn } from 'node:child_process';
import { mkdir } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from '@playwright/test';

const here = dirname(fileURLToPath(import.meta.url));
const shots = join(here, 'shots');
const port = 5199;
const url = `http://localhost:${port}/video_overlay.html?mock=1&bg=dev/sample-2560x1440.jpg`;

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
    const page = await browser.newPage({ viewport: { width: 1920, height: 1080 } });
    await page.goto(url);
    await page.waitForSelector('.zone', { state: 'attached' });
    await page.waitForTimeout(500);

    const zones = page.locator('.zone');
    const shoot = async (name) => {
        await page.waitForTimeout(250);
        await page.screenshot({ path: join(shots, `${name}.png`) });
        console.log(`dev/shots/${name}.png`);
    };

    await zones.nth(0).hover();
    await shoot('hero-hover');
    await zones.nth(0).click();
    await shoot('hero-expanded');

    await zones.nth(4).hover();
    await shoot('town-hover');
    await zones.nth(4).click();
    await shoot('town-expanded');

    await browser.close();
} finally {
    server.kill();
}
