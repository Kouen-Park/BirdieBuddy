renderNav('practice');

const practice = document.getElementById('practice-content');
let insights = [];
function renderSessions(sessions) {
  sessions = Array.isArray(sessions) ? sessions : [];
  if (!sessions.length) return '<div class="card empty-state practice-history"><h3>No practice sessions logged yet.</h3><p>Start a suggested drill and record the result here.</p></div>';
  return `<section class="practice-history"><div class="section-title">Practice log</div><div class="card list-card"><table><caption>Recent practice sessions</caption><thead><tr><th>Date</th><th class="text-cell">Drill</th><th>Minutes</th><th>Status</th><th>Result</th></tr></thead><tbody>${sessions.map(s => `<tr><td>${fmtDate(s.startedAt)}</td><td class="text-cell">${escapeHtml(s.drillTitle)}</td><td>${s.minutes}</td><td>${s.completedAt ? '<span class="status-chip">Completed</span>' : `<button class="btn btn-secondary" data-complete-session="${s.id}">Log result</button>`}</td><td>${escapeHtml(s.result || '—')}</td></tr>`).join('')}</tbody></table></div></section>`;
}

async function loadPractice() {
  const [data, sessions] = await Promise.all([Api.get('/statistics/overview'), Api.get('/practice/sessions')]);
  insights = data.insights || [];
  practice.innerHTML = (insights.length ? `<div class="practice-list">${insights.map((item, index) => `
    <article class="card practice-item">
      <div class="practice-number">${String(index + 1).padStart(2, '0')}</div>
      <div><span class="eyebrow">${escapeHtml(item.severity === 'info' ? 'Next checkpoint' : 'Practice priority')}</span><h2>${escapeHtml(item.title)}</h2><p>${escapeHtml(item.evidence)}</p><strong>${escapeHtml(item.recommendation)}</strong>
      ${item.drill ? `<details class="practice-drill"><summary>${escapeHtml(item.drill.title)} · ${Number(item.drill.minutes)} min</summary><ol>${item.drill.steps.map(step => `<li>${escapeHtml(step)}</li>`).join('')}</ol><p><strong>Measure:</strong> ${escapeHtml(item.drill.measure)}</p><button class="btn btn-flag start-practice" data-focus="${escapeHtml(item.code)}" data-title="${escapeHtml(item.drill.title)}" data-minutes="${Number(item.drill.minutes)}">Log this practice</button></details>` : ''}
      ${item.evidenceRoundIds?.length ? `<details class="practice-evidence"><summary>Review ${item.evidenceRoundIds.length} supporting rounds</summary><ul>${item.evidenceRoundIds.filter(Number.isInteger).map(id => `<li><a href="/round-details.html?id=${id}">Round #${id}</a></li>`).join('')}</ul></details>` : ''}
      </div>
    </article>`).join('')}</div>` : '<div class="card empty-state"><h3>No practice focus yet.</h3><p>Complete three rounds to establish a useful baseline.</p></div>') + renderSessions(sessions);
  document.querySelectorAll?.('.start-practice').forEach(button => button.addEventListener('click', () => startSession(button)));
  document.querySelectorAll?.('[data-complete-session]').forEach(button => button.addEventListener('click', () => completeSession(button.dataset.completeSession)));
}

async function startSession(button) {
  button.disabled = true;
  try {
    await Api.post('/practice/sessions', { focusCode: button.dataset.focus, drillTitle: button.dataset.title, minutes: Number(button.dataset.minutes) });
    await loadPractice();
  } catch (error) { alert(error.message); button.disabled = false; }
}

async function completeSession(id) {
  const result = prompt('What result did you record? (optional)');
  if (result === null) return;
  const notes = prompt('Any notes for next time? (optional)');
  try { await Api.post(`/practice/sessions/${id}/complete`, { result, notes }); await loadPractice(); }
  catch (error) { alert(error.message); }
}

loadPractice().catch(error => { practice.innerHTML = `<div class="alert error">${escapeHtml(error.message)}</div>`; });
/* Legacy inline rendering removed: practice sessions are now persisted with each drill. */
/*
Api.get('/statistics/overview').then(data => {
  const insights = data.insights || [];
  practice.innerHTML = insights.length ? `<div class="practice-list">${insights.map((item, index) => `
    <article class="card practice-item">
      <div class="practice-number">${String(index + 1).padStart(2, '0')}</div>
      <div><span class="eyebrow">${escapeHtml(item.severity === 'info' ? 'Next checkpoint' : 'Practice priority')}</span><h2>${escapeHtml(item.title)}</h2><p>${escapeHtml(item.evidence)}</p><strong>${escapeHtml(item.recommendation)}</strong>
      ${item.drill ? `<details class="practice-drill"><summary>${escapeHtml(item.drill.title)} · ${Number(item.drill.minutes)} min</summary><ol>${item.drill.steps.map(step => `<li>${escapeHtml(step)}</li>`).join('')}</ol><p><strong>Measure:</strong> ${escapeHtml(item.drill.measure)}</p><p>Keep your results in your own notes; practice-session tracking is not available yet.</p></details>` : ''}
      ${item.evidenceRoundIds?.length ? `<details class="practice-evidence"><summary>Review ${item.evidenceRoundIds.length} supporting rounds</summary><ul>${item.evidenceRoundIds.filter(Number.isInteger).map(id => `<li><a href="/round-details.html?id=${id}">Round #${id}</a></li>`).join('')}</ul></details>` : ''}
      </div>
    </article>`).join('')}</div>` : '<div class="card empty-state"><h3>No practice focus yet.</h3><p>Complete three rounds to establish a useful baseline.</p></div>';
}).catch(error => { practice.innerHTML = `<div class="alert error">${escapeHtml(error.message)}</div>`; }); */
