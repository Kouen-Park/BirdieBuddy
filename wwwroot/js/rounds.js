renderNav('rounds');

const content = document.getElementById('rounds-content');
const filters = document.getElementById('round-filters');
let cursor = null;
let rows = [];

// Filter controls that should round-trip through the URL query string.
const FILTER_FIELDS = ['filter-search', 'filter-course', 'filter-status', 'filter-holes', 'filter-from', 'filter-to'];

// Restore filter values from the URL so a bookmarked/shared/refreshed view keeps its filters.
function restoreFiltersFromUrl() {
  const params = new URLSearchParams(location.search);
  FILTER_FIELDS.forEach(id => {
    const value = params.get(id);
    if (value !== null) {
      const el = document.getElementById(id);
      if (el) el.value = value;
    }
  });
}

// Write the current filter values into the URL without adding a history entry per keystroke.
function syncFiltersToUrl() {
  const params = new URLSearchParams();
  FILTER_FIELDS.forEach(id => {
    const value = (document.getElementById(id).value || '').trim();
    if (value) params.set(id, value);
  });
  const query = params.toString();
  history.replaceState(null, '', query ? `?${query}` : location.pathname);
}

function queryString() {
  const values = {
    limit: 20,
    cursor,
    search: document.getElementById('filter-search').value.trim(),
    courseId: document.getElementById('filter-course').value,
    status: document.getElementById('filter-status').value,
    holeCount: document.getElementById('filter-holes').value,
    from: document.getElementById('filter-from').value,
    to: document.getElementById('filter-to').value
  };
  const params = new URLSearchParams();
  Object.entries(values).forEach(([key, value]) => { if (value !== null && value !== '') params.set(key, value); });
  return params.toString();
}

async function loadRounds(reset = false) {
  if (reset) { cursor = null; rows = []; }
  content.setAttribute('aria-busy', 'true');
  try {
    const page = await Api.get(`/rounds/page?${queryString()}`);
    rows.push(...page.items);
    cursor = page.nextCursor;
    renderRows();
  } catch (error) {
    if (error.authRequired) return;
    content.innerHTML = `<div class="alert error">${escapeHtml(error.message)}</div>`;
  } finally { content.removeAttribute('aria-busy'); }
}

function renderRows() {
  if (!rows.length) {
    content.innerHTML = `<div class="card empty-state"><h3>No rounds match these filters.</h3><p>Change a filter or start a new round.</p><a href="/live-round.html" class="btn btn-flag space-top">Start a round</a></div>`;
    return;
  }
  content.innerHTML = `<div class="card list-card"><table><caption>${rows.length}${cursor ? '+' : ''} rounds shown</caption><thead><tr><th>Date</th><th class="text-cell">Course</th><th class="text-cell">Tee</th><th>Holes</th><th>Score</th><th>To par</th></tr></thead><tbody>${rows.map(round => `
    <tr class="clickable" data-round-id="${round.id}" data-status="${round.status}" tabindex="0">
      <td>${fmtDate(round.date)}</td><td class="text-cell">${escapeHtml(round.courseName)}</td><td class="text-cell">${escapeHtml(round.tee)} ${statusChip(round.status)}</td><td>${round.holesPlayed}/${round.expectedHoles}</td><td>${round.totalScore || '—'}</td><td>${round.holesPlayed ? `<span class="pill ${toParPillClass(round.scoreToPar)}">${toPar(round.scoreToPar)}</span>` : '—'}</td>
    </tr>`).join('')}</tbody></table></div>${cursor ? '<button class="btn btn-secondary load-more" id="load-more">Load more</button>' : ''}`;
  content.querySelectorAll('[data-round-id]').forEach(row => {
    const open = () => location.href = row.dataset.status === 'Draft' ? `/live-round.html?id=${row.dataset.roundId}` : `/round-details.html?id=${row.dataset.roundId}`;
    row.addEventListener('click', open);
    row.addEventListener('keydown', event => { if (event.key === 'Enter' || event.key === ' ') open(); });
  });
  document.getElementById('load-more')?.addEventListener('click', () => loadRounds(false));
}

function statusChip(status) {
  if (status === 'Draft') return '<span class="status-chip">In progress</span>';
  if (status === 'Abandoned') return '<span class="status-chip muted">Abandoned</span>';
  return '';
}

filters.addEventListener('submit', event => { event.preventDefault(); syncFiltersToUrl(); loadRounds(true); });

// Load course options first so a course filter carried in the URL can be restored
// before the initial request is built (queryString reads the current DOM values).
Api.get('/courses')
  .then(courses => {
    const select = document.getElementById('filter-course');
    courses.forEach(course => { const option = document.createElement('option'); option.value = course.id; option.textContent = course.name; select.append(option); });
  })
  .catch(() => { /* filters still work without the course dropdown populated */ })
  .finally(() => {
    restoreFiltersFromUrl();
    loadRounds(true);
  });
