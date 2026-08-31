renderNav('courses');

const searchInput = document.getElementById('search-input');
const searchBtn = document.getElementById('search-btn');
const searchAlert = document.getElementById('search-alert');
const searchResults = document.getElementById('search-results');
const localCoursesEl = document.getElementById('local-courses');

async function loadLocalCourses() {
  try {
    const courses = await Api.get('/courses');

    if (courses.length === 0) {
      localCoursesEl.innerHTML = `<div class="card empty-state"><h3>No courses yet.</h3><p>Search GolfCourseAPI above to import one.</p></div>`;
      return;
    }

    localCoursesEl.innerHTML = `
      <div class="card list-card">
        <table>
          <thead><tr><th class="text-cell">Name</th><th class="text-cell">Location</th></tr></thead>
          <tbody>
            ${courses.map(c => `<tr><td class="text-cell">${c.name}</td><td class="text-cell">${c.location || '—'}</td></tr>`).join('')}
          </tbody>
        </table>
      </div>
    `;
  } catch (err) {
    localCoursesEl.innerHTML = `<div class="alert error">Couldn't load courses: ${err.message}</div>`;
  }
}

async function runSearch() {
  const q = searchInput.value.trim();
  if (!q) return;

  searchAlert.innerHTML = '';
  searchResults.innerHTML = `<p class="progress-note">Searching&hellip;</p>`;
  searchBtn.disabled = true;

  try {
    const results = await Api.get(`/courses/external/search?q=${encodeURIComponent(q)}`);

    if (results.length === 0) {
      searchResults.innerHTML = `<p class="progress-note">No matches on GolfCourseAPI.</p>`;
      return;
    }

    searchResults.innerHTML = results.map(r => {
      const maleTeeCount = r.maleTeeCount || 0;
      const femaleTeeCount = r.femaleTeeCount || 0;
      const hasTeeData = maleTeeCount + femaleTeeCount > 0;
      const teeSummary = hasTeeData
        ? `Tee boxes: ${maleTeeCount} men's / ${femaleTeeCount} women's`
        : 'No tee data — holes can be entered manually';

      return `
        <div style="display:flex; align-items:center; justify-content:space-between; padding:10px 0; border-bottom:1px solid var(--line);">
          <div>
            <div style="font-weight:600;">${r.clubName}${r.courseName && r.courseName !== r.clubName ? ' · ' + r.courseName : ''}</div>
            <div class="progress-note">${r.location || ''}</div>
            <div class="progress-note">${teeSummary}</div>
          </div>
          <button class="btn btn-ghost import-btn" data-id="${r.externalId}">Import</button>
        </div>
      `;
    }).join('');

    searchResults.querySelectorAll('.import-btn').forEach(btn => {
      btn.addEventListener('click', () => importCourse(btn));
    });
  } catch (err) {
    searchResults.innerHTML = '';
    searchAlert.innerHTML = `<div class="alert error" style="margin-top:12px;">Search failed: ${err.message}</div>`;
  } finally {
    searchBtn.disabled = false;
  }
}

async function importCourse(btn) {
  const externalId = btn.dataset.id;
  btn.disabled = true;
  btn.textContent = 'Importing…';

  try {
    await Api.post(`/courses/external/${externalId}/import`, {});
    btn.textContent = 'Imported ✓';
    await loadLocalCourses();
  } catch (err) {
    searchAlert.innerHTML = `<div class="alert error" style="margin-top:12px;">${err.message}</div>`;
    btn.disabled = false;
    btn.textContent = 'Import';
  }
}

searchBtn.addEventListener('click', runSearch);
searchInput.addEventListener('keydown', e => { if (e.key === 'Enter') runSearch(); });

loadLocalCourses();
