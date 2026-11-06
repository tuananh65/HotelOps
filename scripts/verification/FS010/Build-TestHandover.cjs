const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { execFileSync } = require('node:child_process');
const ExcelJS = require('../../../frontend/node_modules/exceljs');
const root = path.resolve(__dirname, '../../..');
const output = path.join(root, 'docs/test-handover/FE-04');
const read = (file) => fs.readFileSync(path.join(root, file), 'utf8').replace(/^\uFEFF/, '');
const sha = execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8', windowsHide: true }).trim();
const trxPath = 'docs/test-handover/FE-04/TEST_EVIDENCE/fs010.trx';
const xml = read(trxPath);
const decode = (v) => v.replaceAll('&quot;', '"').replaceAll('&lt;', '<').replaceAll('&gt;', '>').replaceAll('&apos;', "'").replaceAll('&amp;', '&');
const attrs = (v) => Object.fromEntries([...v.matchAll(/(\w+)="([^"]*)"/g)].map((m) => [m[1], decode(m[2])]));
const results = new Map([...xml.matchAll(/<UnitTestResult\s+([^>]+)>?/g)].map((m) => { const a = attrs(m[1]); return [a.testId, a]; }));
const selected = [];
for (const match of xml.matchAll(/<UnitTest\s+([^>]+)>([\s\S]*?)<\/UnitTest>/g)) {
  const test = attrs(match[1]);
  const method = attrs(match[2].match(/<TestMethod\s+([^>]+)\/>/)[1]);
  const cls = method.className.split('.').at(-1);
  const source = `tests/HotelOps.Dev1.RegressionTests/${cls}.cs`;
  const content = read(source);
  if (!content.includes(method.name + '(')) throw Error('Source method missing: ' + source + ':' + method.name);
  let uc;
  if (cls === 'CleaningCreationTests') uc = /Fingerprint|Assignment/.test(method.name) ? 'UC029' : 'UC028';
  else if (['CleaningLifecycleContractTests', 'NotificationCandidateTests', 'CandidateCommitGuardTests'].includes(cls)) uc = 'UC029';
  else if (cls === 'AuthorizationTests') uc = 'UC028/UC029 (shared authorization unit)';
  else if (cls === 'CleaningMonitorRouteTests') uc = 'UC037 (route regression)';
  else if (cls === 'BoundaryTests' && /Monitor|Timeline|timeline/.test(method.name)) uc = 'UC037';
  else if (cls === 'BoundaryTests' && /History/.test(method.name)) uc = 'UC038 (boundary only; controller inactive)';
  else continue;
  const outcome = results.get(test.id);
  if (!outcome) throw Error('Missing executed result ' + test.id);
  selected.push({ TestCaseId: `UT-FE04-${String(selected.length + 1).padStart(3, '0')}`, Module: 'FE-04', UC: uc,
    Requirement: uc.startsWith('UC028') ? 'UC028 PRE-01; GBR-003/004; shared authorization where labeled' : uc.startsWith('UC029') ? 'UC029 PRE-01; GBR-005/007/008/010/046; FS010 commit/replay contract' : 'Historical SRS boundary/route regression only',
    SourceTestFile: source, TestMethod: method.name, TestObjective: method.name.replaceAll('_', ' '),
    Preconditions: 'UNIT; no live database. See source setup and assertions; SQL collaborators are intentionally absent in validation tests.',
    Input: test.name.includes('(') ? test.name.slice(test.name.indexOf('(')) : 'See source method arrangement (no parameterized input).',
    ExpectedResult: 'All exact source assertions succeed: ' + method.name.replaceAll('_', ' '),
    ExecutionCommand: `dotnet test tests/HotelOps.Dev1.RegressionTests/HotelOps.Dev1.RegressionTests.csproj --filter FullyQualifiedName~${method.className}.${method.name}`,
    ActualResult: outcome.outcome, ExecutionStatus: outcome.outcome === 'Passed' ? 'PASS' : outcome.outcome.toUpperCase(),
    Evidence: `${trxPath}#${test.id}`, CommitSHA: sha });
}
// Explicit manual cases: no automatic test result is copied into Tester execution status.
const cases = [
 ['028','Valid Create + detail/monitor navigation','UI/API/SQL','Normal flow 1-8; GBR-007/008/043/044/045','Choose an unused active room in B1; inspect item preview; enter valid type/priority and due UTC +1h; save; refresh detail; search task in Monitor.','201; one pending-assignment task, one current-cycle pointer, one readiness event and create audit; detail persists after refresh; legacy room status unchanged.'],
 ['028','Missing/invalid identifiers','API','Step 4; GBR-003/004','POST create with roomId=0, then nonexistent type/priority; query SQL counts before/after each request.','400; no task/cycle/event/audit delta.'],
 ['028','Invalid deadline and unknown fields','UI/API','Step 4; GBR-003/004/014','Submit past UTC due, local time without Z, and an extra actorId/status field in separate commands.','400 or client validation prevents POST; no persistence; actor/status never client controlled.'],
 ['028','Anonymous Create','API','PRE-01; GBR-001/002','Use a fresh browser with no cookie; POST valid create command.','401; no rows created.'],
 ['028','Forbidden Create','UI/API','PRE-01; GBR-002','Login fs010.owner; obtain CSRF; attempt options and valid create for B1.','403; no disclosure of room configuration or write.'],
 ['028','Cross-branch Create','API/SQL','PRE-01; GBR-002','Login admin with B1 grant; send B2 branch route using a B1 room, then B2 room.','403 or authoritative reference rejection without foreign source data; no rows created.'],
 ['028','Missing CSRF','API','PRE-01; anti-forgery contract','Login admin; POST valid create without X-CSRF-TOKEN.','400; no persistence.'],
 ['028','Duplicate active task','UI/API/SQL','Exception 4.1; GBR-045','Create a task; revisit form for same room; attempt identical second create.','409 on duplicate write; form links existing task; only one open task.'],
 ['028','Concurrent Create','API/SQL','GBR-045; GBR-007/008','Obtain two valid CSRF tokens; send two create requests simultaneously for one unused room.','One 201 and one 409; one task/cycle/readiness/create-audit.'],
 ['028','Create audit rollback','API/SQL','GBR-007/008','Use isolated helper fault-on; POST create; always fault-off in finally; compare task/cycle/event/audit counts.','Write fails; all transaction counts unchanged; no partial task.'],
 ['028','Blocked room preview and Save and Assign','UI/API/SQL','GBR-043/044; alternative 8.1','Create a synthetic block using candidate-block-unused-room; preview items; Save and Assign; inspect SQL/readiness.','201; block stays effective even if expected release is past; legacy state unchanged; navigation uses actual created task.'],
 ['028','Inactive room','API/SQL','Exception 4.2','Provision a dedicated inactive synthetic room; submit valid create against it.','409/inactive; no task/cycle/readiness/audit created.'],
 ['029','Valid candidate Assign','UI/API/SQL','Normal flow 1-8; GBR-007/008/010/046','Open assignment for a new task; select eligible B1 assignee/on-shift; save with original strong ETag and GUID key; inspect five SQL record groups.','200; ASSIGNED task and one active assignment/audit/completed operation/publication/recipient intent; candidate contract explicitly provisional.'],
 ['029','Default-off fail closed','UI/API/SQL','GBR-010; FS010 default-off','Restart owned environment with DisableCandidate; open assignment and send valid POST with ETag/key.','503/unavailable; Save disabled by capability; no assignment/audit/operation/publication/intent delta.'],
 ['029','Invalid assignee','API/SQL','Exception 4.1; GBR-046','Assign to nonexistent/inactive/not-HK/not-on-shift staff using separate dedicated fixtures.','400 InvalidReference; no mutation/publication; invalid staff not offered in planning.'],
 ['029','Foreign branch/ownership','API','PRE-01; GBR-002/046','Use foreign branch route, then assign a B2 staff member from B1; query source as another housekeeper.','403 for foreign scope/read; invalid assignee rejected; no source data leaked.'],
 ['029','Anonymous/forbidden assignment','API/SQL','PRE-01; GBR-001/002','POST from anonymous context then fs010.owner with valid CSRF.','401 then 403; zero assignment-related deltas.'],
 ['029','Stale If-Match','API/SQL','GBR-005','Use stale 16-hex quoted ETag for a new assignment with new key.','412; no assignment/audit/publication/intent.'],
 ['029','Weak/missing ETag and invalid key','API','GBR-005; operation identity contract','Try W/ ETag, *, missing If-Match and non-GUID Idempotency-Key separately.','Request rejected by route validation (400/428 as contract); no mutation.'],
 ['029','Same key same command replay','API/SQL','FS010 durable replay','Assign once; replay exact same body, key and ORIGINAL If-Match, even though task ETag advanced.','200 with byte-equivalent durable business result; original revision preserved; counts remain one each.'],
 ['029','Same key different command','API/SQL','FS010 durable replay','Reuse successful key with changed assignee, reason, due or original If-Match, one at a time.','409; original durable result/records unchanged.'],
 ['029','Concurrent same-key replay','API/SQL','FS010 durable replay','POST exact same key/body/tag concurrently from two authorized contexts.','Both obtain identical successful result; one assignment/audit/operation/publication/intent.'],
 ['029','Concurrent competing commands','API/SQL','GBR-005/007','POST two new keys simultaneously for one pending task.','One success; competing write rejected stale/conflict; no duplicate active assignment.'],
 ['029','Staging failure rollback','API/SQL','GBR-007/008/010','Enable isolated candidate-stage-fault-on; assign; disable fault in finally; inspect counts; retry SAME original key/tag/body.','First mutation rolls back all five groups; retry succeeds once with mandatory durable publication.'],
 ['029','Permission revoked on replay','API/SQL','PRE-01; GBR-002','Assign; revoke actor grant using isolated helper; replay original command; restore fixture grant in finally.','403; durable replay does not bypass current permission; no extra records.'],
 ['029','Source inaccessible on replay','API/SQL','PRE-01; source security','Provision a dedicated completed operation; make source room inactive or remove actor branch access; replay original command.','404/403 according to source access; result not disclosed; no duplicate writes.'],
 ['029','Precommit missing audit/intent','COMPONENT/SQL','GBR-007/008/010; C01','Run candidate-physical-proof on isolated FS010; inspect raw-delete and ExecuteDelete cases.','EF CommitAsync rejected by persisted proof; rollback restores original rows.'],
 ['029','Sync commit, savepoint and cancellation guard','COMPONENT/SQL','C01','Run candidate-physical-proof; inspect sync rejection, savepoint restoration and canceled precommit cases.','Sync commit rejected; restoring mandatory rows via savepoint permits commit; canceled commit rejected.'],
 ['029','Commit acknowledgement loss and cancellation','COMPONENT/SQL','FS010 durable commit recovery','Run candidate-commit-proof; inspect before/after commit faults and cancellation; compare Assignment/Audit/Operation/Publication/Intent deltas.','Before: all zero and unavailable; after: all one and durable success; fresh-context retry exactly once. This is simulated provider fault, not network chaos acceptance.'],
 ['029','Raw provider bypass limitation','COMPONENT/SQL','C01 trust boundary','Run candidate-physical-proof; inspect raw-provider-bypass canary receipt.','Bypass is explicitly confirmed UNSUPPORTED; cannot approve complete C01/shared activation. Canary persists; no repair is used to force PASS.'],
 ['029','Delivery retry after commit','API/SQL','Exception 7.1; GBR-010','Assign; enable isolated inbox fault; deliver exact publication; clear fault in finally; set retry-now for that publication; redeliver.','Assignment remains; pending retry durable; one eventual inbox recipient; no duplicate inbox.'],
 ['029','Claim concurrency and worker restart','COMPONENT/SQL','GBR-010; candidate delivery','For one publication run two claim workers; stop after durable claim; expire its lease; restart explicit delivery helper.','One claim owner; restart resumes persisted lease/intent; exactly one inbox delivery.'],
 ['029','Recipient source-read revoked','API/SQL','GBR-002/010; source security','Assign; revoke recipient role; attempt explicit delivery; read inbox as recipient and other user. Restore isolated role after assertion.','SOURCE_READ_DENIED terminal/no inbox; unauthorized users receive no source payload.'],
 ['029','Recipient inactive/out-of-scope','COMPONENT/API/SQL','GBR-002/010/046','Use separate new publications; mark recipient inactive then out-of-branch; deliver selected publication; restore fixture in finally.','RECIPIENT_INACTIVE / RECIPIENT_OUT_OF_SCOPE terminal; no inbox row.'],
 ['029','Superseded assignment','COMPONENT/SQL','Source-read security','Create dedicated publication; end its assignment using isolated supersede helper; dispatch exact publication.','SUPERSEDED terminal, no inbox or stale source payload.'],
 ['029','Inbox recipient authorization','UI/API/SQL','GBR-002/010','Deliver one event; open assigned task as correct assignee, other housekeeper, and foreign-branch actor; check Monitor permission stays unchanged.','Only authorized recipient can read source; other/foreign denied; Monitor scope not widened.'],
 ['029','Accepted shared publisher compatibility','INTEGRATION','UC029 step 7; A01/A02','Obtain accepted source SHA/signatures from DEV2; reconcile one runtime publisher/DbContext; rerun assignment+delivery on approved integration environment.','BLOCKED until accepted executable source exists; candidate cannot establish production acceptance.'],
 ['029','UC032 and Monitor regression','UI/API/SQL','UC032/UC037 regression','Run full FS010 suite: write progress 100 using actual ETag; resend stale tag; try wrong owner; refresh detail and Monitor.','Task remains IN_PROGRESS; stale 412/wrong owner 403; no UC032 or Monitor behavior changes.'],
];
const tester = cases.map(([uc, title, level, requirement, steps, expected], i) => ({
  'Test Case ID': `FT-FE04-${String(i + 1).padStart(3, '0')}`, 'Module/UC': `FE-04/UC${uc}`, 'SRS requirement': requirement,
  Priority: /commit|rollback|authorization|revoked|Anonymous|Concurrent|Default-off|bypass/i.test(title) ? 'P0' : 'P1', 'Test level': level,
  Preconditions: `TEST_ENVIRONMENT.md; isolated FS010 only; ${uc === '029' && !/Default-off|Accepted/.test(title) ? 'candidate enabled (FS010_PROVISIONAL); ' : ''}fresh synthetic task/operation per case; counts baseline captured.`,
  'Detailed test steps': `1. Prepare/login per TEST_EXECUTION_GUIDE.md.\n2. ${steps}\n3. Capture sanitized API/UI/SQL evidence and actual counts; record actual result/status; file defect on mismatch.`,
  'Synthetic test data': 'fs010.admin / fs010.owner / fs010.other / fs010.denied; B1/B2 from private fixture; unused FS010-B1-* room; GUID operation key; UTC due +1h; no credentials in workbook.',
  'Expected result': expected, 'Actual result': '', 'Tester execution status': 'NOT_RUN', 'Evidence reference': '', 'Defect ID': '',
  Notes: title.includes('Accepted shared') ? 'REQUIRES_ACCEPTED_SHARED_INTEGRATION; MISSING_EXECUTABLE_SOURCE' : uc === '029' ? 'CANDIDATE_ONLY; PROVISIONAL_NOT_ACCEPTED; no production acceptance' : 'Historical SRS draft; validate against confirmed task scope.'
}));
async function workbook(file, sheetName, rows) {
  const book = new ExcelJS.Workbook(); book.creator = 'HotelOps DEV1';
  const sheet = book.addWorksheet(sheetName, { views: [{ state: 'frozen', ySplit: 1, xSplit: 1 }] });
  sheet.columns = Object.keys(rows[0]).map(key => ({ header: key, key, width: ['TestObjective', 'ExpectedResult', 'Detailed test steps', 'Expected result'].includes(key) ? 65 : 30 }));
  rows.forEach(row => sheet.addRow(row));
  sheet.autoFilter = { from: 'A1', to: { row: rows.length + 1, column: sheet.columnCount } };
  sheet.getRow(1).font = { bold: true, color: { argb: 'FFFFFFFF' } };
  sheet.getRow(1).fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF1E3A5F' } };
  sheet.eachRow(row => { row.alignment = { vertical: 'top', wrapText: true }; });
  await book.xlsx.writeFile(path.join(output, file));
  const verify = new ExcelJS.Workbook(); await verify.xlsx.readFile(path.join(output, file));
  const actual = verify.worksheets[0];
  if (actual.rowCount !== rows.length + 1 || actual.columnCount !== Object.keys(rows[0]).length) throw Error('Workbook shape mismatch');
  for (let r = 0; r < rows.length; r++) for (let c = 0; c < actual.columnCount; c++) {
    if ((actual.getCell(r + 2, c + 1).value ?? '') !== Object.values(rows[r])[c]) throw Error('Workbook content mismatch');
  }
  return { file, rows: rows.length, columns: actual.columnCount, roundTrip: 'PASS', sha256: crypto.createHash('sha256').update(fs.readFileSync(path.join(output, file))).digest('hex') };
}
(async () => {
  fs.mkdirSync(output, { recursive: true });
  const checks = [await workbook('UNIT_TEST_CASES.xlsx', 'Automated unit cases', selected), await workbook('TESTER_TEST_CASES.xlsx', 'Tester cases', tester)];
  const counter = attrs(xml.match(/<Counters\s+([^>]+)\/>/)[1]);
  const summary = { commitSHA: sha, testFramework: 'xUnit 2.9.3 / VSTest', executedTotal: Number(counter.total), passed: Number(counter.passed), failed: Number(counter.failed), skipped: Number(counter.notExecuted || 0), mappedUnitCases: selected.length, mappedDistinctMethods: new Set(selected.map(x => x.SourceTestFile + ':' + x.TestMethod)).size, testerCases: tester.length, testerStatus: 'NOT_RUN', coveragePercentage: 'NOT_MEASURED', checks };
  fs.writeFileSync(path.join(output, 'TEST_EVIDENCE/WORKBOOK_VALIDATION.json'), JSON.stringify(summary, null, 2) + '\n');
  console.log(JSON.stringify(summary, null, 2));
})();
