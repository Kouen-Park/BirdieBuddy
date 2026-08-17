renderNav('add-round');

const courseSelect = document.getElementById('course-select');
const dateInput = document.getElementById('date-input');
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
  const holes = [...course.holes].sort((a, b) => a.holeNumber - b.holeNumber);

  wrapper.innerHTML = `
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
    const isPar3 = h.par === 3;
    const row = document.createElement('tr');
    row.dataset.holeNumber = h.holeNumber;
    row.dataset.par = h.par;
    row.innerHTML = `
      <td class="text-cell">${h.holeNumber}</td>
      <td>${h.par}</td>
      <td><input type="number" min="1" class="score-input" value="${h.par}" /></td>
      <td><input type="number" min="0" class="putts-input" value="2" /></td>
      <td style="text-align:center;"><input type="checkbox" class="gir-input" /></td>
      <td class="${isPar3 ? 'na' : ''}">
        ${isPar3 ? 'N/A' : `<select class="fairway-input"><option value="">—</option><option value="true">Hit</option><option value="false">Missed</option></select>`}
      </td>
      <td><input type="number" min="0" class="penalty-input" value="0" /></td>
    `;
    tbody.appendChild(row);
  });

  tbody.addEventListener('input', updateRunningTotal);
  document.getElementById('save-btn').addEventListener('click', saveRound);
  updateRunningTotal();
}

function updateRunningTotal() {
  const rows = [...document.querySelectorAll('#hole-rows tr')];
  let score = 0, par = 0, putts = 0;

  rows.forEach(r => {
    score += Number(r.querySelector('.score-input').value || 0);
    putts += Number(r.querySelector('.putts-input').value || 0);
    par += Number(r.dataset.par);
  });

  document.getElementById('rt-score').textContent = score;
  document.getElementById('rt-putts').textContent = putts;
  document.getElementById('rt-topar').textContent = toPar(score - par);
}

async function saveRound() {
  const rows = [...document.querySelectorAll('#hole-rows tr')];

  const holes = rows.map(r => {
    const fairwaySelect = r.querySelector('.fairway-input');
    const fairwayValue = fairwaySelect && fairwaySelect.value !== '' ? fairwaySelect.value === 'true' : null;

    return {
      holeNumber: Number(r.dataset.holeNumber),
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
    tee: document.getElementById('tee-select').value,
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
