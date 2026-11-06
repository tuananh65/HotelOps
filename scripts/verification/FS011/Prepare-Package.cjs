const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { execFileSync } = require('node:child_process');
const root = path.resolve(__dirname, '../../..');
const destination = path.join(root, 'docs/test-handover/FE-04/FE04_TESTER_HANDOVER');
const hash = data => crypto.createHash('sha256').update(data).digest('hex');
const text = buffer => (buffer[0] === 255 && buffer[1] === 254 ? buffer.toString('utf16le') : buffer.toString('utf8')).replace(/^\uFEFF/, '').replace(/\r\n/g, '\n');
const checkpoint = execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8', windowsHide: true }).trim();
const paths = execFileSync('git', ['ls-files', '--', 'src/HotelOps.Api', 'frontend/src', 'frontend/package.json', 'frontend/package-lock.json', 'frontend/vite.config.ts', 'scripts/database/release', 'database/patches', 'scripts/verification/FS011'], { cwd: root, encoding: 'utf8', windowsHide: true }).trim().split(/\r?\n/).filter(Boolean);
const files = paths.map(file => {
  const buffer = fs.readFileSync(path.join(root, file));
  const normalize = file.endsWith('/.gitignore') || /\.(cs|csproj|json|sql|ts|tsx|ps1|cjs|mjs|md|props|targets|config|http|html|css|svg|xml|txt|yaml|yml|cshtml)$/.test(file);
  return { path: file, normalization: normalize ? 'TEXT_LF' : 'BINARY', sha256: hash(normalize ? text(buffer) : buffer) };
});
if (!files.some(file => file.path === 'scripts/verification/FS011/New-TesterTrial.ps1')) throw Error('Commit harness checkpoint before preparing package');
fs.writeFileSync(path.join(destination, 'CODE_COMMIT.txt'), checkpoint + '\n');
fs.writeFileSync(path.join(destination, 'SOURCE_MANIFEST.json'), JSON.stringify({ codeCommit: checkpoint, normalization: 'UTF BOM removed, CRLF->LF for text; raw bytes for binary', files }, null, 2) + '\n');
fs.copyFileSync(path.join(root, 'scripts/verification/FS011/Tester-Setup.ps1'), path.join(destination, 'SETUP_TESTER.ps1'));
fs.copyFileSync(path.join(root, 'scripts/verification/FS011/Open-TesterBrowser.cjs'), path.join(destination, 'Open-TesterBrowser.cjs'));
for (const file of ['COMMIT_FAULT_RESULTS.json', 'BOUNDARY_RESULTS.json', 'PHYSICAL_GUARD_RESULTS.json', 'PERMISSION_PROBE.json', 'TESTER_FAULT_GATE.json', 'TESTER_FUNCTIONAL_SMOKE.json', 'full01_LIVE_RESULTS.json', 'full02_LIVE_RESULTS.json', 'full03_LIVE_RESULTS.json', 'ACTUAL_RESTART_RESULTS.json', 'DEFAULT_OFF_RESULTS.json']) {
  const source = path.join(root, 'docs/fs011', file);
  if (!fs.existsSync(source)) throw Error('Required executed evidence missing: ' + file);
  const value = JSON.parse(text(fs.readFileSync(source)));
  fs.writeFileSync(path.join(destination, 'TEST_EVIDENCE', file), JSON.stringify(value, null, 2) + '\n');
}
const workbook = JSON.parse(fs.readFileSync(path.join(destination, 'TEST_EVIDENCE/WORKBOOK_VALIDATION.json'), 'utf8'));
for (const entry of workbook.checks) if (hash(fs.readFileSync(path.join(destination, entry.file))) !== entry.sha256) throw Error('Workbook no longer matches executed validation');
fs.writeFileSync(path.join(destination, 'EVIDENCE_REFERENCES.md'), '# Evidence references\n\nAll JSON/TRX evidence is included under TEST_EVIDENCE. Full01 failed11/12 (Monitor link assertion before data readiness); full02/full03 each have independent full-run outcomes. Provider commit-fault4cases use actual SQL with auth doubles. External assignment bypass is expected residual risk, not complete C01 PASS. Testers have not executed these results.\n\nExact source checkpoint: '+checkpoint+'. Runtime/unit checkpoint is recorded in UNIT_TEST_REPORT and unit workbook. Later documentation-only descendant may be used only after all source hashes match.\n');
const entries = [];
function walk(directory) { for (const name of fs.readdirSync(directory)) { const file = path.join(directory, name); if (fs.statSync(file).isDirectory()) walk(file); else if (name !== 'PACKAGE_CONTENTS.json') entries.push({ path: path.relative(destination, file).replaceAll('\\', '/'), sha256: hash(fs.readFileSync(file)) }); } }
walk(destination);
fs.writeFileSync(path.join(destination, 'PACKAGE_CONTENTS.json'), JSON.stringify({ codeCommit: checkpoint, excludesSelf: true, files: entries }, null, 2) + '\n');
console.log(JSON.stringify({ codeCommit: checkpoint, sourceFiles: files.length, packageFiles: entries.length + 1, workbooks: 'HASHES_VERIFIED' }));
