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
      ${movingAverageSummary(data)}
      ${parTypeTrendSummary(data)}
      <p class="progress-note">GIR uses all recorded holes. Fairway percentage excludes par 3s and unrecorded fairways. Rates are weighted by holes, not by rounds.</p>
    `;
  } catch (err) {
    if (request !== overviewRequest) return;
    overviewEl.innerHTML = `<div class="alert error">Couldn't load statistics: ${escapeHtml(err.message)}</div>`;
  }
}

function movingAverageSummary(data) {
  const points = (data.movingAverageTrend || []).filter(p => p.fiveRoundScoreToPar != null || p.tenRoundScoreToPar != null);
  if (!points.length) return '<div class="card empty-state trend-card"><h3>Moving averages unlock after five rounds</h3><p>Record complete rounds to compare recent 5- and 10-round score-to-par and putting averages.</p></div>';
  const latest = points[points.length - 1];
  const metric = (label, value, suffix = '') => `<div><span class="stat-label">${label}</span><strong class="trend-value">${value == null ? '—' : Number(value).toFixed(2)}${suffix}</strong></div>`;
  return `<section class="card trend-card" aria-labelledby="moving-average-title"><div class="section-title" id="moving-average-title">Recent moving averages</div><p class="progress-note">Rolling windows ending ${fmtDate(latest.date)} · score to par is normalized per hole.</p><div class="trend-grid">${metric('Last 5 · score to par / hole', latest.fiveRoundScoreToPar)}${metric('Last 10 · score to par / hole', latest.tenRoundScoreToPar)}${metric('Last 5 · putts / hole', latest.fiveRoundPuttsPerHole)}${metric('Last 10 · putts / hole', latest.tenRoundPuttsPerHole)}</div><div class="trend-bars" role="img" aria-label="Recent moving average history">${points.slice(-10).map(p => `<div class="trend-bar-group"><span class="trend-bar five" style="--bar:${Math.min(100, Math.max(8, ((p.fiveRoundPuttsPerHole || 0) / 3) * 100))}%"></span><span class="trend-bar ten" style="--bar:${Math.min(100, Math.max(8, ((p.tenRoundPuttsPerHole || 0) / 3) * 100))}%"></span><small>${fmtDate(p.date)}</small></div>`).join('')}</div></section>`;
}

function parTypeTrendSummary(data) {
  const points = data.parTypeTrend || [];
  if (!points.length) return '';
  const latestDate = points.map(p => p.date).sort().pop();
  const latest = points.filter(p => p.date === latestDate).sort((a, b) => a.par - b.par);
  return `<section class="card trend-card" aria-labelledby="par-trend-title"><div class="section-title" id="par-trend-title">Latest par-type trend</div><p class="progress-note">${fmtDate(latestDate)} · score to par and GIR by par type.</p><div class="chart-grid">${latest.map(p => `<div class="chart-card"><h3>Par ${p.par}</h3><div class="stat-value">${toPar(Math.round(p.averageScoreToPar * 100) / 100)}</div><p class="progress-note">GIR ${pct(p.girPercentage)}</p></div>`).join('')}</div></section>`;
}

async function loadRoundOptions() {
  try {
    const rounds = await Api.get('/rounds/options?limit=100');
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
