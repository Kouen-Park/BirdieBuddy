const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '../..');
const scripts = fs.readdirSync(path.join(root, 'wwwroot/js'))
  .filter(file => file.endsWith('.js'))
  .map(file => fs.readFileSync(path.join(root, 'wwwroot/js', file), 'utf8'))
  .join('\n');

test('frontend stays compatible with the strict CSP', () => {
  assert.doesNotMatch(scripts, /\bonclick\s*=/i);
  assert.doesNotMatch(scripts, /\b(?:alert|prompt|confirm)\s*\(/);
});

test('the offline scorecard shell is discoverable and registered', () => {
  const livePage = fs.readFileSync(path.join(root, 'wwwroot/live-round.html'), 'utf8');
  const api = fs.readFileSync(path.join(root, 'wwwroot/js/api.js'), 'utf8');
  const worker = fs.readFileSync(path.join(root, 'wwwroot/service-worker.js'), 'utf8');
  assert.match(livePage, /manifest\.webmanifest/);
  assert.match(api, /serviceWorker\.register/);
  assert.match(worker, /url\.pathname\.startsWith\('\/api'\)/);
});

test('live scoring exposes distinct durable, syncing, offline and conflict states', () => {
  const live = fs.readFileSync(path.join(root, 'wwwroot/js/live-round.js'), 'utf8');
  for (const state of ['device', 'syncing', 'offline', 'conflict']) {
    assert.match(live, new RegExp(`'${state}'`));
  }
  assert.match(live, /Saved on this device/);
  assert.match(live, /Saved to server/);
  assert.match(live, /review required/i);
});
