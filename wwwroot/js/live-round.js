renderNav('add-round');

const root = document.getElementById('live-round-root');
const statusEl = document.getElementById('live-status');
const params = new URLSearchParams(location.search);
let round = null;
let course = null;
let currentHole = 1;
let store = null;
let syncTimer = null;
let blockedNavigation = false;
let finalizing = false;
let holeNumbers = [];
let conflictHole = null;
let reviewingConflict = false;

const clean = value => String(value ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');
const setStatus = (message, state = '') => { statusEl.textContent = message; statusEl.dataset.state = state; };

async function init() {
  const id = params.get('id');
  if (!id) return renderStart();
  try {
    let userId;
    let verified = false;
    try {
      userId = (await Api.get('/auth/me')).id;
      sessionStorage.setItem('birdiebuddy.liveUser', String(userId));
      verified = true;
    } catch (error) {
      if (navigator.onLine) throw error;
      userId = Number(sessionStorage.getItem('birdiebuddy.liveUser'));
      if (!userId) throw new Error('Open this round online once before using its offline draft.');
    }
    store = new LiveDraftStore(localStorage, userId, Number(id));
    if (verified) {
      round = await Api.get(`/rounds/${id}`);
      if (round.status !== 'Draft') {
        if (store.pending()) throw new Error('This round is no longer a draft. Unsynced device changes are preserved; review the round before resolving them.');
        return location.replace(`/round-details.html?id=${id}`);
      }
      course = await Api.get(`/courses/${round.courseId}`);
      store.seed(round, course);
    }
    const cached = store.view();
    if (!cached) throw new Error('No offline copy is available. Reconnect to open this round.');
    round = cached.round;
    course = cached.course;
    holeNumbers = round.holeNumbers || Array.from({ length: round.expectedHoles }, (_, i) => i + 1);
    currentHole = holeNumbers.includes(cached.currentHole) ? cached.currentHole : holeNumbers[0];
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
    const today = new Date();
    document.getElementById('live-date').value = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
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
  return tee?.holes?.find(h => h.holeNumber === number) || { holeNumber: number, par: 4, distance: 0, manual: true };
}

function renderHole() {
  const cached = store.view();
  if (cached) round = cached.round;
  const definition = holeDefinition(currentHole);
  const saved = round.holes.find(h => h.holeNumber === currentHole);
  const par = saved?.par || definition.par || 4;
  root.innerHTML = `
    <header class="live-round-header">
      <div><span class="eyebrow">${clean(round.courseName)}</span><h1>${clean(round.tee)}</h1></div>
      <div class="round-progress"><strong>${round.holes.length}</strong><span>of ${holeNumbers.length} recorded · ${store.pending()} pending</span></div>
    </header>
    <section class="yardage-page" aria-labelledby="hole-heading">
      <div class="hole-identity"><span>Hole</span><strong id="hole-heading">${currentHole}</strong><span>Par ${par}${definition.distance ? ` · ${definition.distance}m` : ''}</span></div>
      ${counter('score', 'Score', saved?.score ?? par, 1, 20)}
      ${counter('putts', 'Putts', saved?.putts ?? 2, 0, 10)}
      <div class="live-options">
        ${definition.manual ? `<label>Par <select id="manual-par">${[3, 4, 5, 6].map(value => `<option ${value === par ? 'selected' : ''}>${value}</option>`).join('')}</select></label>` : ''}
        ${toggle('gir', 'Green in regulation', saved?.gir)}
        ${par === 3 ? '<div class="option-na">Fairway · Not applicable on par 3</div>' : segmentedFairway(saved?.fairwayHit)}
        ${counter('penalty', 'Penalty strokes', saved?.penalty ?? 0, 0, 20, true)}
      </div>
    </section>
    <div class="live-actions">
      <button class="btn btn-secondary" id="previous-hole" ${currentHole === holeNumbers[0] ? 'disabled' : ''}>Previous</button>
      <button class="btn btn-primary" id="save-hole">${currentHole === holeNumbers.at(-1) ? 'Save hole' : 'Save & next'}</button>
      ${currentHole === holeNumbers.at(-1) ? '<button class="btn btn-flag" id="finish-round">Finish round</button>' : ''}
    </div>
    <div class="live-secondary-actions"><button class="text-button" id="retry-sync">Retry sync</button><button class="btn btn-secondary" id="review-conflict" ${conflictHole == null ? 'hidden' : ''}>Review save conflict</button><button class="text-button danger-text" id="abandon-round">Abandon round</button></div>`;
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
    captureChange();
  }));
  root.querySelectorAll('input, select').forEach(input => input.addEventListener('change', () => {
    captureChange();
    if (input.id === 'manual-par' && !blockedNavigation) renderHole();
  }));
  document.getElementById('previous-hole').addEventListener('click', () => navigate(-1));
  document.getElementById('save-hole').addEventListener('click', () => saveHole(true));
  document.getElementById('finish-round')?.addEventListener('click', finishRound);
  document.getElementById('abandon-round').addEventListener('click', abandonRound);
  document.getElementById('retry-sync').addEventListener('click', flushQueue);
  document.getElementById('review-conflict').addEventListener('click', reviewConflict);
}

