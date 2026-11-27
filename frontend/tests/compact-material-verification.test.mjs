import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
const read = name => readFile(new URL(`../src/compact/${name}`, import.meta.url), 'utf8');
test('room materials are read-only and refresh availability after refill notifications', async () => {
  const source = await read('room-material-verification.tsx');
  assert.doesNotMatch(source, /<input|<form|onSubmit|usableQuantity|onReady|legacyCompletionAllowed/);
  assert.match(source, /supplies\/room-assessments/);
  assert.match(source, /compact:notification-change/);
  assert.match(source, /CONSUMABLE_REFILL_REQUEST/);
  assert.match(source, /line.shortageQuantity/);
});
test('completion remains photo validated without manual material readiness', async () => {
  const work = await read('work.tsx');
  const completion = await read('cleaning-completion-report.tsx');
  assert.doesNotMatch(work, /materialReady|setMaterialReady|onReady=/);
  assert.match(work, /CleaningCompletionReport/);
  assert.doesNotMatch(completion, /STAFF_SUPPLIES_INSUFFICIENT/);
  assert.match(completion, /attachmentIds: draft.current.map/);
  assert.doesNotMatch(completion, /CONSUMABLE_REFILL_REQUEST/);
});
test('workday removes obsolete reconciliation and keeps actual supplied balances', async () => {
  const workday = await read('workday.tsx');
  const balances = await read('shift-supply-balances.tsx');
  assert.doesNotMatch(workday, /chờ đối soát|Đối soát vật tư/);
  assert.match(workday, /Chờ tự động hoàn trả vật tư/);
  assert.match(workday, /sau 5 phút/);
  assert.match(balances, /item.initialIssued \+ item.additionalIssued/);
  for (const value of ['used', 'returned', 'remaining']) assert.ok(balances.includes(`supplyQuantity(item.${value})`));
  assert.match(balances, /row.assignmentId === activeId && row.status !== "CLOSED"/);
});

test('display distinguishes automatic remaining-to-use from historical recorded usage', async () => {
  const ts = (await import('typescript')).default;
  const source = await read('room-material-verification.tsx');
  const code = source.slice(source.indexOf('export function materialQuantity'), source.indexOf('export default function'));
  const js = ts.transpileModule(code, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext } }).outputText;
  const { materialQuantity } = await import(`data:text/javascript;base64,${Buffer.from(js).toString('base64')}`);
  assert.equal(materialQuantity({standardQuantity: 4, requiredQuantity: 0}, true), 4);
  assert.equal(materialQuantity({standardQuantity: 4, requiredQuantity: 1}, true, true), 1);
  assert.equal(materialQuantity({standardQuantity: 4, requiredQuantity: null}, true, true), null);
  assert.equal(materialQuantity({standardQuantity: 4, requiredQuantity: null}, false), 4);
  assert.equal(materialQuantity({standardQuantity: 4, requiredQuantity: 2}, false), 2);
});


test('after-shift continuation requires explicit backend capability', async () => {
  const work = await read('work.tsx');
  const guard = await read('shift-command-guard.tsx');
  const completion = await read('cleaning-completion-report.tsx');
  assert.ok(work.includes('canContinueCleaning === true'));
  assert.ok(work.includes('["complete", "resume"].includes(a)'));
  assert.ok(guard.includes('data-cleaning-continuation="true"'));
  assert.ok(completion.includes('canContinueAfterShift ? "true" : undefined'));
});
