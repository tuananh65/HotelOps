const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const { execFileSync } = require('node:child_process');
const ExcelJS = require('../../../frontend/node_modules/exceljs'), Zip = require('../../../frontend/node_modules/jszip');
const root = path.resolve(__dirname, '../../..');
const output = path.join(root, 'docs/test-handover/FE-04/FE04_RETEST_FS012');
const archive = output + '.zip';
const baseline = path.join(root, 'docs/test-handover/FE-04/FE04_TESTER_HANDOVER');
const read = p => fs.readFileSync(path.join(root, p), 'utf8').replace(/^\uFEFF/, '');
const hash = b => crypto.createHash('sha256').update(b).digest('hex');
const git = args => execFileSync('git', args, { cwd: root, encoding: 'utf8', windowsHide: true }).trim();
const decode = v => v.replaceAll('&quot;', '"').replaceAll('&lt;', '<').replaceAll('&gt;', '>').replaceAll('&apos;', "'").replaceAll('&amp;', '&');
const attrs = v => Object.fromEntries([...v.matchAll(/(\w+)="([^"]*)"/g)].map(m => [m[1], decode(m[2])]));
const patterns = [/-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/, /\bgh[pousr]_[A-Za-z0-9]{25,}/, /\bAKIA[0-9A-Z]{16}\b/, /\beyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}/, /Password\s*=\s*[^;\s"'<>]{6,}/i];
async function workbook(file, rows) {
  const wb = new ExcelJS.Workbook(), sheet = wb.addWorksheet('Cases');
  sheet.columns = Object.keys(rows[0]).map(k => ({ header: k, key: k, width: 38 }));
  rows.forEach(r => sheet.addRow(r)); sheet.getRow(1).font = { bold: true };
  sheet.eachRow(r => { r.alignment = { vertical: 'top', wrapText: true }; });
  await wb.xlsx.writeFile(path.join(output, file));
  const check = new ExcelJS.Workbook(); await check.xlsx.readFile(path.join(output, file));
  if (check.worksheets[0].rowCount !== rows.length + 1) throw Error('Workbook roundtrip row count');
  for (let r = 0; r < rows.length; r++) for (let c = 0; c < Object.keys(rows[0]).length; c++)
    if ((check.worksheets[0].getCell(r + 2, c + 1).value ?? '') !== Object.values(rows[r])[c]) throw Error('Workbook roundtrip cell value');
}
(async () => {
  if (fs.existsSync(output) || fs.existsSync(archive)) throw Error('Retest version exists; never overwrite');
  const frozenZip = await Zip.loadAsync(fs.readFileSync(baseline + '.zip'));
  if (hash(fs.readFileSync(baseline + '.zip')) !== '8e661796fe83b7b15c16d84f79547ce72e4cdd6272f7996ce2fc659003141974') throw Error('Frozen ZIP changed');
  const frozenFunctional = await frozenZip.file('FE04_TESTER_HANDOVER/TESTER_TEST_CASES.xlsx').async('nodebuffer');
  if (hash(frozenFunctional) !== hash(fs.readFileSync(path.join(baseline, 'TESTER_TEST_CASES.xlsx')))) throw Error('Frozen functional reference differs from authoritative ZIP');
  const commit = git(['rev-parse', 'HEAD']);
  // Code must already be committed. Documentation/package files may still be in progress.
  const sourcePaths = git(['ls-files', '-z']).split('\0').filter(p => p && !p.startsWith('docs/'));
  const dirty = git(['diff', '--name-only', 'HEAD']).split('\n').filter(p => p && !p.startsWith('docs/'));
  const untracked = git(['ls-files', '--others', '--exclude-standard', '-z']).split('\0').filter(p => p && !p.startsWith('docs/'));
  if (dirty.length || untracked.length) throw Error('Commit executable source before packaging');
  const sourceManifest = { codeCommit: commit, scope: 'All tracked non-docs files; manifest only, no source/credentials embedded', files: sourcePaths.map(p => {
    const b = fs.readFileSync(path.join(root, p));
    const text = !b.includes(0) && /\.(cs|csproj|slnx|json|md|ts|tsx|mjs|cjs|js|html|css|sql|ps1|http|yml|yaml|txt|xml|config|props|targets|svg)$|(^|\/)\.[^/]+$/.test(p);
    return { path: p, normalization: text ? 'TEXT_LF' : 'RAW', sha256: hash(text ? Buffer.from(b.toString('utf8').replace(/^\uFEFF/, '').replace(/\r\n/g, '\n')) : b) };
  }) };
  const xml = read('docs/fs012/evidence/fs012.trx');
  const counters = attrs(xml.match(/<Counters\s+([^>]+)\/>/)[1]);
  if (Number(counters.total) !== 137 || Number(counters.executed) !== 137 || Number(counters.passed) !== 137 || Number(counters.failed) !== 0 || Number(counters.notExecuted) !== 0) throw Error('Full TRX suite must be137/137 executed and passed');
  const results = new Map([...xml.matchAll(/<UnitTestResult\s+([^>]+)>?/g)].map(m => { const a = attrs(m[1]); return [a.testId, a]; }));
  const selected = [];
  for (const match of xml.matchAll(/<UnitTest\s+([^>]+)>([\s\S]*?)<\/UnitTest>/g)) {
    const test = attrs(match[1]), method = attrs(match[2].match(/<TestMethod\s+([^>]+)\/>/)[1]);
    const cls = method.className.split('.').at(-1);
    if (!['CleaningCreationTests', 'CleaningLifecycleContractTests', 'NotificationCandidateTests', 'CandidateCommitGuardTests', 'CandidateCommandGuardTests', 'AuthorizationTests', 'CleaningMonitorRouteTests'].includes(cls) && !(cls === 'BoundaryTests' && /Monitor|Timeline|timeline|History/.test(method.name))) continue;
    const source = `tests/HotelOps.Dev1.RegressionTests/${cls}.cs`;
    if (!read(source).includes(method.name + '(')) throw Error('Mapped source method missing');
    selected.push({ CaseId: `UT-FS012-${String(selected.length + 1).padStart(3, '0')}`, SourceTestFile: source, TestMethod: method.name,
      Input: test.name.includes('(') ? test.name.slice(test.name.indexOf('(')) : 'See source method arrangement (no parameterized input).',
      DEVOutcome: results.get(test.id)?.outcome ?? 'MISSING', Evidence: 'TEST_EVIDENCE/fs012.trx#' + test.id, CodeCommit: commit });
  }
  const old = new ExcelJS.Workbook(); await old.xlsx.readFile(path.join(baseline, 'UNIT_TEST_CASES.xlsx'));
  const sheet = old.worksheets[0], headers = sheet.getRow(1).values;
  const col = key => headers.indexOf(key);
  for (let r = 2; r <= sheet.rowCount; r++) {
    const file = String(sheet.getCell(r, col('SourceTestFile')).value), method = String(sheet.getCell(r, col('TestMethod')).value), input = String(sheet.getCell(r, col('Input')).value);
    if (!selected.some(x => x.SourceTestFile === file && x.TestMethod === method && x.Input === input)) throw Error('Frozen FS011 unit case missing from executed FS012 mapping');
  }
  const methods = new Set(selected.map(x => x.SourceTestFile + ':' + x.TestMethod)).size;
  if (sheet.rowCount - 1 !== 75 || selected.length !== 77 || methods !== 34 || selected.some(x => x.DEVOutcome !== 'Passed')) throw Error('Unexpected executed mapping');
  fs.mkdirSync(path.join(output, 'TEST_EVIDENCE'), { recursive: true });
  fs.writeFileSync(path.join(output, 'CODE_COMMIT.txt'), commit + '\n');
  fs.writeFileSync(path.join(output, 'SOURCE_MANIFEST.json'), JSON.stringify(sourceManifest, null, 2) + '\n');
  await workbook('UNIT_TEST_CASES_FS012.xlsx', selected);
  // Frozen functional workbook is a labeled reference copy, never rewritten or presented as a new execution.
  fs.copyFileSync(path.join(baseline, 'TESTER_TEST_CASES.xlsx'), path.join(output, 'FS011_FUNCTIONAL_CASES_REFERENCE.xlsx'));
  const titles = [
    ['FT-FE04-001..012', 'UC028 Create/options/cycle/readiness/audit and navigation', 'Retain frozen per-case restrictions. FT010 audit fault and FT011 block fixture are DEVELOPER_ONLY evidence review on scratch; remaining functional cases use unused active synthetic room. Never run fault/block helper on Tester DB.', 'Atomic Create; no duplicate cycle; existing statuses unchanged.'],
    ['FT-FE04-013..019', 'UC029 validation/security and SQL module assignment', 'Use FS012 developer trial; approved synthetic admin/assignee; original ETag and new GUID.', 'One complete five-record bundle; invalid command/security denied.'],
    ['FT-FE04-020..023', 'Replay, key conflict, restart and concurrency', 'Retain exact original key/body/tag; replay after owned process restart; compare row counts.', 'Same durable result; no duplicate records; conflicting inputs rejected.'],
    ['FT-FE04-024,027..030', 'Restricted SQL writer boundary and commit failures', 'DEVELOPER_ONLY: review BOUNDARY_ATTEMPT07/COMMIT_FAULT/PHYSICAL receipts; scratch trial only.', 'Direct DML denied; missing proof/rollback zero records; privileged bypass remains disclosed.'],
    ['FT-FE04-025..026,031..036', 'Recipient authorization, retry and source reads', 'Retain frozen per-case DEVELOPER_ONLY restrictions. Revoke/restore/inactive/supersede/inbox-fault/claim mutations are developer scratch evidence review only. Use publication-scoped receipts, never mutate Tester DB or consume historical global queue.', 'No unauthorized reads/inbox; failed delivery retries without losing assignment.'],
    ['FT-FE04-014', 'Default OFF', 'Review actual default-off run; service restarted with DisableCandidate on FS012 scratch.', 'HTTP503; UI Save disabled; zero assignment-related mutation.'],
    ['FT-FE04-037', 'Accepted A01/A02', 'MISSING_EXECUTABLE_SOURCE: record BLOCKED only when Tester executes; no accepted signature available.', 'No shared/production activation; candidate remains provisional.'],
    ['FT-FE04-038', 'UC032/Monitor/Detail regression', 'Read/save items at100%, original/stale ETag, wrong owner/branch; navigate Monitor/Detail.', 'IN_PROGRESS retained; ownership and ETag enforced; correct navigation.'],
  ].map((r, i) => ({ RetestId: `RT-FS012-${String(i + 1).padStart(2, '0')}`, AffectedBaselineCases: r[0], Objective: r[1], Procedure: r[2], ExpectedResult: r[3], ActualResult: '', TesterStatus: 'NOT_RUN', CodeCommit: commit }));
  await workbook('FS012_RETEST_CASES.xlsx', titles);
  for (const file of ['BOUNDARY_ATTEMPT01.json','BOUNDARY_ATTEMPT02.json','BOUNDARY_ATTEMPT03.json','BOUNDARY_ATTEMPT04.json','BOUNDARY_ATTEMPT05.json','BOUNDARY_ATTEMPT06.json','BOUNDARY_ATTEMPT07.json','COMMIT_FAULT_RESULTS.json','PHYSICAL_GUARD_RESULTS.json','full01_LIVE_RESULTS.json','full02_LIVE_RESULTS.json','ACTUAL_RESTART_RESULTS.json','defaultoff_DEFAULT_OFF_RESULTS.json','HANDOVER_FREEZE.json','SQL_BOUNDARY_INVENTORY.json']) {
    const data = JSON.parse(read('docs/fs012/' + file));
    if (file === 'SQL_BOUNDARY_INVENTORY.json' && (data.databaseName !== 'HotelOps_Canonical_20261106_f120001_Fresh' || data.residualFaultConstraintCount !== 0 || !Array.isArray(data.effectiveDboProcedureExecute))) throw Error('Final SQL inventory incomplete');
    fs.copyFileSync(path.join(root, 'docs/fs012', file), path.join(output, 'TEST_EVIDENCE', file));
  }
  fs.copyFileSync(path.join(root, 'docs/fs012/evidence/fs012.trx'), path.join(output, 'TEST_EVIDENCE/fs012.trx'));
  for (const file of ['SQL_WRITE_BOUNDARY.md','EXTERNAL_WRITER_TESTS.md','TESTER_EXECUTION_SUPPORT.md','A01_A02_RECONCILIATION.md','REGRESSION_RESULTS.md','MERGE_READINESS.md']) fs.copyFileSync(path.join(root, 'docs/fs012', file), path.join(output, file));
  fs.writeFileSync(path.join(output, 'README.md'), `# FS012 versioned developer retest addendum\n\nCode checkpoint: ${commit}. Not a replacement for FS011 Tester handover, not accepted A01/A02 and not a deployed least-privilege login package.\n\nFS011 frozen ZIP SHA256: 8e661796fe83b7b15c16d84f79547ce72e4cdd6272f7996ce2fc659003141974. Its 38-case functional workbook is included unchanged as FS011_FUNCTIONAL_CASES_REFERENCE.xlsx; its commit/environment remain FS011. FS012_RETEST_CASES.xlsx contains8 affected-case groups, all NOT_RUN; Tester owns actuals/status. Current executed unit mapping is77 cases/34 methods,137/137 suite; all original75 cases/34 methods found with exact source method and parameter input.\n\nReproduce DEV only on a NEW exact FS012 trial: build backend/helper, New-ApprovedTrial -ExecuteApprovedCreate -AllowLocalSqlCertificateException, helper seed --approved-seed --local-sql-certificate-exception, Install-Candidate, then owned Start-LiveEnvironment and Run-LiveSuite. Existing target refuses overwrite; reuse only owned start/stop and append capacity if needed. Fault proofs are DEVELOPER_ONLY, never on FS011 Tester/shared/historical DB. Source manifest covers tracked non-docs files including solution/config/harness dependencies; checkout CODE_COMMIT, validate hashes, then build. Local SQL connection is supplied by owned child environment, never committed.\n\nAPI live uses the developer Windows identity. Restricted-user tests use NOLOGIN user impersonation and nonpooled explicitly reverted connections. Privileged writes remain outside boundary. Tester execution pending; full FE04 not merge-ready. See included scope/limitations and failed attempt receipts.\n`);
  const inventory = [];
  function visit(dir, rel = '') { for (const entry of fs.readdirSync(dir, { withFileTypes: true })) { const p = rel ? rel + '/' + entry.name : entry.name; if (entry.isDirectory()) visit(path.join(dir, entry.name), p); else inventory.push({ path: p, sha256: hash(fs.readFileSync(path.join(dir, entry.name))) }); } }
  visit(output);
  fs.writeFileSync(path.join(output, 'PACKAGE_CONTENTS.json'), JSON.stringify({ codeCommit: commit, files: inventory }, null, 2) + '\n');
  inventory.push({ path: 'PACKAGE_CONTENTS.json', sha256: hash(fs.readFileSync(path.join(output, 'PACKAGE_CONTENTS.json'))) });
  const zip = new Zip(); let cells = 0;
  for (const file of inventory) {
    if (file.path.includes('..') || /(^|\/)\.private(\/|$)|secrets\.json$|\.(dpapi|pem|pfx|key)$/i.test(file.path)) throw Error('Forbidden archive path');
    const b = fs.readFileSync(path.join(output, file.path));
    if (file.path.endsWith('.xlsx')) { const w = new ExcelJS.Workbook(); await w.xlsx.load(b); w.eachSheet(s => s.eachRow(r => r.eachCell(c => { cells++; if (patterns.some(p => p.test(String(c.value ?? '')))) throw Error('Secret pattern in workbook'); }))); }
    else if (patterns.some(p => p.test(b.toString('utf8')))) throw Error('Secret pattern in text');
    zip.file('FE04_RETEST_FS012/' + file.path, b);
  }
  fs.writeFileSync(archive, await zip.generateAsync({ type: 'nodebuffer', compression: 'DEFLATE' }));
  const check = await Zip.loadAsync(fs.readFileSync(archive));
  if (Object.values(check.files).filter(x => !x.dir).length !== inventory.length) throw Error('Archive count mismatch');
  for (const f of inventory) if (hash(await check.file('FE04_RETEST_FS012/' + f.path).async('nodebuffer')) !== f.sha256) throw Error('Archive hash mismatch');
  const result = { codeCommit: commit, zipSHA256: hash(fs.readFileSync(archive)), files: inventory.length, sourceFiles: sourcePaths.length, unitCases: selected.length, unitMethods: methods, preservedFS011UnitCases:75, baselineFunctionalCases:38, retestGroups:8, testerStatus:'NOT_RUN', testerExecution:'TESTER_EXECUTION_PENDING', cellsScanned:cells, secretScan:'PASS_SCOPED_PATTERNS', archiveRoundtrip:'PASS', noPrivateArtifacts:true };
  fs.writeFileSync(path.join(root, 'docs/fs012/RETEST_PACKAGE_VALIDATION.json'), JSON.stringify(result, null, 2) + '\n'); console.log(JSON.stringify(result, null, 2));
})().catch(e => { console.error(e.message); process.exitCode = 1; });
