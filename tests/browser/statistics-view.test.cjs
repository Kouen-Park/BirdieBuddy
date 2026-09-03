const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const context = vm.createContext({ toPar: n => n > 0 ? `+${n}` : n === 0 ? 'E' : String(n) });
vm.runInContext(fs.readFileSync(path.join(__dirname, '../../wwwroot/js/statistics-view.js'), 'utf8'), context);

test('summary labels each round length and never combines raw totals', () => {
  const html = context.roundLengthSummary({ byRoundLength: [
    { holeCount: 9, roundsPlayed: 1, averageScore: 45, bestScore: 45, averageScoreToPar: 9 },
    { holeCount: 18, roundsPlayed: 5, averageScore: 90, bestScore: 80, averageScoreToPar: 18, recentFiveScoreToPar: 18 }
  ] });
  assert.match(html, /9 holes/);
  assert.match(html, /18 holes/);
  assert.match(html, /45\.0/);
  assert.match(html, /90\.0/);
  assert.match(html, /Not enough rounds/);
  assert.match(html, /\+18/);
  assert.doesNotMatch(html, /undefined|NaN/);
});

test('empty groups render an accessible table without fabricated averages', () => {
  const html = context.roundLengthSummary({ byRoundLength: [] });
  assert.match(html, /<caption>/);
  assert.doesNotMatch(html, /undefined|NaN|<td>/);
});
