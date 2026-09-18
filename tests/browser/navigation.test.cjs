const test = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');

function setup() {
  const document = { activeElement: null };
  const node = () => {
    const classes = new Set();
    return { dataset: {}, style: {}, attrs: {}, events: {}, inert: false,
      classList: { add: x => classes.add(x), remove: x => classes.delete(x), contains: x => classes.has(x) },
      setAttribute(k, v) { this.attrs[k] = v; },
      addEventListener(k, v) { this.events[k] = v; },
      focus() { document.activeElement = this; }, getClientRects: () => [1] };
  };
  const nav = node(), main = node(), menu = node(), close = node(), logout = node(), overlay = node();
  nav.querySelectorAll = () => [close, logout];
  document.body = node();
  document.documentElement = node();
  document.getElementById = id => ({ 'site-nav': nav, 'menu-button': menu, 'sidebar-close': close }[id]);
  document.querySelector = selector => ({ '.mobile-header': node(), '.nav-overlay': overlay, main }[selector]);
  const media = { matches: true, addEventListener(_, fn) { this.change = fn; } };
  const window = { scrollY: 420, matchMedia: () => media, requestAnimationFrame: fn => fn(), scrollTo({ top }) { this.scrollY = top; } };
  const context = vm.createContext({ document, window });
  vm.runInContext(fs.readFileSync(path.join(__dirname, '../../wwwroot/js/api.js'), 'utf8'), context);
  context.setupMobileNavigation();
  return { nav, main, menu, close, logout, overlay, document, window, media };
}

test('mobile menu locks the background and restores focus and scroll on Escape', () => {
  const x = setup();
  assert.equal(x.nav.inert, true);
  x.menu.events.click();
  assert.equal(x.main.inert, true);
  assert.equal(x.nav.inert, false);
  assert.equal(x.document.documentElement.classList.contains('menu-open'), true);
  assert.equal(x.document.activeElement, x.close);
  x.nav.events.keydown({ key: 'Escape', preventDefault() {} });
  assert.equal(x.window.scrollY, 420);
  assert.equal(x.document.activeElement, x.menu);
  assert.equal(x.nav.inert, true);
  assert.equal(x.main.inert, false);
  assert.equal(x.document.documentElement.classList.contains('menu-open'), false);
});

test('Tab stays inside the open drawer and desktop resizing releases all locks', () => {
  const x = setup(); x.menu.events.click();
  x.nav.events.keydown({ key: 'Tab', shiftKey: true, preventDefault() {} });
  assert.equal(x.document.activeElement, x.logout);
  x.nav.events.keydown({ key: 'Tab', shiftKey: false, preventDefault() {} });
  assert.equal(x.document.activeElement, x.close);
  x.media.matches = false; x.media.change();
  assert.equal(x.main.inert, false);
  assert.equal(x.nav.inert, false);
  assert.equal(x.menu.attrs['aria-expanded'], 'false');
  assert.equal(x.document.body.classList.contains('menu-open'), false);
});

test('the root path stays public and renders guest account actions', async () => {
  const account = { innerHTML: '' };
  let redirectedTo = null;
  const document = {
    getElementById: id => id === 'sidebar-account' ? account : null
  };
  const window = {
    location: {
      pathname: '/',
      search: '',
      replace(value) { redirectedTo = value; }
    }
  };
  const context = vm.createContext({
    document,
    window,
    fetch: async () => ({ status: 401, ok: false })
  });

  vm.runInContext(fs.readFileSync(path.join(__dirname, '../../wwwroot/js/api.js'), 'utf8'), context);
  await context.hydrateCurrentUser();

  assert.equal(redirectedTo, null);
  assert.match(account.innerHTML, /Browse without an account/);
  assert.match(account.innerHTML, /Create account/);
  assert.match(account.innerHTML, /Sign in/);
});

test('protected pages show a sign-in container instead of redirecting on 401', async () => {
  const main = {
    dataset: {},
    replaceChildren(node) { this.child = node; }
  };
  const document = {
    querySelector: selector => selector === 'main' ? main : null,
    createElement: () => ({
      className: '',
      attrs: {},
      setAttribute(key, value) { this.attrs[key] = value; }
    })
  };
  const window = {
    location: { pathname: '/statistics.html', search: '?courseId=4' }
  };
  const context = vm.createContext({
    document,
    window,
    fetch: async () => ({ status: 401, ok: false })
  });

  vm.runInContext(fs.readFileSync(path.join(__dirname, '../../wwwroot/js/api.js'), 'utf8'), context);
  await assert.rejects(vm.runInContext('Api.get("/statistics/overview")', context), error => error.authRequired === true);

  assert.equal(main.dataset.authRequired, 'true');
  assert.match(main.child.innerHTML, /See how your game is moving/);
  assert.match(main.child.innerHTML, /\/login\.html\?returnUrl=/);
});
