// Local, in-memory UI fixture only. Never used by the production application.
// Run: node tests/browser/smoke-server.cjs
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const webroot = path.resolve(__dirname, '../../wwwroot');
const course = { id: 1, name: 'Smoke Test Club', tees: [{ id: 1, name: 'Back nine', nineHoles: true,
  holes: Array.from({ length: 9 }, (_, i) => ({ holeNumber: i + 10, par: i === 0 ? 3 : 4, distance: 320 })) }] };
const round = { id: 1, courseId: 1, courseName: course.name, courseTeeId: 1, tee: 'Back nine',
  date: '2026-09-03', status: 'Draft', currentHole: 10, expectedHoles: 9,
  holeNumbers: course.tees[0].holes.map(h => h.holeNumber), holes: [] };
const completed = { ...round, id: 2, status: 'Completed', holes: course.tees[0].holes.map(h => ({
  id: h.holeNumber, holeNumber: h.holeNumber, par: h.par, score: 4, putts: 2, gir: true,
  fairwayHit: h.par === 3 ? null : true, penalty: 0
})) };
const parStats = { holesPlayed: 0, averageScore: 0, averageScoreToPar: 0 };
// Separate round/key avoids interfering with the original autosave smoke fixture.
const conflictRound = { ...round, id: 3, holes: [] };
const injectedConflicts = new Set();
const holeValues = h => h && [h.par, h.score, h.putts, h.gir, h.fairwayHit, h.penalty];
const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, 'http://127.0.0.1');
  const json = (value, status = 200) => { res.writeHead(status, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' }); res.end(JSON.stringify(value)); };
  if (url.pathname === '/api/auth/me') return json({ id: 99991, displayName: 'Local smoke test', email: 'smoke@example.test' });
  if (url.pathname === '/api/security/csrf') return json({ token: 'local-fixture-token' });
  if (url.pathname === '/api/rounds/1' && req.method === 'GET') return json(round);
  if (url.pathname === '/api/rounds/3' && req.method === 'GET') return json(conflictRound);
  if (url.pathname === '/api/rounds/2' && req.method === 'GET') return json(completed);
  if (url.pathname === '/api/statistics/round/2') return json({
    totalScore: completed.holes.reduce((n, h) => n + h.score, 0), scoreToPar: completed.holes.reduce((n, h) => n + h.score - h.par, 0),
    totalPutts: completed.holes.reduce((n, h) => n + h.putts, 0), girPercentage: 100, fairwayPercentage: 100,
    par3: parStats, par4: parStats, par5: parStats
  });
  const edit = url.pathname.match(/^\/api\/rounds\/2\/holes\/(\d+)$/);
  if (edit && req.method === 'PUT') {
    let body = '';
    for await (const chunk of req) body += chunk;
    const data = JSON.parse(body);
    const hole = completed.holes.find(h => h.id === Number(edit[1]));
    if (!hole) return json({}, 404);
    Object.assign(hole, { score: data.score, putts: data.putts, gir: data.gir, fairwayHit: data.fairwayHit, penalty: data.penalty });
    res.writeHead(204); return res.end();
  }
  if (url.pathname === '/api/courses/1') return json(course);
  if (url.pathname === '/api/courses') return json([course]);
  const match = url.pathname.match(/^\/api\/rounds\/(1|3)\/holes\/by-number\/(\d+)$/);
  if (match && req.method === 'PUT') {
    let body = '';
    for await (const chunk of req) body += chunk;
    const data = JSON.parse(body);
    const targetRound = match[1] === '3' ? conflictRound : round;
    const number = Number(match[2]);
    const hole = { id: number, holeNumber: number, par: data.par, score: data.score,
      putts: data.putts, gir: data.gir, fairwayHit: data.fairwayHit, penalty: data.penalty };
    if (targetRound === conflictRound && !injectedConflicts.has(number)) {
      injectedConflicts.add(number);
      targetRound.holes = targetRound.holes.filter(h => h.holeNumber !== number).concat({ ...hole, score: 8 });
    }
    const current = targetRound.holes.find(h => h.holeNumber === number) || null;
    if (data.checkExpected && JSON.stringify(current) !== JSON.stringify(data.expectedHole) &&
        JSON.stringify(holeValues(current)) !== JSON.stringify(holeValues(hole)))
      return json({ detail: 'This round changed in another session.' }, 409);
    targetRound.holes = targetRound.holes.filter(h => h.holeNumber !== number).concat(hole);
    targetRound.currentHole = number;
    return json(hole);
  }
  if (url.pathname.startsWith('/api/')) return json({ detail: 'Not available in this UI fixture.' }, 404);
  const file = path.resolve(webroot, `.${url.pathname}`);
  if (!file.startsWith(webroot + path.sep) || !fs.existsSync(file) || !fs.statSync(file).isFile()) return json({}, 404);
  const type = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css' }[path.extname(file)] || 'application/octet-stream';
  res.writeHead(200, { 'Content-Type': type });
  fs.createReadStream(file).pipe(res);
});
server.listen(4173, '127.0.0.1', () => process.stdout.write('UI fixture: http://127.0.0.1:4173/live-round.html?id=1\n'));
