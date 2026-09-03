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
const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, 'http://127.0.0.1');
  const json = (value, status = 200) => { res.writeHead(status, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' }); res.end(JSON.stringify(value)); };
  if (url.pathname === '/api/auth/me') return json({ id: 99991, displayName: 'Local smoke test', email: 'smoke@example.test' });
  if (url.pathname === '/api/security/csrf') return json({ token: 'local-fixture-token' });
  if (url.pathname === '/api/rounds/1' && req.method === 'GET') return json(round);
  if (url.pathname === '/api/courses/1') return json(course);
  if (url.pathname === '/api/courses') return json([course]);
  const match = url.pathname.match(/^\/api\/rounds\/1\/holes\/by-number\/(\d+)$/);
  if (match && req.method === 'PUT') {
    let body = '';
    for await (const chunk of req) body += chunk;
    const data = JSON.parse(body);
    const number = Number(match[1]);
    const hole = { id: number, holeNumber: number, par: data.par, score: data.score,
      putts: data.putts, gir: data.gir, fairwayHit: data.fairwayHit, penalty: data.penalty };
    round.holes = round.holes.filter(h => h.holeNumber !== number).concat(hole);
    round.currentHole = number;
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
