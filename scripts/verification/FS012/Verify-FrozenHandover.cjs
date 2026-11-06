const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const Zip = require('../../../frontend/node_modules/jszip');
const ExcelJS = require('../../../frontend/node_modules/exceljs');
const root = path.resolve(__dirname, '../../..');
const hash = b => crypto.createHash('sha256').update(b).digest('hex');
const expected = '8e661796fe83b7b15c16d84f79547ce72e4cdd6272f7996ce2fc659003141974';
(async () => {
  const buffer = fs.readFileSync(path.join(root, 'docs/test-handover/FE-04/FE04_TESTER_HANDOVER.zip'));
  if (hash(buffer) !== expected) throw Error('Frozen ZIP SHA changed');
  const zip = await Zip.loadAsync(buffer), prefix = 'FE04_TESTER_HANDOVER/';
  const manifest = JSON.parse(await zip.file(prefix + 'PACKAGE_CONTENTS.json').async('string'));
  const source = JSON.parse(await zip.file(prefix + 'SOURCE_MANIFEST.json').async('string'));
  let cells = 0; const sheets = [], sourceChanges = [], mapping = {};
  const patterns = [/-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/, /\bgh[pousr]_[A-Za-z0-9]{25,}/, /\bAKIA[0-9A-Z]{16}\b/, /\beyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}/, /Password\s*=\s*[^;\s"'<>]{6,}/i];
  const files = Object.values(zip.files).filter(e => !e.dir);
  if (files.length !== 30 || manifest.files.length !== 29) throw Error('Inventory mismatch');
  for (const entry of files) {
    const relative = entry.name.slice(prefix.length);
    if (!entry.name.startsWith(prefix) || relative.includes('..') || /(^|\/)\.private(\/|$)|secrets\.json$|\.(dpapi|pem|pfx|key)$/i.test(relative)) throw Error('Forbidden archive path');
    const contents = await entry.async('nodebuffer');
    if (relative !== 'PACKAGE_CONTENTS.json' && hash(contents) !== manifest.files.find(f => f.path === relative)?.sha256) throw Error('ZIP manifest hash mismatch');
    const disk = fs.readFileSync(path.join(root, 'docs/test-handover/FE-04/FE04_TESTER_HANDOVER', relative));
    if (hash(disk) !== hash(contents)) throw Error('Frozen expanded package mismatch');
    if (relative.endsWith('.xlsx')) {
      const wb = new ExcelJS.Workbook(); await wb.xlsx.load(contents);
      for (const sheet of wb.worksheets) {
        sheet.eachRow(row => row.eachCell(cell => { cells++; if (patterns.some(p => p.test(String(cell.value ?? '')))) throw Error('Secret pattern in workbook'); }));
        sheets.push({ workbook: relative, sheet: sheet.name, rows: sheet.rowCount });
        const columns = sheet.getRow(1).values;
        if (relative === 'TESTER_TEST_CASES.xlsx') {
          const status = columns.indexOf('Tester execution status');
          if (sheet.rowCount !== 39 || status < 1) throw Error('Functional mapping shape changed');
          for (let r = 2; r <= sheet.rowCount; r++) if (sheet.getCell(r, status).value !== 'NOT_RUN') throw Error('Tester status changed');
          mapping.functionalCases = 38; mapping.testerStatuses = 'ALL_NOT_RUN';
        }
        if (relative === 'UNIT_TEST_CASES.xlsx') {
          const file = columns.indexOf('SourceTestFile'), method = columns.indexOf('TestMethod');
          const methods = new Set();
          for (let r = 2; r <= sheet.rowCount; r++) methods.add(String(sheet.getCell(r, file).value) + ':' + sheet.getCell(r, method).value);
          if (sheet.rowCount !== 76 || methods.size !== 34) throw Error('Unit mapping changed');
          mapping.unitCases = 75; mapping.unitMethods = methods.size;
        }
      }
    } else if (patterns.some(p => p.test(contents.toString('utf8')))) throw Error('Secret pattern in frozen text');
  }
  for (const entry of source.files) {
    const b = fs.readFileSync(path.join(root, entry.path));
    const normalized = entry.normalization === 'TEXT_LF' ? Buffer.from(b.toString('utf8').replace(/^\uFEFF/, '').replace(/\r\n/g, '\n')) : b;
    if (hash(normalized) !== entry.sha256) sourceChanges.push(entry.path);
  }
  const result = { result: 'PASS_FROZEN_BYTES_AND_CONTENTS', zipSHA256: expected, codeCommit: manifest.codeCommit, files: files.length, sourceFiles: source.files.length, sourceChangesSinceFS011: sourceChanges, mapping, sheets, xlsxCellsScanned: cells, secretScan: 'PASS_SCOPED_PATTERNS', testerExecution: 'TESTER_EXECUTION_PENDING', writesToFrozenPackage: false };
  fs.writeFileSync(path.join(root, 'docs/fs012/HANDOVER_FREEZE.json'), JSON.stringify(result, null, 2) + '\n'); console.log(JSON.stringify(result, null, 2));
})().catch(e => { console.error(e.message); process.exitCode = 1; });
