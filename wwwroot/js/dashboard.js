renderNav('dashboard');

const chartToken = (name, fallback) => getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback;

async function loadDashboard() {
  const el = document.getElementById('dashboard-content');

  try {
    const [data, draftPage] = await Promise.all([
      Api.get('/statistics/overview'),
      Api.get('/rounds/page?status=Draft&limit=1')
    ]);

    const draft = draftPage.items[0];

    if (data.roundsPlayed === 0) {
      el.innerHTML = `
        ${draft ? `<a class="resume-round" href="/live-round.html?id=${draft.id}"><span><small>Round in progress</small><strong>${escapeHtml(draft.courseName)}</strong><em>${draft.holesPlayed} of ${draft.expectedHoles} holes saved</em></span><b>Resume →</b></a>` : ''}
        <div class="card empty-state">
          <h3>No rounds recorded yet.</h3>
          <p>Play your first round and let Birdie Buddy track your game.</p>
          <a href="/live-round.html" class="btn btn-flag space-top">Start your first round</a>
        </div>`;
      return;
    }

    el.innerHTML = `
      ${draft ? `<a class="resume-round" href="/live-round.html?id=${draft.id}"><span><small>Round in progress</small><strong>${escapeHtml(draft.courseName)}</strong><em>${draft.holesPlayed} of ${draft.expectedHoles} holes saved</em></span><b>Resume →</b></a>` : `<a class="resume-round new-round" href="/live-round.html"><span><small>Ready for the first tee?</small><strong>Start a live round</strong><em>Your scorecard saves after every hole.</em></span><b>Start →</b></a>`}
      <div class="stat-grid">
        ${statCard('Completed rounds', data.roundsPlayed)}
        ${statCard('Putts per hole', data.averagePuttsPerHole.toFixed(2))}
        ${statCard('GIR %', pct(data.averageGirPercentage))}
        ${statCard('Fairway %', data.averageFairwayPercentage != null ? pct(data.averageFairwayPercentage) : '—')}
      </div>
      ${roundLengthSummary(data)}

      <div class="section-title">Performance trends</div>
      <div class="chart-grid">
        <div class="card chart-card"><h3>Score to par per hole</h3><p>All completed rounds · 0 = par</p><canvas id="scoreChart" height="160" role="img" aria-describedby="scoreChartSummary" aria-label="Score to par per hole over time"></canvas><p class="chart-summary" id="scoreChartSummary">Loading trend summary…</p></div>
        <div class="card chart-card"><h3>GIR % trend</h3><canvas id="girChart" height="160" role="img" aria-describedby="girChartSummary" aria-label="Greens in regulation percentage over time"></canvas><p class="chart-summary" id="girChartSummary">Loading trend summary…</p></div>
        <div class="card chart-card"><h3>Putts per hole</h3><p>All completed rounds · strokes per hole</p><canvas id="puttsChart" height="160" role="img" aria-describedby="puttsChartSummary" aria-label="Putts per hole over time"></canvas><p class="chart-summary" id="puttsChartSummary">Loading trend summary…</p></div>
      </div>

      <div class="section-title">Recent rounds</div>
      <div class="card list-card">
        <table>
          <thead><tr><th>Date</th><th class="text-cell">Course</th><th>Holes</th><th>Score</th><th>To Par</th></tr></thead>
          <tbody>
            ${data.recentRounds.map(r => `
              <tr>
                <td>${fmtDate(r.date)}</td>
                <td class="text-cell"><a class="table-row-link" href="/round-details.html?id=${r.id}">${escapeHtml(r.courseName)}</a></td>
                <td>${r.holesPlayed}</td>
                <td>${r.totalScore}</td>
                <td><span class="pill ${toParPillClass(r.scoreToPar)}">${toPar(r.scoreToPar)}</span></td>
              </tr>`).join('')}
          </tbody>
        </table>
      </div>
    `;

    if (data.insights?.length) {
      el.insertAdjacentHTML('beforeend', `<div class="section-title">Your next focus</div>${insightCard(data.insights[0])}`);
    }
    drawCharts(data);
  } catch (err) {
    el.innerHTML = `<div class="alert error">Couldn't load the dashboard: ${escapeHtml(err.message)}</div>`;
  }
}

function insightCard(insight) {
  return `<article class="card insight-card"><span class="eyebrow">Based on recent rounds</span><h3>${escapeHtml(insight.title)}</h3><p>${escapeHtml(insight.evidence)}</p><strong>${escapeHtml(insight.recommendation)}</strong><a href="/practice.html">Open practice plan →</a></article>`;
}

function statCard(label, value, extraClass = '') {
  return `<div class="card stat-card"><div class="stat-label">${label}</div><div class="stat-value ${extraClass}">${value}</div></div>`;
}

function drawCharts(data) {
  const labels = data.scoreToParPerHoleTrend.map(p => fmtDate(p.date));
  const CHART_COLORS = {
    fairway: chartToken('--fairway', '#2c7168'),
    flag: chartToken('--flag', '#e56a4e'),
    grid: chartToken('--line', '#d6e1dc'),
    putts: chartToken('--chart-putts', '#5a7a92')
  };

  const baseOptions = {
    responsive: true,
    plugins: { legend: { display: false } },
    scales: {
      x: { grid: { display: false } },
      y: { grid: { color: CHART_COLORS.grid } }
    }
  };

  new Chart(document.getElementById('scoreChart'), {
    type: 'line',
    data: { labels, datasets: [{ label: 'Score to par per hole', data: data.scoreToParPerHoleTrend.map(p => p.value), borderColor: CHART_COLORS.fairway, backgroundColor: CHART_COLORS.fairway, tension: 0.3, pointRadius: 3 }] },
    options: baseOptions
  });

  new Chart(document.getElementById('girChart'), {
    type: 'line',
    data: { labels, datasets: [{ label: 'GIR percentage', data: data.girTrend.map(p => p.value), borderColor: CHART_COLORS.flag, backgroundColor: CHART_COLORS.flag, tension: 0.3, pointRadius: 3 }] },
    options: baseOptions
  });

  new Chart(document.getElementById('puttsChart'), {
    type: 'line',
    data: { labels, datasets: [{ label: 'Putts per hole', data: data.puttsPerHoleTrend.map(p => p.value), borderColor: CHART_COLORS.putts, backgroundColor: CHART_COLORS.putts, tension: 0.3, pointRadius: 3 }] },
    options: baseOptions
  });

  document.getElementById('scoreChartSummary').textContent = trendSummary(data.scoreToParPerHoleTrend, 'Score to par per hole');
  document.getElementById('girChartSummary').textContent = trendSummary(data.girTrend, 'GIR percentage', '%');
  document.getElementById('puttsChartSummary').textContent = trendSummary(data.puttsPerHoleTrend, 'Putts per hole');
}

function trendSummary(points, label, suffix = '') {
  if (!points?.length) return 'No completed-round data is available yet.';
  const first = Number(points[0].value).toFixed(2);
  const latest = Number(points.at(-1).value).toFixed(2);
  return `${label}: first ${first}${suffix}, latest ${latest}${suffix}.`;
}

loadDashboard();
