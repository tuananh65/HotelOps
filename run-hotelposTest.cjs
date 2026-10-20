const { spawn } = require('node:child_process');
const { existsSync } = require('node:fs');
const path = require('node:path');
const net = require('node:net');

const root = __dirname;
const localDotnet = path.resolve(root, '../integration-artifacts/dotnet/dotnet.exe');
const dotnet = process.env.HOTELOPS_DOTNET || (existsSync(localDotnet) ? localDotnet : 'dotnet');
const children = [];
let stopping = false;

function stop(code = 0) {
  if (stopping) return;
  stopping = true;
  for (const child of children) {
    if (!child.pid || child.exitCode !== null) continue;
    if (process.platform === 'win32') {
      spawn('taskkill', ['/PID', String(child.pid), '/T', '/F'], { windowsHide: true, stdio: 'ignore' });
    } else child.kill('SIGTERM');
  }
  setTimeout(() => process.exit(code), 1000);
}

function launch(label, command, args) {
  const child = spawn(command, args, { cwd: root, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  children.push(child);
  for (const stream of [child.stdout, child.stderr]) {
    stream.on('data', data => process.stdout.write(`[${label}] ${data}`));
  }
  child.on('error', error => { console.error(`[${label}] ${error.message}`); stop(1); });
  child.on('exit', code => {
    if (!stopping) { console.error(`[${label}] stopped (${code}).`); stop(code || 1); }
  });
}

async function checkPort(port) {
  await new Promise((resolve, reject) => {
    const server = net.createServer();
    server.once('error', () => reject(new Error(`Port ${port} is already in use. Stop the existing server first, or open https://localhost:5175/login.`)));
    server.listen(port, 'localhost', () => server.close(resolve));
  });
}

process.on('SIGINT', () => stop());
process.on('SIGTERM', () => stop());

(async () => {
  if (!existsSync(path.join(root, 'node_modules/vite/bin/vite.js'))) throw new Error('Run npm.cmd ci first.');
  for (const port of [7029, 5175]) await checkPort(port);
  console.log('HotelposTest: https://localhost:5175/login\nPress Ctrl+C to stop both servers.');
  launch('BE', dotnet, ['run', '--project', 'src/HotelOps.Api', '--no-build', '--launch-profile', 'https', '--urls', 'https://localhost:7029']);
  launch('FE', process.execPath, ['node_modules/vite/bin/vite.js', '--config', 'vite.hotelposTest.config.ts', '--port', '5175']);
})().catch(error => { console.error(error.message); stop(1); });
