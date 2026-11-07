const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const { execFileSync } = require('node:child_process');
const root = path.resolve(__dirname, '../../..');
const trx = 'docs/fs013/evidence/fs013.trx';
const read = p => fs.readFileSync(path.join(root, p), 'utf8').replace(/^\uFEFF/, '');
const decode = v => v.replaceAll('&quot;', '"').replaceAll('&lt;', '<').replaceAll('&gt;', '>').replaceAll('&apos;', "'").replaceAll('&amp;', '&');
const attrs = s => Object.fromEntries([...s.matchAll(/(\w+)="([^"]*)"/g)].map(m => [m[1], decode(m[2])]));
const xml = read(trx), counters = attrs(xml.match(/<Counters\s+([^>]+)\/>/)[1]);
const results = [...xml.matchAll(/<UnitTestResult\s+([^>]+)>?/g)].map(m => attrs(m[1]));
const definitions = new Map([...xml.matchAll(/<UnitTest\s+([^>]+)>([\s\S]*?)<\/UnitTest>/g)].map(m => [attrs(m[1]).id, { test: attrs(m[1]), method: attrs(m[2].match(/<TestMethod\s+([^>]+)\/>/)[1]) }]));
const cases = [];
for (const result of results) {
  const definition = definitions.get(result.testId); if (!definition) throw Error('Missing source definition');
  const method = definition.method, test = { id: result.testId, name: result.testName };
  const cls = method.className.split('.').at(-1), file = `tests/HotelOps.Dev1.RegressionTests/${cls}.cs`;
  const source = read(file); if (!source.includes(method.name + '(')) throw Error('Executed source method missing: ' + file + ':' + method.name);
  const status = result.outcome === 'Passed' ? 'PASS' : result.outcome === 'NotExecuted' ? 'SKIP' : 'FAIL';
  cases.push({ testId: test.id, executionId: result.executionId, displayName: test.name, sourceFile: file, sourceMethod: method.name,
    sourceTextLFSHA256: crypto.createHash('sha256').update(source.replace(/\r\n/g, '\n')).digest('hex'), status,
    input: test.name.includes('(') ? test.name.slice(test.name.indexOf('(')) : 'See source arrangement',
    command: `dotnet test tests/HotelOps.Dev1.RegressionTests/HotelOps.Dev1.RegressionTests.csproj --filter FullyQualifiedName~${method.className}.${method.name}`,
    evidence: `${trx}#${result.executionId}`, level: 'UNIT_NOT_LIVE_SQL', scope: cls === 'CleaningExecutionTests' ? 'UC030/UC031 rules, command identity and DTO security' : 'Existing regression; see exact source assertions' });
}
if (cases.length !== Number(counters.total) || cases.filter(x => x.status === 'PASS').length !== Number(counters.passed)) throw Error('TRX inventory count mismatch');
const inventory = { checkpointAtInventory: execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8', windowsHide: true }).trim(),
  sourceIdentity: 'Per-file normalized source hashes bind working source; git checkpoint is stated separately', testCommand: `dotnet test tests/HotelOps.Dev1.RegressionTests/HotelOps.Dev1.RegressionTests.csproj --logger "trx;LogFileName=fs013.trx" --results-directory docs/fs013/evidence`,
  total: cases.length, passed: cases.filter(x => x.status === 'PASS').length, failed: cases.filter(x => x.status === 'FAIL').length, skipped: cases.filter(x => x.status === 'SKIP').length,
  distinctMethods: new Set(cases.map(x => x.sourceFile + ':' + x.sourceMethod)).size, coveragePercentage: 'NOT_MEASURED', testerStatus: 'NOT_ASSERTED_DEVELOPMENT_ONLY', cases };
fs.writeFileSync(path.join(root, 'docs/fs013/UNIT_TEST_INVENTORY.json'), JSON.stringify(inventory, null, 2) + '\n');
fs.writeFileSync(path.join(root, 'docs/fs013/UNIT_TEST_RESULTS.md'), '# Executed Unit Test inventory FS013\n\nDeveloper verification only; no Tester-ready/accepted claim or handover ZIP. Exact source methods, parameter inputs, PASS/FAIL/SKIP, repeat commands and TRX evidence are in UNIT_TEST_INVENTORY.json. Unit assertions do not substitute real HTTP/SQL/transaction evidence.\n\n| Total | PASS | FAIL | SKIP | Distinct methods |\n| --- | --- | --- | --- | --- |\n| '+[inventory.total, inventory.passed, inventory.failed, inventory.skipped, inventory.distinctMethods].join(' | ')+' |\n\nNo measured coverage percentage or automatic UC credit.\n');
console.log(JSON.stringify({ total: inventory.total, passed: inventory.passed, failed: inventory.failed, skipped: inventory.skipped, methods: inventory.distinctMethods }));
