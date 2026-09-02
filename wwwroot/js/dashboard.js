renderNav('dashboard');

const CHART_COLORS = { fairway: '#2f6844', flag: '#c97a2b', grid: '#ded7c2' };

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
        ${statCard('Average Score', data.averageScore.toFixed(1))}
        ${statCard('Best Score', data.bestScore, 'accent')}
        ${statCard('Average Putts', data.averagePutts.toFixed(1))}
        ${statCard('GIR %', pct(data.averageGirPercentage))}
        ${statCard('Fairway %', data.averageFairwayPercentage != null ? pct(data.averageFairwayPercentage) : '—')}
      </div>

      <div class="section-title">Performance trends</div>
      <div class="chart-grid">
        <div class="card chart-card"><h3>Score trend</h3><canvas id="scoreChart" height="160"></canvas></div>
        <div class="card chart-card"><h3>GIR % trend</h3><canvas id="girChart" height="160"></canvas></div>
        <div class="card chart-card"><h3>Putting trend</h3><canvas id="puttsChart" height="160"></canvas></div>
      </div>

      <div class="section-title">Recent rounds</div>
      <div class="card list-card">
        <table>
          <thead><tr><th>Date</th><th class="text-cell">Course</th><th>Score</th><th>To Par</th></tr></thead>
          <tbody>
            ${data.recentRounds.map(r => `
              <tr class="clickable" data-round-id="${r.id}" tabindex="0">
                <td>${fmtDate(r.date)}</td>
                <td class="text-cell">${escapeHtml(r.courseName)}</td>
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
    el.querySelectorAll('[data-round-id]').forEach(row => {
      const open = () => location.href = `/round-details.html?id=${row.dataset.roundId}`;
      row.addEventListener('click', open);
      row.addEventListener('keydown', event => { if (event.key === 'Enter' || event.key === ' ') open(); });
    });
    drawCharts(data);
  } catch (err) {
    el.innerHTML = `<div class="alert error">Couldn't load the dashboard: ${err.message}</div>`;
  }
}

function insightCard(insight) {
  return `<article class="card insight-card"><span class="eyebrow">Based on recent rounds</span><h3>${escapeHtml(insight.title)}</h3><p>${escapeHtml(insight.evidence)}</p><strong>${escapeHtml(insight.recommendation)}</strong><a href="/practice.html">Open practice plan →</a></article>`;
}

function statCard(label, value, extraClass = '') {
  return `<div class="card stat-card"><div class="stat-label">${label}</div><div class="stat-value ${extraClass}">${value}</div></div>`;
}

function drawCharts(data) {
  const labels = data.scoreTrend.map(p => fmtDate(p.date));

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
    data: { labels, datasets: [{ data: data.scoreTrend.map(p => p.value), borderColor: CHART_COLORS.fairway, backgroundColor: CHART_COLORS.fairway, tension: 0.3, pointRadius: 3 }] },
    options: baseOptions
  });

  new Chart(document.getElementById('girChart'), {
    type: 'line',
    data: { labels, datasets: [{ data: data.girTrend.map(p => p.value), borderColor: CHART_COLORS.flag, backgroundColor: CHART_COLORS.flag, tension: 0.3, pointRadius: 3 }] },
    options: baseOptions
  });

  new Chart(document.getElementById('puttsChart'), {
    type: 'line',
    data: { labels, datasets: [{ data: data.puttsTrend.map(p => p.value), borderColor: '#5a7a92', backgroundColor: '#5a7a92', tension: 0.3, pointRadius: 3 }] },
    options: baseOptions
  });
}

loadDashboard();