function payload() {
  const definition = holeDefinition(currentHole);
  const fairway = root.querySelector('input[name="fairway"]:checked');
  const par = Number(document.getElementById('manual-par')?.value || definition.par || 4);
  return { par, score: Number(document.getElementById('score-value').textContent), putts: Number(document.getElementById('putts-value').textContent), gir: document.getElementById('gir-input').checked, fairwayHit: par === 3 ? null : fairway ? fairway.value === 'true' : null, penalty: Number(document.getElementById('penalty-value').textContent) };
}

function captureChange() {
  try {
    store.queue(currentHole, payload());
    updateProgress();
    blockedNavigation = false;
    setStatus('Saved on this device — syncing…', 'saving');
    clearTimeout(syncTimer);
    syncTimer = setTimeout(flushQueue, 600);
    return true;
  } catch (error) {
    blockedNavigation = true;
    setStatus(error.message, 'error');
    return false;
  }
}

function navigate(delta) {
  if (blockedNavigation || finalizing) return;
  const next = holeNumbers[holeNumbers.indexOf(currentHole) + delta];
  if (!next) return;
  try { store.navigate(next); currentHole = next; renderHole(); }
  catch (error) { setStatus(error.message, 'error'); }
}

function saveHole(advance) {
  if (finalizing || !captureChange()) return;
  if (advance) navigate(1);
  flushQueue();
}

async function flushQueue() {
  clearTimeout(syncTimer);
  if (!store || reviewingConflict) return false;
  if (!navigator.onLine) { setStatus('Offline — changes are saved on this device', 'offline'); return false; }
  try {
    setStatus('Syncing…', 'saving');
    await store.flush(
      (number, data) => Api.put(`/rounds/${store.roundId}/holes/by-number/${number}`, data),
      async () => (await Api.get('/auth/me')).id);
    const pending = store.pending();
    conflictHole = null;
    const reviewButton = document.getElementById('review-conflict');
    if (reviewButton) reviewButton.hidden = true;
    setStatus(pending ? `${pending} holes waiting to sync` : 'All changes saved to server', pending ? 'saving' : 'saved');
    updateProgress();
    return pending === 0;
  } catch (error) {
    updateProgress();
    if (error.status === 409 && error.holeNumber != null) {
      conflictHole = error.holeNumber;
      const reviewButton = document.getElementById('review-conflict');
      if (reviewButton) reviewButton.hidden = false;
    }
    setStatus(`${error.message} Your device copy is preserved.`, 'error');
    return false;
  }
}

function updateProgress() {
  if (!store) return;
  const progress = root.querySelector('.round-progress span');
  if (progress) progress.textContent = `of ${holeNumbers.length} recorded · ${store.pending()} pending`;
  const count = root.querySelector('.round-progress strong');
  if (count) count.textContent = store.view().round.holes.length;
}

