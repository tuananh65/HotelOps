import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
const files = { '/': ['index.html', 'text/html'], '/index.html': ['index.html', 'text/html'], '/preview.css': ['preview.css', 'text/css'], '/preview.js': ['preview.js', 'text/javascript'], '/users.js': ['users.js','text/javascript'] };
createServer(async (req, res) => {
  const entry = files[new URL(req.url, 'http://127.0.0.1').pathname];
  if (!entry) { res.writeHead(404); res.end('Not found'); return; }
  try { const body = await readFile(new URL(entry[0], import.meta.url)); res.writeHead(200, { 'Content-Type': `${entry[1]}; charset=utf-8`, 'Cache-Control': 'no-store' }); res.end(body); }
  catch { res.writeHead(500); res.end('Preview unavailable'); }
}).listen(60335, '127.0.0.1', () => console.log('HotelOps user management preview: http://127.0.0.1:60335/'));
