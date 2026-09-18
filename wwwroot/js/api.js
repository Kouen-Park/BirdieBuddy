const PUBLIC_BROWSE_PATHS = new Set(['/', '/index.html', '/courses.html']);
const AUTH_ENTRY_PATHS = new Set(['/login.html', '/signup.html']);

function isPublicBrowsePath(pathname = window.location.pathname) {
    return PUBLIC_BROWSE_PATHS.has(pathname);
}

function isPublicPagePath(pathname = window.location.pathname) {
    return isPublicBrowsePath(pathname) || AUTH_ENTRY_PATHS.has(pathname);
}

function renderLoginRequired() {
    const main = document.querySelector('main');
    if (!main || main.dataset.authRequired === 'true') return;

    const previews = {
        '/rounds.html': {
            title: 'Your rounds, all in one place.',
            description: 'Sign in to save rounds and revisit every scorecard.',
            demo: '<div class="card list-card member-demo"><table><caption>Example round history</caption><thead><tr><th>Date</th><th class="text-cell">Course</th><th>Score</th><th>To par</th></tr></thead><tbody><tr><td>18 Sep 2026</td><td class="text-cell">Kauri Cliffs</td><td>84</td><td><span class="pill">+12</span></td></tr><tr><td>07 Sep 2026</td><td class="text-cell">Titirangi</td><td>78</td><td><span class="pill">+6</span></td></tr></tbody></table></div>'
        },
        '/statistics.html': {
            title: 'See how your game is moving.',
            description: 'Sign in to unlock your averages, trends and round breakdowns.',
            demo: '<div class="member-demo"><span class="eyebrow">Example</span><div class="stat-grid"><div class="card stat-card"><div class="stat-label">Completed rounds</div><div class="stat-value">12</div></div><div class="card stat-card"><div class="stat-label">Putts per hole</div><div class="stat-value">1.82</div></div><div class="card stat-card"><div class="stat-label">Average GIR %</div><div class="stat-value">42%</div></div></div></div>'
        },
        '/practice.html': {
            title: 'Build a practice plan around your game.',
            description: 'Sign in to see tailored practice priorities and keep a record of your drills.',
            demo: '<div class="card member-demo practice-preview"><span class="eyebrow">Example practice priority</span><h2>Turn good drives into lower scores</h2><p>Use recent rounds to find the next small improvement.</p><strong>Practice plan and progress tracking unlock after sign in.</strong></div>'
        }
    };
    const preview = previews[window.location.pathname] || {
        title: 'This service requires you to sign in.',
        description: 'Sign in to access this service.',
        demo: ''
    };
    const returnUrl = `${window.location.pathname}${window.location.search}`;
    const section = document.createElement('section');
    section.className = 'auth-required-wrap';
    section.innerHTML = `<div class="card auth-required" aria-labelledby="auth-required-title">
      <span class="eyebrow">Members only</span>
      <h2 id="auth-required-title">${preview.title}</h2>
      <p>${preview.description}</p>
      <a class="btn btn-flag" href="/login.html?returnUrl=${encodeURIComponent(returnUrl)}">Sign in to continue</a>
    </div>${preview.demo}`;
    Array.from(main.children).forEach(child => {
        if (!child.classList.contains('page-header')) child.remove();
    });
    main.append(section);
    main.dataset.authRequired = 'true';
}