async function reviewConflict() {
  if (reviewingConflict || conflictHole == null || finalizing || blockedNavigation) return;
  reviewingConflict = true;
  clearTimeout(syncTimer);
  let dialog;
  try {
    await store.inFlight?.catch(() => {});
    if (Number((await Api.get('/auth/me')).id) !== store.userId) throw new Error('Sign in to the original account before resolving this draft.');
    const serverRound = await Api.get(`/rounds/${store.roundId}`);
    if (serverRound.status !== 'Draft') throw new Error('This round is no longer editable. Your device copy is preserved.');
    const number = conflictHole;
    const edit = store.read().edits[number];
    if (!edit) { conflictHole = null; return; }
    const remote = serverRound.holes.find(h => h.holeNumber === number);
    const fields = [['par', 'Par'], ['score', 'Score'], ['putts', 'Putts'], ['gir', 'GIR'], ['fairwayHit', 'Fairway'], ['penalty', 'Penalties']];
    const value = v => v == null ? 'Not recorded / N/A' : typeof v === 'boolean' ? (v ? 'Yes' : 'No') : clean(v);
    dialog = document.createElement('dialog');
    dialog.className = 'hole-editor card';
    dialog.setAttribute('aria-labelledby', 'conflict-title');
    dialog.innerHTML = `<h2 id="conflict-title">Resolve hole ${number}</h2>
      <p>Compare before choosing. Other pending holes are kept. Keeping your input retries against this server snapshot; a newer server edit will conflict again.</p>
      <table><caption>Hole ${number} save conflict</caption><thead><tr><th>Field</th><th>This device</th><th>Server</th></tr></thead><tbody>${fields.map(([key, label]) => `<tr><th scope="row">${label}</th><td>${value(edit.payload[key])}</td><td>${remote ? value(remote[key]) : 'No saved hole'}</td></tr>`).join('')}</tbody></table>
      <p role="alert" class="alert error" hidden></p><div class="detail-actions"><button class="btn btn-secondary" data-choice="server">Use server record</button><button class="btn btn-primary" data-choice="local">Keep my input & retry</button><button class="btn btn-secondary" data-choice="cancel">Cancel</button></div>`;
    document.body.append(dialog);
    await new Promise(resolve => {
      let busy = false;
      dialog.addEventListener('cancel', event => { if (busy) event.preventDefault(); });
      dialog.addEventListener('close', resolve, { once: true });
      dialog.querySelectorAll('[data-choice]').forEach(button => button.addEventListener('click', async () => {
        if (busy) return;
        if (button.dataset.choice === 'cancel') return dialog.close();
        busy = true;
        dialog.querySelectorAll('button').forEach(b => b.disabled = true);
        try {
          if (Number((await Api.get('/auth/me')).id) !== store.userId) throw new Error('Your signed-in account changed. Nothing was discarded.');
          await store.resolve(number, edit.revision, serverRound, button.dataset.choice);
          conflictHole = null;
          renderHole();
          dialog.close();
        } catch (error) {
          const alert = dialog.querySelector('[role="alert"]');
          alert.textContent = error.message; alert.hidden = false;
        } finally { busy = false; dialog.querySelectorAll('button').forEach(b => b.disabled = false); }
      }));
      dialog.showModal();
    });
  } catch (error) { setStatus(error.message, 'error'); }
  finally { dialog?.remove(); reviewingConflict = false; }
  if (conflictHole == null) await flushQueue();
}

async function finishRound() {
  if (finalizing || !captureChange()) return;
  finalizing = true;
  root.querySelectorAll('button, input, select').forEach(control => control.disabled = true);
  try {
    if (!(await flushQueue())) return;
    const completed = await Api.post(`/rounds/${round.id}/complete`, {});
    location.href = `/round-details.html?id=${completed.id}`;
  } catch (error) { setStatus(error.message, 'error'); }
  finally { finalizing = false; renderHole(); }
}

async function abandonRound() {
  if (finalizing || blockedNavigation) return;
  if (!confirm('Abandon this round? Its saved holes will remain in your history.')) return;
  finalizing = true;
  root.querySelectorAll('button, input, select').forEach(control => control.disabled = true);
  try {
    if (!(await flushQueue())) return;
    await Api.post(`/rounds/${round.id}/abandon`, {});
    location.href = '/rounds.html';
  } catch (error) { setStatus(error.message, 'error'); }
  finally { finalizing = false; renderHole(); }
}

window.addEventListener('online', flushQueue);
window.addEventListener('beforeunload', event => { if (blockedNavigation || store?.pending()) { event.preventDefault(); event.returnValue = ''; } });
init();
