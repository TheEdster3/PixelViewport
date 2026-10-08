import assert from 'node:assert/strict';
import { readFile, readdir, access } from 'node:fs/promises';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { fit, constrain, zoomAt, toImage } from '../site/viewport.mjs';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '../site');
const state = fit(1200, 440, 960, 600), anchor = { x: 600, y: 220 };
assert.deepEqual(toImage(zoomAt(state, 2, anchor, 1200, 440, 960, 600), anchor), toImage(state, anchor));
assert.equal(constrain({ zoom: 2, x: -9999, y: 9999 }, 1200, 440, 960, 600).x, -720);
for (const file of (await readdir(root)).filter(file => file.endsWith('.html'))) {
  const html = await readFile(resolve(root, file), 'utf8');
  assert.match(html, /<title>.+<\/title>/);
  assert.doesNotMatch(html, /github\.com\/OWNER/);
  for (const match of html.matchAll(/(?:href|src)="([^"#]+)"/g)) {
    if (!/^(https?:|data:|mailto:)/.test(match[1])) await access(resolve(root, match[1].split('#')[0]));
  }
}
console.log('Product site checks passed: image-space geometry, metadata, local links.');
