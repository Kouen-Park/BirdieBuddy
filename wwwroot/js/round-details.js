renderNav('rounds');

const roundId = new URLSearchParams(location.search).get('id');
const content = document.getElementById('round-content');

async function loadRoundDetails() {
  if (!roundId) {
    content.innerHTML = `<div class="alert error">No round specified.</div>`;
    return;
  }

  try {
    const [round, stats] = await Promise.all([
      Api.get(`/rounds/${roundId}`),
      Api.get(`/statistics/round/${roundId}`)
    ]);

    if (round.status === 'Draft') return location.replace(`/live-round.html?id=${round.id}`);
    document.getElementById('round-title').textContent = round.courseName;
    document.getElementById('round-subtitle').textContent = `${fmtDate(round.date)} · Tee: ${round.tee}`;

    content.innerHTML = `
      <div class="stat-grid">
        ${statCard('Total Score', stats.totalScore)}
        ${statCard('To Par', toPar(stats.scoreToPar), 'accent')}
        ${statCard('Putts', stats.totalPutts)}
        ${statCard('GIR %', pct(stats.girPercentage))}
        ${statCard('Fairway %', stats.fairwayPercentage != null ? pct(stats.fairwayPercentage) : '—')}
      </div>

      <div class="legend">
        <span><span class="score-mark eagle"></span>Eagle or better</span>
        <span><span class="score-mark birdie"></span>Birdie</span>
        <span><span class="score-mark"></span>Par</span>
        <span><span class="score-mark bogey"></span>Bogey</span>
        <span><span class="score-mark double"></span>Double bogey or worse</span>
      </div>

      <div class="card list-card">
        <table>
          <caption>Round scorecard · ${round.holes.length} recorded holes</caption>
          <thead>
            <tr><th>Hole</th><th>Par</th><th>Score</th><th>+/-</th><th>Putts</th><th>GIR</th><th>Fairway</th><th>Penalty</th><th>Edit</th></tr>
          </thead>
          <tbody>
            ${round.holes.map(h => `
              <tr>
                <td class="text-cell">${h.holeNumber}</td>
                <td>${h.par}</td>
                <td><span class="score-mark ${scoreMarkClass(h.score, h.par)}">${h.score}</span></td>
                <td>${toPar(h.score - h.par)}</td>
                <td>${h.putts}</td>
                <td class="text-cell">${h.gir ? '✓' : '—'}</td>
                <td class="text-cell">${h.fairwayHit === null ? 'N/A' : (h.fairwayHit ? '✓' : '✕')}</td>
                <td>${h.penalty}</td>
                <td>${round.status === 'Completed' ? `<button class="btn btn-secondary" data-edit-hole="${h.id}" aria-label="Edit hole ${h.holeNumber}">Edit</button>` : '—'}</td>
              </tr>`).join('')}
          </tbody>
        </table>
      </div>

      <div class="section-title">By hole type</div>
      <div class="chart-grid">
        ${parTypeCard('Par 3', stats.par3)}
        ${parTypeCard('Par 4', stats.par4)}
        ${parTypeCard('Par 5', stats.par5)}
      </div>
      <div class="detail-actions">
        <button class="btn btn-secondary" id="edit-round">Edit round details</button>
        <button class="text-button danger-text" id="delete-round">Delete round</button>
      </div>
    `;
    document.getElementById('edit-round').addEventListener('click', () => editRound(round));
    document.getElementById('delete-round').addEventListener('click', deleteRound);
    content.querySelectorAll('[data-edit-hole]').forEach(button => button.addEventListener('click', () => {
      editHole(round, round.holes.find(h => h.id === Number(button.dataset.editHole)));
    }));
  } catch (err) {
    content.innerHTML = `<div class="alert error">Couldn't load this round: ${escapeHtml(err.message)}</div>`;
  }
}

