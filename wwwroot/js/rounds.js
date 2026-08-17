renderNav('rounds');

async function loadRounds() {
  const el = document.getElementById('rounds-content');

  try {
    const rounds = await Api.get('/rounds');

    if (rounds.length === 0) {
      el.innerHTML = `
        <div class="card empty-state">
          <h3>No rounds recorded yet.</h3>
          <p>Play your first round and let Birdie Buddy track your game.</p>
          <a href="/add-round.html" class="btn btn-flag" style="margin-top:14px;">Add your first round</a>
        </div>`;
      return;
    }

    el.innerHTML = `
      <div class="card list-card">
        <table>
          <thead>
            <tr><th>Date</th><th class="text-cell">Course</th><th class="text-cell">Tee</th><th>Score</th><th>To Par</th></tr>
          </thead>
          <tbody>
            ${rounds.map(r => `
              <tr class="clickable" onclick="location.href='/round-details.html?id=${r.id}'">
                <td>${fmtDate(r.date)}</td>
                <td class="text-cell">${r.courseName}</td>
                <td class="text-cell">${r.tee}</td>
                <td>${r.totalScore}</td>
                <td><span class="pill ${toParPillClass(r.scoreToPar)}">${toPar(r.scoreToPar)}</span></td>
              </tr>`).join('')}
          </tbody>
        </table>
      </div>
    `;
  } catch (err) {
    el.innerHTML = `<div class="alert error">Couldn't load rounds: ${err.message}</div>`;
  }
}

loadRounds();
