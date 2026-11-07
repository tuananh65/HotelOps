const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const root = path.resolve(__dirname, '../../..');
const logPath = 'docs/fs013/NODE_TESTS.log';
const bytes = fs.readFileSync(path.join(root, logPath));
const log = bytes.toString(bytes[0] === 255 && bytes[1] === 254 ? 'utf16le' : 'utf8').replace(/^\uFEFF/, '').replace(/\r\n/g, '\n');
const names = [...log.matchAll(/^\u2714 (.+) \([0-9.]+ms\)$/gm)].map(m => m[1]);
if (names.length !== 50 || !log.includes('\u2139 pass 50') || !log.includes('\u2139 fail 0') || !log.includes('\u2139 skipped 0')) throw Error('Executed Node result mismatch');
const hash = s => crypto.createHash('sha256').update(s).digest('hex');
const files = fs.readdirSync(path.join(root, 'frontend/tests')).filter(f => f.endsWith('.test.mjs')).map(f => ({
  sourceFile: 'frontend/tests/' + f, source: fs.readFileSync(path.join(root, 'frontend/tests', f), 'utf8').replace(/\r\n/g, '\n')
}));
const cases = names.map(name => {
  const matches = files.filter(f => f.source.includes(name));
  if (matches.length !== 1) throw Error('Ambiguous/missing executed source: ' + name);
  const f = matches[0];
  return { displayName: name, sourceFile: f.sourceFile, sourceMethod: 'node:test test("' + name + '")',
    sourceLine: f.source.slice(0, f.source.indexOf(name)).split('\n').length,
    sourceTextLFSHA256: hash(f.source), status: 'PASS', workingDirectory: 'frontend',
    command: 'node --test tests/' + path.basename(f.sourceFile), evidence: logPath,
    level: 'NODE_UNIT_OR_INTERCEPTED_TRANSPORT_NOT_REAL_API_SQL',
    coverageScope: f.sourceFile.endsWith('cleaning-execution.test.mjs') ? 'FS013 UC030/031 command/response contracts' : 'Existing regression; see source, includes prototype store units' };
});
fs.writeFileSync(path.join(root, 'docs/fs013/NODE_TEST_INVENTORY.json'), JSON.stringify({
  total: 50, passed: 50, failed: 0, skipped: 0, sourceMethods: 50, command: 'npm test', workingDirectory: 'frontend',
  evidenceTextLFSHA256: hash(log), coveragePercentage: 'NOT_MEASURED', testerAcceptance: false, cases
}, null, 2) + '\n');
console.log(JSON.stringify({ total: 50, passed: 50, sourceMethods: 50 }));
