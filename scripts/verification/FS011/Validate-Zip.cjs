const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const Zip = require('../../../frontend/node_modules/jszip');
const ExcelJS = require('../../../frontend/node_modules/exceljs');
const root = path.resolve(__dirname, '../../..');
const directory = path.join(root, 'docs/test-handover/FE-04/FE04_TESTER_HANDOVER');
const output = path.join(root, 'docs/test-handover/FE-04/FE04_TESTER_HANDOVER.zip');
const hash = data => crypto.createHash('sha256').update(data).digest('hex');
const patterns = [/-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/, /\bgh[pousr]_[A-Za-z0-9]{25,}/, /\bAKIA[0-9A-Z]{16}\b/, /\beyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}/, /Password\s*=\s*[^;\s"'<>]{6,}/i];
(async () => {
  const manifest = JSON.parse(fs.readFileSync(path.join(directory, 'PACKAGE_CONTENTS.json'), 'utf8'));
  const files = [...manifest.files, { path: 'PACKAGE_CONTENTS.json', sha256: hash(fs.readFileSync(path.join(directory, 'PACKAGE_CONTENTS.json'))) }];
  const zip = new Zip(); let cells = 0;
  for (const file of files) {
    if (/(^|\/)\.private(\/|$)|secrets\.json$|\.(dpapi|pem|pfx|key)$/i.test(file.path) || file.path.includes('..')) throw Error('Forbidden archive path');
    const buffer = fs.readFileSync(path.join(directory, file.path));
    if (hash(buffer) !== file.sha256) throw Error('Manifest content changed: ' + file.path);
    if (file.path.endsWith('.xlsx')) {
      const workbook = new ExcelJS.Workbook(); await workbook.xlsx.load(buffer);
      for (const sheet of workbook.worksheets) sheet.eachRow(row => row.eachCell(cell => { cells++; if (patterns.some(pattern => pattern.test(String(cell.value ?? '')))) throw Error('Secret pattern in workbook'); }));
    } else if (patterns.some(pattern => pattern.test(buffer.toString('utf8')))) throw Error('Secret pattern in text: ' + file.path);
    zip.file('FE04_TESTER_HANDOVER/' + file.path, buffer);
  }
  await fs.promises.writeFile(output, await zip.generateAsync({ type: 'nodebuffer', compression: 'DEFLATE' }));
  const reopened = await Zip.loadAsync(fs.readFileSync(output));
  const entries = Object.values(reopened.files).filter(entry => !entry.dir);
  if (entries.length !== files.length) throw Error('ZIP file count mismatch');
  for (const file of files) {
    const entry = reopened.file('FE04_TESTER_HANDOVER/' + file.path);
    if (!entry || hash(await entry.async('nodebuffer')) !== file.sha256) throw Error('ZIP roundtrip mismatch');
  }
  const result = { zip: 'docs/test-handover/FE-04/FE04_TESTER_HANDOVER.zip', codeCommit: manifest.codeCommit, files: files.length, xlsxCellsScanned: cells, manifestAndZipRoundtrip: 'PASS', secretPatternScan: 'PASS_SCOPED_NOT_UNIVERSAL', privateArtifacts: 0, zipSHA256: hash(fs.readFileSync(output)), testerAcceptance: 'NOT_RECEIVED' };
  fs.writeFileSync(path.join(root, 'docs/fs011/ZIP_VALIDATION.json'), JSON.stringify(result, null, 2) + '\n');
  console.log(JSON.stringify(result, null, 2));
})().catch(error => { console.error(error.message); process.exitCode = 1; });
