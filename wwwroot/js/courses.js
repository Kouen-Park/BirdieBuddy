renderNav('courses');

const importButton = document.getElementById('import-golf-nz-btn');
const importAlert = document.getElementById('import-alert');
const localCoursesEl = document.getElementById('local-courses');

async function loadLocalCourses() {
  try {
    const courses = await Api.get('/courses');

    if (courses.length === 0) {
      localCoursesEl.innerHTML = `<div class="card empty-state"><h3>No courses yet.</h3><p>Load the Golf NZ course database above.</p></div>`;
      return;
    }

    localCoursesEl.innerHTML = `
      <div class="card list-card">
        <table>
          <thead><tr><th class="text-cell">Name</th><th class="text-cell">Location</th></tr></thead>
          <tbody>
            ${courses.map(c => `<tr><td class="text-cell">${escapeHtml(c.name)}</td><td class="text-cell">${escapeHtml(c.location || '—')}</td></tr>`).join('')}
          </tbody>
        </table>
      </div>
    `;
  } catch (err) {
    localCoursesEl.innerHTML = `<div class="alert error">Couldn't load courses: ${escapeHtml(err.message)}</div>`;
  }
}

async function importGolfNz() {
  importButton.disabled = true;
  importButton.textContent = 'Loading Golf NZ courses…';
  importAlert.innerHTML = '';

  try {
    const result = await Api.post('/courses/import-golf-nz', {});
    importAlert.innerHTML = `
      <div class="alert success">
        Golf NZ import complete: ${result.coursesCreated} courses created, ${result.coursesUpdated} updated,
        ${result.teesCreated} tees created, and ${result.holesCreated} holes created.
      </div>
    `;
    await loadLocalCourses();
  } catch (err) {
    importAlert.innerHTML = `<div class="alert error">Import failed: ${escapeHtml(err.message)}</div>`;
  } finally {
    importButton.disabled = false;
    importButton.textContent = 'Load Golf NZ courses';
  }
}

function escapeHtml(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

importButton.addEventListener('click', importGolfNz);
loadLocalCourses();
