const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const { execFileSync } = require('node:child_process');
const root = path.resolve(__dirname, '../../..');
const files = [...new Set(execFileSync('git', ['ls-files', '-mo', '--exclude-standard', '-z'], { cwd: root, encoding: 'utf8', windowsHide: true }).split('\0').filter(Boolean))];
const patterns = [/-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/, /\bgh[pousr]_[A-Za-z0-9]{25,}/, /\bAKIA[0-9A-Z]{16}\b/, /\beyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}/, /Password\s*=\s*[^;\s"'<>]{6,}/i];
const report = { scope: 'Changed/untracked nonignored text plus separately validated package workbook/archive scans; patterns only, not universal secret detection', files: [], failures: [], privateArtifacts: [], result: 'PASS_SCOPED_PATTERNS' };
for (const p of files) {
  if (/(^|\/)\.private(\/|$)|(^|\/)secrets\.json$|\.(dpapi|pem|pfx|key)$/i.test(p)) report.privateArtifacts.push(p);
  const full = path.join(root, p); if (!fs.existsSync(full) || !fs.statSync(full).isFile()) continue;
  const b = fs.readFileSync(full);
  if (/\.(zip|xlsx|png|jpg|jpeg|pdf|dll|pdb)$/i.test(p)) continue;
  const text = b[0] === 255 && b[1] === 254 ? b.subarray(2).toString('utf16le') : b.toString('utf8');
  if (patterns.some(pattern => pattern.test(text))) report.failures.push(p);
  report.files.push({ path: p, sha256: crypto.createHash('sha256').update(b).digest('hex') });
}
if (report.failures.length || report.privateArtifacts.length) report.result = 'FAIL';
fs.writeFileSync(path.join(root, 'docs/fs012/SECRET_SCAN.json'), JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify({ result: report.result, files: report.files.length, failures: report.failures, privateArtifacts: report.privateArtifacts }));
if (report.result === 'FAIL') process.exitCode = 1;
