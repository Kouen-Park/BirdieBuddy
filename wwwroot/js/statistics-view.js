// Shared presentation: raw totals are only compared within one round length.
function roundLengthSummary(data) {
  const groups = data.byRoundLength || [];
  const average = value => value == null ? 'Not enough rounds' : toPar(Number(value.toFixed(1)));
  return `<div class="card list-card"><table>
    <caption>Completed rounds by length · last 5/10 averages require 5/10 rounds of that length</caption>
    <thead><tr><th>Length</th><th>Rounds</th><th>Average score</th><th>Best score</th><th>Average to par</th><th>Last 5 to par</th><th>Last 10 to par</th></tr></thead>
    <tbody>${groups.map(g => `<tr><th scope="row">${g.holeCount} holes</th><td>${g.roundsPlayed}</td>
      <td>${g.averageScore.toFixed(1)}</td><td>${g.bestScore}</td><td>${average(g.averageScoreToPar)}</td>
      <td>${average(g.recentFiveScoreToPar)}</td><td>${average(g.recentTenScoreToPar)}</td></tr>`).join('')}</tbody>
    </table></div>`;
}
