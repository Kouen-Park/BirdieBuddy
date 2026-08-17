renderNav('dashboard');

const CHART_COLORS = { fairway: '#2f6844', flag: '#c97a2b', grid: '#ded7c2' };

async function loadDashboard() {
  const el = document.getElementById('dashboard-content');

  try {
    const data = await Api.get('/statistics/overview');

    if (data.roundsPlayed === 0) {
      el.innerHTML = `
        <div class="card empty-state">
          <h3>No rounds recorded yet.</h3>
          <p>Play your first round and let Birdie Buddy track your game.</p>
          <a href="/add-round.html" class="btn btn-flag" style="margin-top:14px;">Add your first round</a>
        </div>`;
      return;
    }

    el.innerHTML = `
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
              <tr class="clickable" onclick="location.href='/round-details.html?id=${r.id}'">
                <td>${fmtDate(r.date)}</td>
                <td class="text-cell">${r.courseName}</td>
                <td>${r.totalScore}</td>
                <td><span class="pill ${toParPillClass(r.scoreToPar)}">${toPar(r.scoreToPar)}</span></td>
              </tr>`).join('')}
          </tbody>
        </table>
      </div>
    `;

    drawCharts(data);
  } catch (err) {
    el.innerHTML = `<div class="alert error">Couldn't load the dashboard: ${err.message}</div>`;
  }
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
