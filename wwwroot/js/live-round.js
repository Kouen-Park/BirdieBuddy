renderNav('add-round');

const root = document.getElementById('live-round-root');
const statusEl = document.getElementById('live-status');
const params = new URLSearchParams(location.search);
let round = null;
let course = null;
let currentHole = 1;
let saving = false;
const queueKey = 'birdiebuddy.liveQueue';

const clean = value => String(value ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');
const setStatus = (message, state = '') => { statusEl.textContent = message; statusEl.dataset.state = state; };

async function init() {
  const id = params.get('id');
  if (!id) return renderStart();
  try {
    round = await Api.get(`/rounds/${id}`);
    if (round.status !== 'Draft') return location.replace(`/round-details.html?id=${id}`);
    course = await Api.get(`/courses/${round.courseId}`);
    currentHole = Math.max(1, Math.min(round.currentHole || 1, round.expectedHoles));
    renderHole();
    await flushQueue();
  } catch (error) {
    root.innerHTML = `<div class="alert error">${clean(error.message)}</div>`;
  }
}

async function renderStart() {
  try {
    const courses = await Api.get('/courses');
    root.innerHTML = `
      <header class="live-intro"><span class="eyebrow">On-course scorecard</span><h1>Start a round</h1><p>Choose your course and tee. Each hole saves as you play.</p></header>
      <form class="card live-start" id="start-round-form">
        <div class="field"><label for="live-course">Course</label><select id="live-course" required><option value="">Choose a course…</option>${courses.map(c => `<option value="${c.id}">${clean(c.name)}</option>`).join('')}</select></div>
        <div class="field"><label for="live-tee">Tee</label><select id="live-tee" required disabled><option value="">Choose a course first…</option></select></div>
        <div class="field"><label for="live-date">Round date</label><input id="live-date" type="date" required /></div>
        <button class="btn btn-flag btn-wide" type="submit">Start round</button>
        <a class="quiet-link" href="/add-round.html">Enter a completed scorecard instead</a>
      </form>`;
    const courseSelect = document.getElementById('live-course');
    const teeSelect = document.getElementById('live-tee');
    document.getElementById('live-date').valueAsDate = new Date();
    courseSelect.addEventListener('change', async () => {
      teeSelect.disabled = true;
      teeSelect.innerHTML = '<option>Loading…</option>';
      if (!courseSelect.value) return;
      course = await Api.get(`/courses/${courseSelect.value}`);
      teeSelect.innerHTML = (course.tees || []).map(t => `<option value="${t.id}">${clean(t.name)} · ${t.nineHoles ? '9' : '18'} holes</option>`).join('');
      teeSelect.disabled = !teeSelect.options.length;
    });
    document.getElementById('start-round-form').addEventListener('submit', startRound);
  } catch (error) {
    root.innerHTML = `<div class="alert error">${clean(error.message)}</div>`;
  }
}

async function startRound(event) {
  event.preventDefault();
  const button = event.submitter;
  button.disabled = true;
  setStatus('Starting round…', 'saving');
  try {
    const created = await Api.post('/rounds/drafts', {
      courseId: Number(document.getElementById('live-course').value),
      courseTeeId: Number(document.getElementById('live-tee').value),
      tee: null,
      date: document.getElementById('live-date').value
    });
    location.replace(`/live-round.html?id=${created.id}`);
  } catch (error) {
    setStatus(error.message, 'error');
    button.disabled = false;
  }
}

function holeDefinition(number) {
  const tee = course?.tees?.find(t => t.id === round.courseTeeId);
  return tee?.holes?.find(h => h.holeNumber === number) || { holeNumber: number, par: 4, distance: 0 };
}

function renderHole() {
  const definition = holeDefinition(currentHole);
  const saved = round.holes.find(h => h.holeNumber === currentHole);
  const par = saved?.par || definition.par || 4;
  root.innerHTML = `
    <header class="live-round-header">
      <div><span class="eyebrow">${clean(round.courseName)}</span><h1>${clean(round.tee)}</h1></div>
      <div class="round-progress"><strong>${round.holes.length}</strong><span>of ${round.expectedHoles} saved</span></div>
    </header>
    <section class="yardage-page" aria-labelledby="hole-heading">
      <div class="hole-identity"><span>Hole</span><strong id="hole-heading">${currentHole}</strong><span>Par ${par}${definition.distance ? ` · ${definition.distance}m` : ''}</span></div>
      ${counter('score', 'Score', saved?.score ?? par, 1, 20)}
      ${counter('putts', 'Putts', saved?.putts ?? 2, 0, 10)}
      <div class="live-options">
        ${toggle('gir', 'Green in regulation', saved?.gir)}
        ${par === 3 ? '<div class="option-na">Fairway · Not applicable on par 3</div>' : segmentedFairway(saved?.fairwayHit)}
        ${counter('penalty', 'Penalty strokes', saved?.penalty ?? 0, 0, 20, true)}
      </div>
    </section>
    <div class="live-actions">
      <button class="btn btn-secondary" id="previous-hole" ${currentHole === 1 ? 'disabled' : ''}>Previous</button>
      <button class="btn btn-primary" id="save-hole">${currentHole === round.expectedHoles ? 'Save hole' : 'Save & next'}</button>
      ${currentHole === round.expectedHoles ? '<button class="btn btn-flag" id="finish-round">Finish round</button>' : ''}
    </div>
    <div class="live-secondary-actions"><button class="text-button danger-text" id="abandon-round">Abandon round</button></div>`;
  bindControls();
}

function counter(id, label, value, min, max, compact = false) {
  return `<div class="live-counter ${compact ? 'compact' : ''}"><span>${label}</span><div><button type="button" data-counter="${id}" data-delta="-1" aria-label="Decrease ${label}">−</button><output id="${id}-value" data-min="${min}" data-max="${max}">${value}</output><button type="button" data-counter="${id}" data-delta="1" aria-label="Increase ${label}">+</button></div></div>`;
}
function toggle(id, label, checked) { return `<label class="switch-option"><span>${label}</span><input id="${id}-input" type="checkbox" ${checked ? 'checked' : ''}/><i aria-hidden="true"></i></label>`; }
function segmentedFairway(value) { return `<fieldset class="fairway-choice"><legend>Fairway</legend><label><input type="radio" name="fairway" value="true" ${value === true ? 'checked' : ''}/><span>Hit</span></label><label><input type="radio" name="fairway" value="false" ${value === false ? 'checked' : ''}/><span>Miss</span></label></fieldset>`; }

function bindControls() {
  root.querySelectorAll('[data-counter]').forEach(button => button.addEventListener('click', () => {
    const output = document.getElementById(`${button.dataset.counter}-value`);
    output.textContent = Math.max(Number(output.dataset.min), Math.min(Number(output.dataset.max), Number(output.textContent) + Number(button.dataset.delta)));
  }));
  document.getElementById('previous-hole').addEventListener('click', () => { currentHole -= 1; renderHole(); });
  document.getElementById('save-hole').addEventListener('click', () => saveHole(true));
  document.getElementById('finish-round')?.addEventListener('click', finishRound);
  document.getElementById('abandon-round').addEventListener('click', abandonRound);
}

function payload() {
  const definition = holeDefinition(currentHole);
  const fairway = root.querySelector('input[name="fairway"]:checked');
  return { par: definition.par || 4, score: Number(document.getElementById('score-value').textContent), putts: Number(document.getElementById('putts-value').textContent), gir: document.getElementById('gir-input').checked, fairwayHit: fairway ? fairway.value === 'true' : null, penalty: Number(document.getElementById('penalty-value').textContent) };
}

async function saveHole(advance) {
  if (saving) return false;
  saving = true;
  setStatus('Saving…', 'saving');
  const item = { roundId: round.id, holeNumber: currentHole, payload: payload() };
  try {
    const hole = await Api.put(`/rounds/${round.id}/holes/by-number/${currentHole}`, item.payload);
    round.holes = round.holes.filter(h => h.holeNumber !== currentHole).concat(hole);
    setStatus('Saved', 'saved');
    if (advance && currentHole < round.expectedHoles) currentHole += 1;
    renderHole();
    return true;
  } catch (error) {
    if (!navigator.onLine || /fetch/i.test(error.message)) {
      const queue = JSON.parse(localStorage.getItem(queueKey) || '[]').filter(q => !(q.roundId === item.roundId && q.holeNumber === item.holeNumber));
      queue.push(item);
      localStorage.setItem(queueKey, JSON.stringify(queue));
      round.holes = round.holes.filter(h => h.holeNumber !== currentHole).concat({ holeNumber: currentHole, ...item.payload });
      setStatus('Saved on this device — waiting for connection', 'offline');
      if (advance && currentHole < round.expectedHoles) currentHole += 1;
      renderHole();
      return true;
    } else setStatus(error.message, 'error');
    return false;
  } finally { saving = false; }
}

async function flushQueue() {
  if (!navigator.onLine) return;
  const queue = JSON.parse(localStorage.getItem(queueKey) || '[]');
  const remaining = [];
  for (const item of queue) {
    try { await Api.put(`/rounds/${item.roundId}/holes/by-number/${item.holeNumber}`, item.payload); }
    catch { remaining.push(item); }
  }
  localStorage.setItem(queueKey, JSON.stringify(remaining));
  if (!remaining.length && queue.length) { round = await Api.get(`/rounds/${round.id}`); setStatus('Offline changes synced', 'saved'); renderHole(); }
}

async function finishRound() {
  if (!(await saveHole(false))) return;
  try { await flushQueue(); const completed = await Api.post(`/rounds/${round.id}/complete`, {}); location.href = `/round-details.html?id=${completed.id}`; }
  catch (error) { setStatus(error.message, 'error'); }
}

async function abandonRound() {
  if (!confirm('Abandon this round? Its saved holes will remain in your history.')) return;
  await Api.post(`/rounds/${round.id}/abandon`, {});
  location.href = '/rounds.html';
}

window.addEventListener('online', flushQueue);
window.addEventListener('beforeunload', event => { if (saving) { event.preventDefault(); event.returnValue = ''; } });
init();
