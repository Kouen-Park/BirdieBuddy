const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../../wwwroot/js/practice.js'), 'utf8');
async function render(insights) {
  const element = { innerHTML: '' };
  const context = vm.createContext({
    renderNav() {}, document: { getElementById: () => element },
    Api: { get: async () => ({ insights }) },
    escapeHtml: value => String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;')
  });
  vm.runInContext(source, context);
  await new Promise(resolve => setImmediate(resolve));
  return element.innerHTML;
}
test('practice renders drill steps and safe evidence links with encoded content', async () => {
  const html = await render([{ title: '<img src=x>', evidence: '<script>bad</script>', recommendation: 'Try a block', severity: 'medium',
    evidenceRoundIds: [3, 'javascript:bad'], drill: { title: 'Target', minutes: 20, steps: ['<b>One</b>', 'Two'], measure: 'Count hits' } }]);
  assert.match(html, /&lt;img/);
  assert.doesNotMatch(html, /<script>|<img|href="javascript/);
  assert.match(html, /round-details.html\?id=3/);
  assert.match(html, /<summary>Target · 20 min/);
  assert.match(html, /Count hits/);
});
test('baseline guidance does not invent a practice drill', async () => {
  const html = await render([{ title: 'Baseline', evidence: '1 of 3', recommendation: 'Two more rounds', severity: 'info' }]);
  assert.match(html, /Next checkpoint/);
  assert.doesNotMatch(html, /practice-drill/);
});