// Thin fetch wrapper around the Birdie Buddy API. Every page script uses this
// instead of calling fetch() directly so error handling stays in one place.
const Api = {
    base: '/api',
    csrfToken: null,

    async getCsrfToken() {
        if (this.csrfToken) return this.csrfToken;
        const response = await fetch('/api/security/csrf', {
            credentials: 'same-origin',
            cache: 'no-store',
            headers: { 'Accept': 'application/json' }
        });
        const body = await response.json().catch(() => null);
        if (!response.ok || !body?.token) {
            throw new Error(`Could not initialize a secure session (${response.status}). Refresh the page and try again.`);
        }
        this.csrfToken = body.token;
        return this.csrfToken;
    },

    async request(path, options = {}) {
        const method = (options.method || 'GET').toUpperCase();
        const headers = { 'Content-Type': 'application/json', ...(options.headers || {}) };
        if (!['GET', 'HEAD', 'OPTIONS'].includes(method)) headers['X-CSRF-TOKEN'] = await this.getCsrfToken();
        const res = await fetch(this.base + path, {
            ...options,
            credentials: 'same-origin',
            headers
        });

        if (res.status === 401 && !isPublicPagePath()) {
            renderLoginRequired();
            const error = new Error('Authentication required.');
            error.authRequired = true;
            throw error;
        }

        if (res.status === 204) return null;

        const isJson = /\bjson\b/i.test(res.headers.get('content-type') || '');

        const body = isJson ? await res.json().catch(() => null) : null;

        if (!res.ok) {
            const message = body?.detail || body?.error || body?.title || `Request failed (${res.status})`;
            const error = new Error(message);
            error.status = res.status;
            throw error;
        }

        return body;
    },

    get(path) {
        return this.request(path);
    },

    post(path, data) {
        return this.request(path, {
            method: 'POST',
            body: JSON.stringify(data)
        });
    },

    put(path, data) {
        return this.request(path, {
            method: 'PUT',
            body: JSON.stringify(data)
        });
    },

    del(path) {
        return this.request(path, {
            method: 'DELETE'
        });
    }
};


// Builds the sidebar navigation and marks the current page active.
function renderNav(active) {
    const items = [
        {
            href: '/index.html',
            label: 'Dashboard',
            key: 'dashboard',
            icon: '◌'
        },
        {
            href: '/rounds.html',
            label: 'Rounds',
            key: 'rounds',
            icon: '↗',
            requiresAuth: false
        },
        {
            href: '/live-round.html',
            label: 'Start Round',
            key: 'add-round',
            icon: '+',
            requiresAuth: true
        },
        {
            href: '/courses.html',
            label: 'Courses',
            key: 'courses',
            icon: '⌂'
        },
        {
            href: '/statistics.html',
            label: 'Statistics',
            key: 'statistics',
            icon: '◒',
            requiresAuth: false
        },
        {
            href: '/practice.html',
            label: 'Practice',
            key: 'practice',
            icon: '✧',
            requiresAuth: false
        },
        {
            href: '/account.html',
            label: 'Account',
            key: 'account',
            icon: '•',
            requiresAuth: true
        }
    ];

    const nav = document.getElementById('site-nav');

    if (!nav) return;
    nav.setAttribute('aria-label', 'Primary navigation');

    nav.innerHTML = `
    <button
      class="sidebar-close"
      id="sidebar-close"
      aria-label="Close navigation">
      ×
    </button>

    <div class="brand">
      <span class="brand-mark">&#9873;</span>
      Birdie Buddy
    </div>

    <ul class="nav-list">
      ${items.map(item => `
        <li${item.requiresAuth ? ' hidden data-requires-auth="true"' : ''}>
            <a
              href="${item.href}"
              class="${item.key === active ? 'active' : ''}"
              data-icon="${item.icon}"
              aria-current="${item.key === active ? 'page' : 'false'}">
              ${item.label}
            </a>
        </li>
      `).join('')}
    </ul>

    <div class="sidebar-account" id="sidebar-account">
      <div class="account-label">Exploring as guest</div>
      <div class="account-name" id="current-user-name">Guest</div>
      <button class="logout-button" id="logout-button" type="button">Log out</button>
    </div>

    <div class="sidebar-footer">
      Round tracking &amp; performance analysis
    </div>
  `;

    setupMobileNavigation();
    hydrateCurrentUser();
}


async function hydrateCurrentUser() {
    if (AUTH_ENTRY_PATHS.has(window.location.pathname)) return;

    try {
        const response = await fetch('/api/auth/me', { credentials: 'same-origin' });
        if (response.status === 401) {
            renderLoginRequired();
            renderGuestAccount();
            return;
        }
        if (!response.ok) throw new Error('Account connection unavailable.');

        const user = await response.json();
        document.querySelectorAll('[data-requires-auth="true"]').forEach(item => { item.hidden = false; });
        const name = document.getElementById('current-user-name');
        if (name) name.textContent = user.displayName || user.email;

        const logoutButton = document.getElementById('logout-button');
        logoutButton?.addEventListener('click', async () => {
            logoutButton.disabled = true;
            await Api.post('/auth/logout', {});
            sessionStorage.removeItem('birdiebuddy.liveUser');
            Api.csrfToken = null;
            window.location.replace('/login.html');
        });
    } catch {
        // A network outage is not a sign-out. Keep the offline scorecard open.
        const name = document.getElementById('current-user-name');
        if (name) name.textContent = 'Connection unavailable';
    }
}

