renderNav('practice');

const practice = document.getElementById('practice-content');
Api.get('/statistics/overview').then(data => {
  const insights = data.insights || [];
  practice.innerHTML = insights.length ? `<div class="practice-list">${insights.map((item, index) => `
    <article class="card practice-item">
      <div class="practice-number">${String(index + 1).padStart(2, '0')}</div>
      <div><span class="eyebrow">${escapeHtml(item.severity === 'info' ? 'Next checkpoint' : 'Practice priority')}</span><h2>${escapeHtml(item.title)}</h2><p>${escapeHtml(item.evidence)}</p><strong>${escapeHtml(item.recommendation)}</strong>
      ${item.drill ? `<details class="practice-drill"><summary>${escapeHtml(item.drill.title)} · ${Number(item.drill.minutes)} min</summary><ol>${item.drill.steps.map(step => `<li>${escapeHtml(step)}</li>`).join('')}</ol><p><strong>Measure:</strong> ${escapeHtml(item.drill.measure)}</p><p>Keep your results in your own notes; practice-session tracking is not available yet.</p></details>` : ''}
      ${item.evidenceRoundIds?.length ? `<details class="practice-evidence"><summary>Review ${item.evidenceRoundIds.length} supporting rounds</summary><ul>${item.evidenceRoundIds.filter(Number.isInteger).map(id => `<li><a href="/round-details.html?id=${id}">Round #${id}</a></li>`).join('')}</ul></details>` : ''}
      </div>
    </article>`).join('')}</div>` : '<div class="card empty-state"><h3>No practice focus yet.</h3><p>Complete three rounds to establish a useful baseline.</p></div>';
}).catch(error => { practice.innerHTML = `<div class="alert error">${escapeHtml(error.message)}</div>`; });
