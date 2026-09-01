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

        const isJson = res.headers
            .get('content-type')
            ?.includes('application/json');

        const body = isJson ? await res.json() : null;

        if (!res.ok) {
            const message = body?.error || `Request failed (${res.status})`;
            throw new Error(message);
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
            icon: '↗'
        },
        {
            href: '/add-round.html',
            label: 'Add Round',
            key: 'add-round',
            icon: '+'
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
            icon: '◒'
        },
        {
            href: '/practice.html',
            label: 'Practice',
            key: 'practice',
            icon: '✧'
        }
    ];

    const nav = document.getElementById('site-nav');

    if (!nav) return;

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
        <li>
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

    <div class="sidebar-footer">
      Round tracking &amp; performance analysis
    </div>
  `;

    setupMobileNavigation();
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

    function openMenu() {
        nav.classList.add('mobile-open');
        overlay.classList.add('active');

        if (menuButton) {
            menuButton.setAttribute('aria-expanded', 'true');
        }

        document.body.classList.add('menu-open');
    }

    function closeMenu() {
        nav.classList.remove('mobile-open');
        overlay.classList.remove('active');

        if (menuButton) {
            menuButton.setAttribute('aria-expanded', 'false');
        }

        document.body.classList.remove('menu-open');
    }

    if (menuButton) {
        menuButton.addEventListener('click', openMenu);
    }

    if (closeButton) {
        closeButton.addEventListener('click', closeMenu);
    }

    overlay.addEventListener('click', closeMenu);

    nav.addEventListener('click', event => {
        const link = event.target.closest('a');

        if (link) {
            closeMenu();
        }
    });

    window.addEventListener('resize', () => {
        if (window.innerWidth > 900) {
            closeMenu();
        }
    });
}


function fmtDate(dateStr) {
    const d = new Date(dateStr);

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