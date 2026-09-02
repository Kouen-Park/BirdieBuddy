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
          <thead>
            <tr><th>Hole</th><th>Par</th><th>Score</th><th>+/-</th><th>Putts</th><th>GIR</th><th>Fairway</th><th>Penalty</th></tr>
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
  } catch (err) {
    content.innerHTML = `<div class="alert error">Couldn't load this round: ${err.message}</div>`;
  }
}

async function editRound(round) {
  const date = prompt('Round date (YYYY-MM-DD)', round.date.slice(0, 10));
  if (!date) return;
  try {
    await Api.put(`/rounds/${round.id}`, { date, courseTeeId: round.courseTeeId, tee: round.tee });
    location.reload();
  } catch (error) { content.insertAdjacentHTML('afterbegin', `<div class="alert error">${escapeHtml(error.message)}</div>`); }
}

async function deleteRound() {
  if (!confirm('Delete this round permanently?')) return;
  try { await Api.del(`/rounds/${roundId}`); location.href = '/rounds.html'; }
  catch (error) { content.insertAdjacentHTML('afterbegin', `<div class="alert error">${escapeHtml(error.message)}</div>`); }
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
