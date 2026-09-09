import { createReadStream } from 'node:fs';
import { stat } from 'node:fs/promises';
import { createServer } from 'node:http';
import { dirname, extname, join, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const roots = { '/dev/': here, '/': join(here, '..', 'dist') };
const port = Number(process.env.PORT ?? 5173);

const TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
};

function resolve(pathname) {
  for (const [prefix, root] of Object.entries(roots)) {
    if (!pathname.startsWith(prefix)) continue;
    const relative = normalize(pathname.slice(prefix.length));
    if (relative.startsWith('..')) return null;
    return join(root, relative);
  }
  return null;
}

createServer(async (request, response) => {
  const pathname = new URL(request.url ?? '/', 'http://localhost').pathname;
  const file = resolve(pathname === '/' ? '/video_overlay.html' : pathname);
  if (file === null) {
    response.writeHead(400).end('bad path');
    return;
  }
  try {
    await stat(file);
  } catch {
    response.writeHead(404).end('not found');
    return;
  }
  response.writeHead(200, {
    'content-type': TYPES[extname(file)] ?? 'application/octet-stream',
    'cache-control': 'no-store',
  });
  createReadStream(file).pipe(response);
}).listen(port, () => console.log(`http://localhost:${port}/video_overlay.html?mock=1&debug=1`));
