renderNav('add-round');

const courseSelect = document.getElementById('course-select');
const dateInput = document.getElementById('date-input');
const teeSelect = document.getElementById('tee-select');
const customTeeInput = document.getElementById('custom-tee-input');
const wrapper = document.getElementById('scorecard-wrapper');
const alertBox = document.getElementById('form-alert');

let selectedCourse = null;
let selectedTee = null;

dateInput.valueAsDate = new Date();

async function init() {
  try {
    const courses = await Api.get('/courses');
    courses.forEach(c => {
      const opt = document.createElement('option');
      opt.value = c.id;
      opt.textContent = c.name;
      courseSelect.appendChild(opt);
    });
  } catch (err) {
    showAlert(`Couldn't load courses: ${err.message}`);
  }
}

courseSelect.addEventListener('change', async () => {
  wrapper.innerHTML = '';
  selectedCourse = null;
  selectedTee = null;
  teeSelect.innerHTML = '<option value="">Loading tees…</option>';
  teeSelect.disabled = true;
  customTeeInput.hidden = true;

  if (!courseSelect.value) {
    teeSelect.innerHTML = '<option value="">Select a course first…</option>';
    return;
  }

  try {
    selectedCourse = await Api.get(`/courses/${courseSelect.value}`);
    populateTees(selectedCourse);
  } catch (err) {
    showAlert(`Couldn't load course details: ${err.message}`);
  }
});

teeSelect.addEventListener('change', () => {
  const value = teeSelect.value;
  if (value === '__custom__') {
    selectedTee = null;
    customTeeInput.hidden = false;
    customTeeInput.focus();
    renderScorecard(selectedCourse, null);
    return;
  }

  customTeeInput.hidden = true;
  selectedTee = selectedCourse?.tees?.find(t => String(t.id) === value) || null;
  renderScorecard(selectedCourse, selectedTee);
});

customTeeInput.addEventListener('input', () => {
  if (teeSelect.value === '__custom__') renderScorecard(selectedCourse, null);
});

function populateTees(course) {
  const tees = [...(course.tees || [])].sort((a, b) => {
    if (a.nineHoles !== b.nineHoles) return a.nineHoles ? 1 : -1;
    return a.name.localeCompare(b.name);
  });

  teeSelect.innerHTML = '';
  if (tees.length === 0) {
    teeSelect.innerHTML = '<option value="__custom__">Custom tee</option>';
    teeSelect.disabled = false;
    teeSelect.value = '__custom__';
    customTeeInput.hidden = false;
    customTeeInput.value = 'White';
    renderScorecard(course, null);
    return;
  }

  tees.forEach(tee => {
    const opt = document.createElement('option');
    opt.value = tee.id;
    opt.textContent = formatTeeLabel(tee);
    teeSelect.appendChild(opt);
  });

  const customOpt = document.createElement('option');
  customOpt.value = '__custom__';
  customOpt.textContent = 'Custom tee name…';
  teeSelect.appendChild(customOpt);
  teeSelect.disabled = false;

  selectedTee = tees[0];
  teeSelect.value = String(selectedTee.id);
  renderScorecard(course, selectedTee);
}

function formatTeeLabel(tee) {
  const details = [tee.courseType, tee.gender, tee.nineHoles ? '9 holes' : '18 holes']
    .filter(Boolean)
    .join(' · ');
  return details ? `${tee.name} (${details})` : tee.name;
}

