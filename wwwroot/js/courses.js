renderNav('courses');

const list = document.getElementById('local-courses');
const search = document.getElementById('course-search');
const loadMore = document.getElementById('load-more-courses');
let courses = [];
let nextCursor = null;
let requestId = 0;
let searchTimer = null;

function renderCoursesLoginRequired() {
  const returnUrl = `${window.location.pathname}${window.location.search}`;
  list.innerHTML = `
    <div class="card auth-required course-auth-required" role="status" aria-labelledby="courses-auth-title">
      <span class="eyebrow">Members only</span>
      <h2 id="courses-auth-title">Sign in to view available courses.</h2>
      <p>Sign in to choose a course when adding a round.</p>
      <a class="btn btn-flag" href="/login.html?returnUrl=${encodeURIComponent(returnUrl)}">Sign in to continue</a>
    </div>`;
  loadMore.hidden = true;
}

async function requireCourseLogin() {
  const response = await fetch('/api/auth/me', { credentials: 'same-origin' });
  if (response.status === 401) {
    renderCoursesLoginRequired();
    return false;
  }
  if (!response.ok) throw new Error('Account connection unavailable.');
  return true;
}

function renderCourses() {
  const query = search.value.trim();
  if (!courses.length) {
    list.innerHTML = `<div class="card empty-state"><h3>${query ? 'No matching courses.' : 'No courses available.'}</h3><p>${query ? 'Try a shorter course or location name.' : 'Ask the administrator to load the Golf NZ course catalogue.'}</p></div>`;
  } else {
    list.innerHTML = `<div class="card list-card"><table><caption>${query ? 'Matching courses' : 'Available courses'}</caption><thead><tr><th class="text-cell">Name</th><th class="text-cell">Location</th></tr></thead><tbody>${courses.map(course => `<tr><td class="text-cell">${escapeHtml(course.name)}</td><td class="text-cell">${escapeHtml(course.location || '—')}</td></tr>`).join('')}</tbody></table></div>`;
  }
  loadMore.hidden = !nextCursor;
}

async function loadCourses(reset = true) {
  const currentRequest = ++requestId;
  if (reset) {
    courses = [];
    nextCursor = null;
    list.innerHTML = '<p class="progress-note">Loading courses…</p>';
  }
  loadMore.disabled = true;
  try {
    if (!(await requireCourseLogin())) return;
    const params = new URLSearchParams({ limit: '50' });
    if (search.value.trim()) params.set('search', search.value.trim());
    if (!reset && nextCursor) params.set('cursor', nextCursor);
    const page = await Api.get(`/courses/page?${params}`);
    if (currentRequest !== requestId) return;
    courses = reset ? page.items : courses.concat(page.items);
    nextCursor = page.nextCursor;
    renderCourses();
  } catch (error) {
    if (currentRequest !== requestId) return;
    list.innerHTML = `<div class="alert error" role="alert">${escapeHtml(error.message)}</div>`;
    loadMore.hidden = true;
  } finally {
    if (currentRequest === requestId) loadMore.disabled = false;
  }
}

search.addEventListener('input', () => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => loadCourses(true), 250);
});
loadMore.addEventListener('click', () => loadCourses(false));
loadCourses();
