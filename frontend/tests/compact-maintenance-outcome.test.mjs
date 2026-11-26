import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import ts from 'typescript';

const source = await readFile(new URL('../src/compact/workflow.ts', import.meta.url), 'utf8');
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext } }).outputText;
const { nextCommands, permission } = await import(`data:text/javascript;base64,${Buffer.from(compiled).toString('base64')}`);

test('assigned maintenance worker has two outcomes only while executing', () => {
  const actions = nextCommands('MAINTENANCE', 'IN_PROGRESS', true, true, false);
  assert.ok(actions.includes('complete'));
  assert.ok(actions.includes('unrepairable'));
  assert.equal(nextCommands('MAINTENANCE', 'PENDING', true, false, false).includes('unrepairable'), false);
  assert.equal(nextCommands('MAINTENANCE', 'IN_PROGRESS', false, false, false).includes('unrepairable'), false);
  assert.equal(nextCommands('CLEANING', 'IN_PROGRESS', true, true, false).includes('unrepairable'), false);
  assert.equal(permission('MAINTENANCE', 'unrepairable'), 'MAINTENANCE_EXECUTE');
});

test('maintenance completion no longer offers a lead verification command', () => {
  assert.equal(nextCommands('MAINTENANCE', 'COMPLETED', false, false, false).includes('verify'), false);
  assert.equal(nextCommands('OPERATIONAL', 'PENDING_VERIFICATION', false, false, false).includes('verify'), true);
});