function renderScorecard(course, tee) {
  if (!course) return;

  const courseHoles = [...(tee?.holes || [])].sort((a, b) => a.holeNumber - b.holeNumber);
  const isManualScorecard = courseHoles.length === 0;
  const holes = isManualScorecard
    ? Array.from({ length: tee?.nineHoles ? 9 : 18 }, (_, index) => ({ holeNumber: index + 1, par: 4 }))
    : courseHoles;

  const manualNote = isManualScorecard
    ? '<p class="progress-note manual-note">This tee has no hole data. Enter the par for each hole below before saving.</p>'
    : '';

  wrapper.innerHTML = `
    ${manualNote}
    <div class="card scorecard-card">
      <div class="scorecard-scroll">
        <table class="scorecard-table">
          <thead>
            <tr>
              <th>Hole</th><th>Par</th><th>Score</th><th>Putts</th><th>GIR</th><th>Fairway</th><th>Penalty</th>
            </tr>
          </thead>
          <tbody id="hole-rows"></tbody>
        </table>
      </div>
    </div>

    <div class="running-total">
      <div class="rt-item"><div class="rt-label">Score</div><div class="rt-value" id="rt-score">0</div></div>
      <div class="rt-item"><div class="rt-label">To Par</div><div class="rt-value" id="rt-topar">E</div></div>
      <div class="rt-item"><div class="rt-label">Putts</div><div class="rt-value" id="rt-putts">0</div></div>
      <div class="running-total-actions">
        <button class="btn btn-flag" id="save-btn">Save Round</button>
      </div>
    </div>
  `;

  const tbody = document.getElementById('hole-rows');
  holes.forEach(h => {
    const par = Number(h.par) || 4;
    const isPar3 = par === 3;
    const row = document.createElement('tr');
    row.dataset.holeNumber = h.holeNumber;
    row.dataset.par = par;
    row.dataset.manualPar = isManualScorecard ? 'true' : 'false';
    row.innerHTML = `
      <td class="text-cell">${h.holeNumber}</td>
      <td>${isManualScorecard ? `<input type="number" min="3" max="6" class="par-input" value="${par}" />` : par}</td>
      <td><input type="number" min="1" class="score-input" value="${par}" /></td>
      <td><input type="number" min="0" class="putts-input" value="2" /></td>
      <td class="center-cell"><input type="checkbox" class="gir-input" /></td>
      <td class="fairway-cell ${isPar3 ? 'na' : ''}">${isPar3 ? 'N/A' : fairwaySelectHtml()}</td>
      <td><input type="number" min="0" class="penalty-input" value="0" /></td>
    `;
    tbody.appendChild(row);
  });

  tbody.addEventListener('input', updateRunningTotal);
  document.getElementById('save-btn').addEventListener('click', saveRound);
  updateRunningTotal();
}

function fairwaySelectHtml() {
  return '<select class="fairway-input"><option value="">—</option><option value="true">Hit</option><option value="false">Missed</option></select>';
}

function rowPar(row) {
  const parInput = row.querySelector('.par-input');
  return parInput ? Number(parInput.value || 0) : Number(row.dataset.par);
}

function syncFairwayField(row) {
  if (row.dataset.manualPar !== 'true') return;
  const cell = row.querySelector('.fairway-cell');
  if (!cell) return;

  const isPar3 = rowPar(row) === 3;
  const hasSelect = !!cell.querySelector('.fairway-input');
  if (isPar3 && hasSelect) {
    cell.className = 'fairway-cell na';
    cell.textContent = 'N/A';
  } else if (!isPar3 && !hasSelect) {
    cell.className = 'fairway-cell';
    cell.innerHTML = fairwaySelectHtml();
  }
}

function updateRunningTotal() {
  const rows = [...document.querySelectorAll('#hole-rows tr')];
  let score = 0, par = 0, putts = 0;
  rows.forEach(r => {
    syncFairwayField(r);
    score += Number(r.querySelector('.score-input').value || 0);
    putts += Number(r.querySelector('.putts-input').value || 0);
    par += rowPar(r);
  });

  document.getElementById('rt-score').textContent = score;
  document.getElementById('rt-putts').textContent = putts;
  document.getElementById('rt-topar').textContent = toPar(score - par);
}

async function saveRound() {
  const rows = [...document.querySelectorAll('#hole-rows tr')];
  const customTee = customTeeInput.value.trim();
  const teeName = selectedTee?.name || customTee;

  if (!teeName) {
    showAlert('Please select a tee or enter a custom tee name.');
    return;
  }
  if (!courseSelect.value || rows.length === 0) {
    showAlert('Please select a course and enter at least one hole.');
    return;
  }

  const holes = rows.map(r => {
    const fairwaySelect = r.querySelector('.fairway-input');
    const fairwayValue = fairwaySelect && fairwaySelect.value !== '' ? fairwaySelect.value === 'true' : null;
    const parInput = r.querySelector('.par-input');
    return {
      holeNumber: Number(r.dataset.holeNumber),
      par: parInput ? Number(parInput.value) : null,
      score: Number(r.querySelector('.score-input').value),
      putts: Number(r.querySelector('.putts-input').value),
      gir: r.querySelector('.gir-input').checked,
      fairwayHit: fairwayValue,
      penalty: Number(r.querySelector('.penalty-input').value)
    };
  });

  const dto = {
    courseId: Number(courseSelect.value),
    courseTeeId: selectedTee?.id || null,
    date: dateInput.value,
    tee: selectedTee ? null : teeName,
    holes
  };

  const btn = document.getElementById('save-btn');
  btn.disabled = true;
  btn.textContent = 'Saving…';
  try {
    const round = await Api.post('/rounds', dto);
    location.href = `/round-details.html?id=${round.id}`;
  } catch (err) {
    showAlert(`Couldn't save the round: ${err.message}`);
    btn.disabled = false;
    btn.textContent = 'Save Round';
  }
}

function showAlert(message) {
  alertBox.innerHTML = `<div class="alert error">${escapeHtml(message)}</div>`;
}

init();
