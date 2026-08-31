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

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

function setImportRunning() {
  importButton.disabled = true;
  importButton.textContent = 'Importing Golf NZ courses…';
}

async function waitForImport() {
  for (let attempt = 0; attempt < 900; attempt += 1) {
    const status = await Api.get('/courses/import-golf-nz/status');

    if (status.state === 'running') {
      setImportRunning();
      await sleep(1000);
      continue;
    }

    return status;
  }

  throw new Error('The Golf NZ import is taking longer than expected. Refresh the page to check its status.');
}

async function importGolfNz() {
  setImportRunning();
  importAlert.innerHTML = '<div class="alert">Golf NZ data is being imported in the background. You can keep this page open.</div>';

  try {
    await Api.post('/courses/import-golf-nz', {});
    const status = await waitForImport();

    if (status.state === 'failed') {
      throw new Error(status.error || 'Golf NZ import failed. Check the server logs.');
    }

    const result = status.result;
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
