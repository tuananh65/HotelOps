import { readFile } from 'node:fs/promises';
import { verifyTarget } from './p0-runtime-adapter.mjs';
const f = JSON.parse(await readFile('artifacts/p0-workflow/private/runtime-fixture.json','utf8'));
await verifyTarget(f);
const actor = f.actors['DVBV.supervisor'];
const response = await fetch(`${f.baseUrl}/api/v2/work?branchId=${actor.branchId}&kind=CLEANING&pageSize=100`, {
  headers: { Authorization: `Bearer ${actor.token}` }, signal: AbortSignal.timeout(120000),
});
if (!response.ok) throw Error(`TEST work probe HTTP ${response.status}`);
const rows = await response.json();
console.log(JSON.stringify((rows.items ?? rows).map(x => {
  const w = x.fields ?? x;
  return { id: w.workId, title: w.title, status: w.status, roomId: w.roomId };
}).filter(w => !['CLOSED','CANCELLED'].includes(w.status))));
