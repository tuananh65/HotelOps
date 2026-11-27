import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { nextCommands } from '../src/compact/workflow.ts';

const compact = new URL('../src/compact/', import.meta.url);

test('cleaning workspace omits consumable and reusable material panels', async () => {
  const source = await readFile(new URL('room-material-verification.tsx', compact), 'utf8');
  const work = await readFile(new URL('work.tsx', compact), 'utf8');
  assert.doesNotMatch(work, /RoomMaterialVerification|ReusableInventory/);
  assert.doesNotMatch(source, /ActualReplenishedQuantity|actualReplenishedQuantity|<details/);
  assert.doesNotMatch(source, /usableQuantity|<input|<form|onReady/);
  assert.doesNotMatch(work, /materialReady/);
});

test('cleaning and rework in progress retain completion without a manual assessment action', () => {
  for (const kind of ['CLEANING', 'REWORK']) {
    const actions = nextCommands(kind, 'IN_PROGRESS', true, true, false);
    assert.ok(actions.includes('complete'), kind);
    assert.ok(!actions.some(action => /assess|material|suppl/i.test(action)), kind);
  }
});
