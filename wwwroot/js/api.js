// Thin fetch wrapper around the Birdie Buddy API. Every page script uses this
// instead of calling fetch() directly so error handling stays in one place.
const Api = {
  base: '/api',

  async request(path, options = {}) {
    const res = await fetch(this.base + path, {
      headers: { 'Content-Type': 'application/json' },
      ...options
    });

    if (res.status === 204) return null;

    const isJson = res.headers.get('content-type')?.includes('application/json');
    const body = isJson ? await res.json() : null;

    if (!res.ok) {
      const message = body?.error || `Request failed (${res.status})`;
      throw new Error(message);
    }
    return body;
  },

  get(path) { return this.request(path); },
  post(path, data) { return this.request(path, { method: 'POST', body: JSON.stringify(data) }); },
  put(path, data) { return this.request(path, { method: 'PUT', body: JSON.stringify(data) }); },
  del(path) { return this.request(path, { method: 'DELETE' }); }
};

// Builds the sidebar nav and marks the current page active.
function renderNav(active) {
  const items = [
    { href: '/index.html', label: 'Dashboard', key: 'dashboard' },
    { href: '/rounds.html', label: 'Rounds', key: 'rounds' },
    { href: '/add-round.html', label: 'Add Round', key: 'add-round' },
    { href: '/statistics.html', label: 'Statistics', key: 'statistics' },
    { href: '/practice.html', label: 'Practice', key: 'practice' }
  ];

  const nav = document.getElementById('site-nav');
  if (!nav) return;

  nav.innerHTML = `
    <div class="brand"><span class="brand-mark">&#9873;</span> Birdie Buddy</div>
    <ul class="nav-list">
      ${items.map(i => `<li><a href="${i.href}" class="${i.key === active ? 'active' : ''}">${i.label}</a></li>`).join('')}
    </ul>
    <div class="sidebar-footer">Round tracking &amp; performance analysis</div>
  `;
}

function fmtDate(dateStr) {
  const d = new Date(dateStr);
  return d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
}

function toPar(n) {
  if (n === 0) return 'E';
  return n > 0 ? `+${n}` : `${n}`;
}

function toParPillClass(n) {
  if (n < 0) return 'under';
  if (n === 0) return 'even';
  return 'over';
}

// Scorecard marking convention: eagle = double circle, birdie = circle,
// par = plain number, bogey = square, double-bogey-or-worse = double square.
function scoreMarkClass(score, par) {
  const diff = score - par;
  if (diff <= -2) return 'eagle';
  if (diff === -1) return 'birdie';
  if (diff === 1) return 'bogey';
  if (diff >= 2) return 'double';
  return '';
}

function pct(n) {
  return `${Math.round(n)}%`;
}
