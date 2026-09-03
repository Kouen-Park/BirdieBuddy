renderNav('statistics');

const overviewEl = document.getElementById('overview-content');
const roundSelect = document.getElementById('round-select');
const breakdownEl = document.getElementById('round-breakdown');
const statsFilters = document.getElementById('stats-filters');
let overviewRequest = 0;
let teeRequest = 0;

function overviewPath() {
  const params = new URLSearchParams();
  [['courseId', 'stats-course'], ['courseTeeId', 'stats-tee'], ['holeCount', 'stats-holes'], ['from', 'stats-from'], ['to', 'stats-to']].forEach(([key, id]) => {
    const value = document.getElementById(id).value;
    if (value) params.set(key, value);
  });
  return `/statistics/overview?${params}`;
}

async function loadOverview() {
  const request = ++overviewRequest;
  try {
    const data = await Api.get(overviewPath());
    if (request !== overviewRequest) return;

    if (data.roundsPlayed === 0) {
      overviewEl.innerHTML = `
        <div class="card empty-state">
          <h3>No completed rounds match these filters.</h3>
          <p>Try a different course, round length or date range.</p>
          <a href="/live-round.html" class="btn btn-flag space-top">Start your first round</a>
        </div>`;
      return;
    }

    overviewEl.innerHTML = `
      <div class="stat-grid">
        <div class="card stat-card"><div class="stat-label">Rounds Played</div><div class="stat-value">${data.roundsPlayed}</div></div>
        <div class="card stat-card"><div class="stat-label">Putts per hole</div><div class="stat-value">${data.averagePuttsPerHole.toFixed(2)}</div></div>
        <div class="card stat-card"><div class="stat-label">Average GIR %</div><div class="stat-value">${pct(data.averageGirPercentage)}</div></div>
        <div class="card stat-card"><div class="stat-label">Average Fairway %</div><div class="stat-value">${data.averageFairwayPercentage != null ? pct(data.averageFairwayPercentage) : '—'}</div></div>
      </div>
      ${roundLengthSummary(data)}
      <p class="progress-note">GIR uses all recorded holes. Fairway percentage excludes par 3s and unrecorded fairways. Rates are weighted by holes, not by rounds.</p>
    `;
  } catch (err) {
    if (request !== overviewRequest) return;
    overviewEl.innerHTML = `<div class="alert error">Couldn't load statistics: ${escapeHtml(err.message)}</div>`;
  }
}

async function loadRoundOptions() {
  try {
    const rounds = await Api.get('/rounds');
    const completedRounds = rounds.filter(r => r.status === 'Completed');
    if (completedRounds.length === 0) {
      roundSelect.innerHTML = `<option>No rounds yet</option>`;
      return;
    }

    roundSelect.innerHTML = completedRounds
      .map(r => `<option value="${r.id}">${fmtDate(r.date)} · ${escapeHtml(r.courseName)} (${r.totalScore})</option>`)
      .join('');

    roundSelect.addEventListener('change', () => loadRoundBreakdown(roundSelect.value));
    loadRoundBreakdown(roundSelect.value);
  } catch (err) {
    breakdownEl.innerHTML = `<div class="alert error">Couldn't load rounds: ${escapeHtml(err.message)}</div>`;
  }
}

async function loadRoundBreakdown(roundId) {
  if (!roundId) return;
  breakdownEl.innerHTML = `<p class="progress-note">Loading&hellip;</p>`;

  try {
    const s = await Api.get(`/statistics/round/${roundId}`);

    breakdownEl.innerHTML = `
      <div class="stat-grid">
        <div class="card stat-card"><div class="stat-label">Score to Par</div><div class="stat-value">${toPar(s.scoreToPar)}</div></div>
        <div class="card stat-card"><div class="stat-label">Eagles</div><div class="stat-value">${s.eagles}</div></div>
        <div class="card stat-card"><div class="stat-label">Birdies</div><div class="stat-value">${s.birdies}</div></div>
        <div class="card stat-card"><div class="stat-label">Pars</div><div class="stat-value">${s.pars}</div></div>
        <div class="card stat-card"><div class="stat-label">Bogeys+</div><div class="stat-value">${s.bogeys + s.doubleBogeysOrWorse}</div></div>
      </div>
      <div class="chart-grid">
        ${parCard('Par 3', s.par3)}
        ${parCard('Par 4', s.par4)}
        ${parCard('Par 5', s.par5)}
      </div>
    `;
  } catch (err) {
    breakdownEl.innerHTML = `<div class="alert error">Couldn't load this round's statistics: ${escapeHtml(err.message)}</div>`;
  }
}

function parCard(label, s) {
  const body = s.holesPlayed === 0
    ? `<p class="progress-note">No holes of this type.</p>`
    : `<div class="stat-value">${s.averageScore.toFixed(2)}</div><p class="progress-note">${s.holesPlayed} holes · avg ${toPar(Math.round(s.averageScoreToPar * 100) / 100)}</p>`;
  return `<div class="card chart-card"><h3>${label}</h3>${body}</div>`;
}

loadOverview();
loadRoundOptions();
statsFilters.addEventListener('submit', event => { event.preventDefault(); loadOverview(); });
Api.get('/courses').then(courses => {
  const select = document.getElementById('stats-course');
  courses.forEach(course => { const option = document.createElement('option'); option.value = course.id; option.textContent = course.name; select.append(option); });
}).catch(error => { overviewEl.textContent = `Could not load course filters: ${error.message}`; });

document.getElementById('stats-course').addEventListener('change', async event => {
  const request = ++teeRequest;
  const select = document.getElementById('stats-tee');
  select.replaceChildren(new Option('All tees', ''));
  select.disabled = true;
  if (!event.target.value) return;
  try {
    const course = await Api.get(`/courses/${event.target.value}`);
    if (request !== teeRequest) return;
    course.tees.forEach(tee => select.append(new Option(tee.name, tee.id)));
    select.disabled = false;
  } catch (error) {
    if (request === teeRequest) overviewEl.textContent = `Could not load tees: ${error.message}`;
  }
});