function editHole(round, hole) {
  const dialog = document.createElement('dialog');
  dialog.className = 'hole-editor card';
  dialog.setAttribute('aria-labelledby', 'hole-edit-title');
  const numeric = (name, label, value, min, max) => `<div class="field"><label for="edit-${name}">${label}</label><input id="edit-${name}" name="${name}" type="number" inputmode="numeric" min="${min}" max="${max}" step="1" required value="${value}"></div>`;
  dialog.innerHTML = `<form>
    <h2 id="hole-edit-title">Edit hole ${hole.holeNumber} · Par ${hole.par}</h2>
    <p>Only this hole is saved. The original course and par are preserved.</p>
    <div class="form-grid">
      ${numeric('score', 'Score', hole.score, 1, 20)}
      ${numeric('putts', 'Putts', hole.putts, 0, 10)}
      ${numeric('penalty', 'Penalty strokes', hole.penalty, 0, 20)}
      <div class="field"><label for="edit-gir">Green in regulation</label><select id="edit-gir" name="gir"><option value="true">Yes</option><option value="false">No</option></select></div>
      <div class="field"><label for="edit-fairway">Fairway</label><select id="edit-fairway" name="fairway" ${hole.par === 3 ? 'disabled' : ''}><option value="">${hole.par === 3 ? 'Not applicable (par 3)' : 'Not recorded'}</option><option value="true">Hit</option><option value="false">Missed</option></select></div>
    </div>
    <p class="alert error" role="alert" id="hole-edit-error" hidden></p>
    <div class="detail-actions"><button class="btn btn-primary" type="submit">Save hole</button><button class="btn btn-secondary" type="button" id="cancel-hole-edit">Cancel</button></div>
  </form>`;
  document.body.append(dialog);
  const form = dialog.querySelector('form');
  form.elements.gir.value = String(hole.gir);
  form.elements.fairway.value = hole.par === 3 || hole.fairwayHit == null ? '' : String(hole.fairwayHit);
  let saving = false;
  dialog.addEventListener('cancel', event => { if (saving) event.preventDefault(); });
  dialog.addEventListener('close', () => dialog.remove());
  dialog.querySelector('#cancel-hole-edit').addEventListener('click', () => dialog.close());
  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (saving) return;
    const values = new FormData(form);
    const payload = {
      score: Number(values.get('score')), putts: Number(values.get('putts')),
      penalty: Number(values.get('penalty')), gir: values.get('gir') === 'true',
      fairwayHit: hole.par === 3 || !values.get('fairway') ? null : values.get('fairway') === 'true',
      checkExpected: true, expectedHole: hole
    };
    const errorEl = dialog.querySelector('#hole-edit-error');
    errorEl.hidden = true;
    if (payload.putts + payload.penalty > payload.score) {
      errorEl.textContent = 'Putts plus penalties cannot exceed the score.';
      errorEl.hidden = false;
      return;
    }
    saving = true;
    form.querySelectorAll('button, input, select').forEach(el => { el.disabled = true; });
    try {
      await Api.put(`/rounds/${round.id}/holes/${hole.id}`, payload);
      dialog.close();
      await loadRoundDetails();
    } catch (error) {
      errorEl.textContent = error.status === 409
        ? 'This hole changed in another session. Your input is still here. Note your changes, cancel and reload the round before trying again.'
        : error.message;
      errorEl.hidden = false;
    } finally {
      saving = false;
      form.querySelectorAll('button, input, select').forEach(el => { el.disabled = false; });
      if (hole.par === 3) form.elements.fairway.disabled = true;
    }
  });
  dialog.showModal();
}

async function editRound(round) {
  const values = await openFormDialog({
    title: 'Edit round details',
    description: 'Update the date without changing the recorded scorecard.',
    fields: [{ name: 'date', label: 'Round date', type: 'date', value: round.date.slice(0, 10), required: true }],
    submitLabel: 'Save changes'
  });
  const date = values?.date;
  if (!date) return;
  try {
    await Api.put(`/rounds/${round.id}`, { date, courseTeeId: round.courseTeeId, tee: round.tee, expectedUpdatedAt: round.updatedAt });
    location.reload();
  } catch (error) {
    const message = error.status === 409
      ? 'This round was updated in another session. Reload the round before editing it again.'
      : error.message;
    const alert = document.createElement('div');
    alert.className = 'alert error';
    alert.setAttribute('role', 'alert');
    const copy = document.createElement('span');
    copy.textContent = message;
    const reload = document.createElement('button');
    reload.className = 'text-button';
    reload.type = 'button';
    reload.textContent = 'Reload';
    reload.addEventListener('click', loadRoundDetails);
    alert.append(copy, ' ', reload);
    content.prepend(alert);
  }
}

async function deleteRound() {
  if (!await openConfirmDialog({
    title: 'Delete this round?',
    message: 'This permanently removes the round and its recorded holes.',
    confirmLabel: 'Delete round',
    danger: true
  })) return;
  try { await Api.del(`/rounds/${roundId}`); location.href = '/rounds.html'; }
  catch (error) {
    const alert = document.createElement('div');
    alert.className = 'alert error';
    alert.setAttribute('role', 'alert');
    alert.textContent = error.message;
    content.prepend(alert);
  }
}

function statCard(label, value, extraClass = '') {
  return `<div class="card stat-card"><div class="stat-label">${label}</div><div class="stat-value ${extraClass}">${value}</div></div>`;
}

function parTypeCard(label, s) {
  const body = s.holesPlayed === 0
    ? `<p class="progress-note">No holes of this type.</p>`
    : `<div class="stat-value">${s.averageScore.toFixed(2)}</div>
       <p class="progress-note">${s.holesPlayed} holes · avg ${toPar(Math.round(s.averageScoreToPar * 100) / 100)}</p>`;

  return `<div class="card chart-card"><h3>${label}</h3>${body}</div>`;
}

loadRoundDetails();
