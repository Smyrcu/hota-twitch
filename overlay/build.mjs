import { cp, mkdir, readdir, rm, stat } from 'node:fs/promises';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import * as esbuild from 'esbuild';

const run = promisify(execFile);
const here = dirname(fileURLToPath(import.meta.url));
const dist = join(here, 'dist');
const packageDir = join(here, 'package');
const zipName = 'hota-twitch-overlay.zip';

const backendUrl = process.env.HOTA_BACKEND_URL ?? 'https://hota.smyrcu.net';

async function bundle() {
    await esbuild.build({
        entryPoints: {
            'video-overlay': join(here, 'src/pages/video-overlay.ts'),
            config: join(here, 'src/pages/config.ts'),
        },
        outdir: dist,
        bundle: true,
        format: 'iife',
        target: ['chrome110', 'firefox110', 'safari16'],
        minify: true,
        legalComments: 'none',
        define: { __BACKEND_URL__: JSON.stringify(backendUrl) },
        logLevel: 'warning',
    });
}

async function copyStatic() {
    await cp(join(here, 'public'), dist, { recursive: true });
    await cp(join(here, 'assets'), join(dist, 'assets'), { recursive: true });
}

async function listFiles(root, base = root) {
    const entries = await readdir(root, { withFileTypes: true });
    const files = [];
    for (const entry of entries) {
        const full = join(root, entry.name);
        if (entry.isDirectory()) files.push(...(await listFiles(full, base)));
        else files.push({ path: relative(base, full), size: (await stat(full)).size });
    }
    return files.sort((a, b) => a.path.localeCompare(b.path));
}

async function makeZip() {
    await rm(packageDir, { recursive: true, force: true });
    await mkdir(packageDir, { recursive: true });
    await run('zip', ['-r', '-q', '-X', join(packageDir, zipName), '.'], { cwd: dist });
    return (await stat(join(packageDir, zipName))).size;
}

const kib = (bytes) => `${(bytes / 1024).toFixed(1)} KiB`;

await rm(dist, { recursive: true, force: true });
await mkdir(dist, { recursive: true });
await bundle();
await copyStatic();

const files = await listFiles(dist);
const total = files.reduce((sum, file) => sum + file.size, 0);
console.log(`dist: ${files.length} files, ${kib(total)}`);
for (const file of files.filter((file) => !file.path.startsWith('assets/'))) {
    console.log(`  ${file.path} — ${kib(file.size)}`);
}
const assets = files.filter((file) => file.path.startsWith('assets/'));
console.log(`  assets/ — ${assets.length} files, ${kib(assets.reduce((sum, file) => sum + file.size, 0))}`);

if (process.argv.includes('--package')) {
    console.log(`package/${zipName}: ${kib(await makeZip())}`);
}
