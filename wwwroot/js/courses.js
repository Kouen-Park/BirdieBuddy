renderNav('courses');

const list = document.getElementById('local-courses');
const search = document.getElementById('course-search');
let courses = [];

function renderCourses() {
  const query = search.value.trim().toLowerCase();
  const filtered = courses.filter(course => `${course.name} ${course.location}`.toLowerCase().includes(query));
  if (!filtered.length) {
    list.innerHTML = `<div class="card empty-state"><h3>${query ? 'No matching courses.' : 'No courses available.'}</h3><p>${query ? 'Try a shorter course or location name.' : 'Ask the administrator to load the Golf NZ course catalogue.'}</p></div>`;
    return;
  }
  list.innerHTML = `<div class="card list-card"><table><caption>${filtered.length} course${filtered.length === 1 ? '' : 's'}</caption><thead><tr><th class="text-cell">Name</th><th class="text-cell">Location</th></tr></thead><tbody>${filtered.map(course => `<tr><td class="text-cell">${escapeHtml(course.name)}</td><td class="text-cell">${escapeHtml(course.location || '—')}</td></tr>`).join('')}</tbody></table></div>`;
}

search.addEventListener('input', renderCourses);
Api.get('/courses').then(result => { courses = result; renderCourses(); }).catch(error => {
  list.innerHTML = `<div class="alert error">${escapeHtml(error.message)}</div>`;
});
