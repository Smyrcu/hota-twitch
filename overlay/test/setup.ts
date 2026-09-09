/** esbuild injects this at build time (see build.mjs); tests supply it themselves. */
(globalThis as Record<string, unknown>)['__BACKEND_URL__'] = 'https://backend.test';