function renderGuestAccount() {
    const account = document.getElementById('sidebar-account');
    if (!account) return;

    const returnPath = window.location.pathname === '/' ? '/index.html' : window.location.pathname;
    account.innerHTML = `
      <div class="account-label">Exploring as guest</div>
      <div class="account-name">Browse without an account</div>
      <div class="sidebar-auth-actions">
        <a class="btn btn-flag" href="/signup.html?returnUrl=${encodeURIComponent(returnPath)}">Create account</a>
        <a class="btn btn-secondary" href="/login.html?returnUrl=${encodeURIComponent(returnPath)}">Sign in</a>
      </div>`;
}


// Creates and controls the mobile navigation.
function setupMobileNavigation() {
    const nav = document.getElementById('site-nav');

    if (!nav) return;

    let mobileHeader = document.querySelector('.mobile-header');

    if (!mobileHeader) {
        mobileHeader = document.createElement('header');
        mobileHeader.className = 'mobile-header';

        mobileHeader.innerHTML = `
      <button
        class="menu-button"
        id="menu-button"
        aria-label="Open navigation"
        aria-controls="site-nav"
        aria-expanded="false">
        ☰
      </button>

      <div class="mobile-brand">
        <span class="brand-mark">&#9873;</span>
        Birdie Buddy
      </div>
    `;

        document.body.insertBefore(
            mobileHeader,
            document.querySelector('.app-shell')
        );
    }

    let overlay = document.querySelector('.nav-overlay');

    if (!overlay) {
        overlay = document.createElement('div');
        overlay.className = 'nav-overlay';
        overlay.id = 'nav-overlay';

        document.body.insertBefore(
            overlay,
            document.querySelector('.app-shell')
        );
    }

    const menuButton = document.getElementById('menu-button');
    const closeButton = document.getElementById('sidebar-close');

    if (nav.dataset.mobileSetup === 'true') {
        return;
    }

    nav.dataset.mobileSetup = 'true';
    const mobileQuery = window.matchMedia('(max-width: 900px)');
    const main = document.querySelector('main');
    let isOpen = false;

    function updateNavigationState() {
        nav.inert = mobileQuery.matches && !isOpen;
        if (main) main.inert = mobileQuery.matches && isOpen;
    }

    function openMenu() {
        if (!mobileQuery.matches || isOpen) return;
        isOpen = true;
        nav.classList.add('mobile-open');
        overlay.classList.add('active');

        if (menuButton) {
            menuButton.setAttribute('aria-expanded', 'true');
        }

        document.body.classList.add('menu-open');
        document.documentElement.classList.add('menu-open');
        updateNavigationState();
        closeButton?.focus({ preventScroll: true });
    }

    function closeMenu() {
        const wasOpen = isOpen;
        isOpen = false;
        nav.classList.remove('mobile-open');
        overlay.classList.remove('active');

        if (menuButton) {
            menuButton.setAttribute('aria-expanded', 'false');
        }

        document.body.classList.remove('menu-open');
        document.documentElement.classList.remove('menu-open');
        if (wasOpen) {
            menuButton?.focus({ preventScroll: true });
        }
        updateNavigationState();
    }

    if (menuButton) {
        menuButton.addEventListener('click', openMenu);
    }

    if (closeButton) {
        closeButton.addEventListener('click', closeMenu);
    }

    overlay.addEventListener('click', closeMenu);

    nav.addEventListener('keydown', event => {
        if (!isOpen) return;
        if (event.key === 'Escape') { event.preventDefault(); closeMenu(); }
        if (event.key !== 'Tab') return;
        const focusable = [...nav.querySelectorAll('a[href], button:not([disabled])')]
            .filter(element => element.getClientRects().length);
        const first = focusable[0], last = focusable.at(-1);
        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault(); last?.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault(); first?.focus();
        }
    });

    nav.addEventListener('click', event => {
        const link = event.target.closest('a');

        if (link) {
            closeMenu();
        }
    });

    mobileQuery.addEventListener('change', closeMenu);
    updateNavigationState();
}


