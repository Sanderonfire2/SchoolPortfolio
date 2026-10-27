// afbeelding-ontvanger.mjs — mini-HTTP-server: de browsertab op sites.google.com
// haalt elke afbeelding gelijk-oorspronkelijk op en POST-et base64 hierheen.
// GET  /status → aantal opgeslagen bestanden (ACAO:*)
// POST /opslag?naam=x.jpg body=base64 → bewaart uploads/projecten/x.jpg
// Elke opslag wordt ook gelogd in opslag-regels.txt → mapping is achteraf te lezen via GET /log
import http from 'http';
import fs from 'fs';
import path from 'path';

const ROOT = path.resolve(import.meta.dirname);
const UPDIR = path.join(ROOT, 'wwwroot', 'uploads', 'projecten');
fs.mkdirSync(UPDIR, { recursive: true });
const LOGPAD = path.join(ROOT, 'ontvanger-log.txt');

const server = http.createServer((req, res) => {
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'POST, GET, OPTIONS');
  const url = new URL(req.url, 'http://127.0.0.1');

  if (req.method === 'OPTIONS') { res.writeHead(204); res.end(''); return; }

  if (req.method === 'GET' && url.pathname === '/status') {
    const l = fs.readdirSync(UPDIR).filter(f => !f.endsWith('.FOUT'));
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ opgeslagen: l.length, bestanden: l }));
    return;
  }

  if (req.method === 'GET' && url.pathname === '/log') {
    res.writeHead(200, { 'Content-Type': 'text/plain' });
    res.end(fs.existsSync(LOGPAD) ? fs.readFileSync(LOGPAD, 'utf8') : '');
    return;
  }

  if (req.method === 'POST' && url.pathname === '/dag') {
    const delen = [];
    req.on('data', d => delen.push(d));
    req.on('end', () => {
      try {
        fs.writeFileSync(path.join(ROOT, 'gs-data.json'), Buffer.from(Buffer.concat(delen).toString('utf8'), 'base64'));
        res.writeHead(200); res.end('opgeslagen');
      } catch (e) { res.writeHead(500); res.end(e.message); }
    });
    return;
  }

  if (req.method === 'POST' && url.pathname === '/opslag') {
    const naam = (url.searchParams.get('naam') || '').replace(/[^a-zA-Z0-9._-]/gi, '');
    if (!naam) { res.writeHead(400); res.end('naam ontbreekt'); return; }
    const delen = [];
    req.on('data', d => delen.push(d));
    req.on('end', () => {
      try {
        const buf = Buffer.from(Buffer.concat(delen).toString('utf8'), 'base64');
        if (buf.length < 500) throw new Error('te klein: ' + buf.length + ' bytes');
        fs.writeFileSync(path.join(UPDIR, naam), buf);
        fs.appendFileSync(LOGPAD, naam + '\n');
        res.writeHead(200); res.end('ok');
      } catch (e) {
        console.log('fout', naam, e.message);
        fs.writeFileSync(path.join(UPDIR, naam + '.FOUT'), 'fout: ' + e.message + '\n');
        res.writeHead(500); res.end(e.message);
      }
    });
    return;
  }
  res.writeHead(404); res.end();
});

server.listen(8377, '127.0.0.1', () => console.log('ontvanger luistert op 127.0.0.1:8377'));
