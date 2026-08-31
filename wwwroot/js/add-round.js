renderNav('add-round');

const courseSelect = document.getElementById('course-select');
const dateInput = document.getElementById('date-input');
const teeInput = document.getElementById('tee-input');
const wrapper = document.getElementById('scorecard-wrapper');
const alertBox = document.getElementById('form-alert');

let selectedCourse = null;

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
  if (!courseSelect.value) { wrapper.innerHTML = ''; selectedCourse = null; return; }

  try {
    selectedCourse = await Api.get(`/courses/${courseSelect.value}`);
    renderScorecard(selectedCourse);
  } catch (err) {
    showAlert(`Couldn't load course details: ${err.message}`);
  }
});

function renderScorecard(course) {
  const courseHoles = [...course.holes].sort((a, b) => a.holeNumber - b.holeNumber);
  const isManualScorecard = courseHoles.length === 0;
  const holes = isManualScorecard
    ? Array.from({ length: 18 }, (_, index) => ({ holeNumber: index + 1, par: 4 }))
    : courseHoles;

  const manualNote = isManualScorecard
    ? '<p class="progress-note" style="margin:20px 0 0;">This course has no hole data. Enter the par for each hole below before saving.</p>'
    : '';

  wrapper.innerHTML = `
    ${manualNote}
    <div class="card" style="margin-top:20px;">
      <table class="scorecard-table">
        <thead>
          <tr>
            <th>Hole</th><th>Par</th><th>Score</th><th>Putts</th><th>GIR</th><th>Fairway</th><th>Penalty</th>
          </tr>
        </thead>
        <tbody id="hole-rows"></tbody>
      </table>
    </div>

    <div class="running-total">
      <div class="rt-item"><div class="rt-label">Score</div><div class="rt-value" id="rt-score">0</div></div>
      <div class="rt-item"><div class="rt-label">To Par</div><div class="rt-value" id="rt-topar">E</div></div>
      <div class="rt-item"><div class="rt-label">Putts</div><div class="rt-value" id="rt-putts">0</div></div>
      <div style="margin-left:auto;">
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
      <td style="text-align:center;"><input type="checkbox" class="gir-input" /></td>
      <td class="fairway-cell ${isPar3 ? 'na' : ''}">
        ${isPar3 ? 'N/A' : fairwaySelectHtml()}
      </td>
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
  const tee = teeInput.value.trim();

  if (!tee) {
    showAlert('Please enter a tee name, such as White or Blue.');
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
    date: dateInput.value,
    tee,
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
  alertBox.innerHTML = `<div class="alert error">${message}</div>`;
}

init();