function fmtDate(dateStr) {
    const d = new Date(/^\d{4}-\d{2}-\d{2}$/.test(dateStr) ? `${dateStr}T00:00:00` : dateStr);

    return d.toLocaleDateString(
        undefined,
        {
            day: 'numeric',
            month: 'short',
            year: 'numeric'
        }
    );
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

function escapeHtml(value) {
    return String(value ?? '')
        .replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;').replaceAll("'", '&#039;');
}

// Dialog helpers keep destructive and multi-field actions usable with a
// keyboard and on a phone. They also avoid browser-native prompt/confirm UI,
// which cannot carry the app's status and validation language.
function openConfirmDialog({ title, message, confirmLabel = 'Continue', danger = false }) {
    return new Promise(resolve => {
        const dialog = document.createElement('dialog');
        dialog.className = 'card app-dialog';
        dialog.setAttribute('aria-labelledby', 'app-dialog-title');
        dialog.innerHTML = `<form method="dialog">
          <h2 id="app-dialog-title">${escapeHtml(title)}</h2>
          <p>${escapeHtml(message)}</p>
          <div class="detail-actions"><button class="btn btn-secondary" value="cancel">Cancel</button><button class="btn ${danger ? 'btn-danger' : 'btn-primary'}" value="confirm">${escapeHtml(confirmLabel)}</button></div>
        </form>`;
        dialog.addEventListener('close', () => {
            resolve(dialog.returnValue === 'confirm');
            dialog.remove();
        }, { once: true });
        document.body.append(dialog);
        dialog.showModal();
    });
}

function openFormDialog({ title, description = '', fields, submitLabel = 'Save' }) {
    return new Promise(resolve => {
        const dialog = document.createElement('dialog');
        dialog.className = 'card app-dialog';
        dialog.setAttribute('aria-labelledby', 'app-dialog-title');
        const fieldMarkup = fields.map(field => {
            const id = `dialog-${field.name}`;
            const control = field.type === 'textarea'
                ? `<textarea id="${id}" name="${escapeHtml(field.name)}" rows="4" maxlength="${Number(field.maxLength || 2000)}" ${field.required ? 'required' : ''} placeholder="${escapeHtml(field.placeholder || '')}">${escapeHtml(field.value || '')}</textarea>`
                : `<input id="${id}" name="${escapeHtml(field.name)}" type="${escapeHtml(field.type || 'text')}" value="${escapeHtml(field.value || '')}" maxlength="${Number(field.maxLength || 500)}" ${field.autocomplete ? `autocomplete="${escapeHtml(field.autocomplete)}"` : ''} ${field.required ? 'required' : ''} placeholder="${escapeHtml(field.placeholder || '')}"/>`;
            return `<div class="field"><label for="${id}">${escapeHtml(field.label)}</label>${control}</div>`;
        }).join('');
        dialog.innerHTML = `<form method="dialog"><h2 id="app-dialog-title">${escapeHtml(title)}</h2>${description ? `<p>${escapeHtml(description)}</p>` : ''}${fieldMarkup}<div class="detail-actions"><button class="btn btn-secondary" value="cancel">Cancel</button><button class="btn btn-primary" value="submit">${escapeHtml(submitLabel)}</button></div></form>`;
        dialog.addEventListener('close', () => {
            const values = dialog.returnValue === 'submit'
                ? Object.fromEntries(new FormData(dialog.querySelector('form')).entries())
                : null;
            resolve(values);
            dialog.remove();
        }, { once: true });
        document.body.append(dialog);
        dialog.showModal();
        dialog.querySelector('input, textarea, select')?.focus({ preventScroll: true });
    });
}

if (typeof window !== 'undefined' && typeof navigator !== 'undefined' && 'serviceWorker' in navigator) {
    window.addEventListener('load', () => navigator.serviceWorker.register('/service-worker.js').catch(() => {}), { once: true });
}
