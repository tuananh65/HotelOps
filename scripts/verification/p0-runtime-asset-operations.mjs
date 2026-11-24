// Supplemental A-scope runtime acceptance. Run sequentially after the P0 lifecycle suite.
// Creates marked TEST-only work/assets through real APIs. Never alters existing fixture assets.
import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {readFile, mkdir, writeFile} from 'node:fs/promises';
import {resolve, dirname} from 'node:path';
import {pathToFileURL} from 'node:url';
import {verifyTarget} from './p0-runtime-adapter.mjs';

const fields = value => value?.fields ?? value;
export async function run(fixturePath, outputPath) {
  assert(fixturePath, 'P0_RUNTIME_FIXTURE is required');
  const fixture = JSON.parse(await readFile(resolve(fixturePath), 'utf8'));
  const target = await verifyTarget(fixture);
  assert.equal(target.database, 'HotelOpsCompactWorkdayTestP0261124');
  assert.equal(new URL(target.baseUrl).origin, new URL(fixture.baseUrl).origin);
  assert(target.processId > 0 && Date.now() - Date.parse(target.verifiedAt) < 60000);
  const branch = fixture.branches.find(b => b.code === 'P0_TEST_DVBV');
  const foreign = fixture.branches.find(b => b.code === 'P0_TEST_DVCT');
  assert(branch && foreign && branch.rooms.length >= 2);
  const results = [], traces = [], created = [], stamp = randomUUID().slice(0, 8);
  let active, assertions = 0;
  const check = (condition, message) => { assertions++; assert(condition, message); };
  const equal = (actual, expected, message) => { assertions++; assert.deepEqual(actual, expected, message); };
  async function api(actor, method, path, body, statuses = [200]) {
    assert(path.startsWith('/api/v2/'));
    const url = new URL(path, fixture.baseUrl);
    assert.equal(url.origin, new URL(fixture.baseUrl).origin);
    const token = fixture.actors[actor]?.token; assert(token, `Missing actor ${actor}`);
    const response = await fetch(url, {method, redirect:'error', signal:AbortSignal.timeout(120000),
      headers:{Authorization:`Bearer ${token}`, 'Content-Type':'application/json'},
      body:body === undefined ? undefined : JSON.stringify(body)});
    traces.push({test:active, actor, method, path:url.pathname, status:response.status});
    const text = await response.text();
    let value; try { value = text ? JSON.parse(text) : null; } catch { throw new Error(`Non-JSON HTTP ${response.status}`); }
    check(statuses.includes(response.status), `Expected ${statuses.join('/')}, got ${response.status}: ${value?.code ?? value?.title ?? ''}`);
    return value;
  }
  const dashboard = () => api('DVBV.om','GET',`/api/v2/insights/dashboard?branchId=${branch.branchId}`);
  async function tile(code, id, expected) {
    const data = await dashboard(), group = data.actionCenter?.groups?.find(g => g.code === code);
    check(group, `Action center group ${code} missing`);
    equal(group.items.some(x => x.id === id), expected, `${code} must reflect actual record ${id}`);
    if (expected) check(group.count > 0, `${code} must be populated`);
  }
  async function scenario(name, test) {
    active = name; assertions = 0;
    try { await test(); results.push({name,status:'VERIFIED',assertions}); }
    catch(error) {
      const secrets = Object.values(fixture.actors).map(a => a.token).filter(Boolean);
      const message = secrets.reduce((s, token) => s.replaceAll(token,'[REDACTED]'), String(error.message));
      results.push({name,status:'FAILED',assertions,error:message});
    }
    await mkdir(dirname(resolve(outputPath)),{recursive:true});
    await writeFile(resolve(outputPath), JSON.stringify({at:new Date().toISOString(),database:target.database,
      processId:target.processId,baseUrl:target.baseUrl,results,traces,created,
      limitations:['Transfer and stocktake reports need their own populated workflow fixtures.',
        'No browser assertions; this script verifies real API behavior only.']},null,2));
  }
  await scenario('A-OM-OPERATIONAL-SELF-REVIEW', async () => {
    const body = {branchId:branch.branchId,kind:'OPERATIONAL',title:`P0 TEST OM self review ${stamp}`,
      description:'Synthetic TEST acceptance task',departmentId:fixture.actors['DVBV.om'].departmentId,
      dueAt:new Date(Date.now()+3600000).toISOString()};
    for (const actor of ['DVBV.owner','DVBV.admin','DVCT.om'])
      await api(actor,'POST','/api/v2/work',body,[403,404]);
    let work = fields(await api('DVBV.om','POST','/api/v2/work',body));
    created.push({kind:'OPERATIONAL',id:work.workId});
    equal(work.status,'PENDING_ASSIGNMENT');
    await tile('UNASSIGNED_TASKS',work.workId,true);
    const act = async (command, extra={}) => {
      work = fields(await api('DVBV.om','POST',`/api/v2/work/${work.workId}/actions/${command}`,
        {rowVersion:work.rowVersion,operationKey:randomUUID(),reason:'P0 TEST acceptance',...extra}));
    };
    await act('assign',{userId:fixture.actors['DVBV.om'].userId});
    await act('accept'); await act('start'); await act('complete');
    equal(work.status,'PENDING_VERIFICATION');
    await tile('TASKS_TO_VERIFY',work.workId,true);
    for (const actor of ['DVBV.owner','DVBV.admin','DVCT.om'])
      await api(actor,'POST',`/api/v2/work/${work.workId}/actions/verify`,
        {rowVersion:work.rowVersion,operationKey:randomUUID(),reason:'P0 TEST negative'},[403,404]);
    await act('verify'); equal(work.status,'CLOSED');
    equal(work.verifiedBy,fixture.actors['DVBV.om'].userId);
    const detail = await api('DVBV.om','GET',`/api/v2/work/${work.workId}`);
    check(detail.events.map(fields).some(e => e.eventType === 'OM_OPERATIONAL_SELF_REVIEW'),'Self-review audit event missing');
    check(detail.time.map(fields).some(t => t.closedAt != null && t.effectiveEndedAt != null),'Closed execution interval missing');
    await tile('TASKS_TO_VERIFY',work.workId,false);
    const report = await api('DVBV.om','GET',`/api/v2/insights/reports/tasks?branchId=${branch.branchId}`);
    check(report.some(w => w.workId === work.workId && w.status === 'CLOSED' && w.verifiedAt),'Populated task report must contain verified task');
    await api('DVCT.om','GET',`/api/v2/work/${work.workId}`,undefined,[403,404]);
  });
  await scenario('A-ASSET-CREATE-UPDATE-MOVE-HANDOVER', async () => {
    const catalog = `/api/v2/catalog/assets?branchId=${branch.branchId}`;
    const body = {fields:{assetCode:`P0_ACCEPT_${stamp}`,name:`P0 TEST asset ${stamp}`,
      roomId:branch.rooms[0].roomId,departmentId:fixture.actors['DVBV.om'].departmentId,conditionCode:'GOOD'}};
    for (const actor of ['DVBV.owner','DVBV.admin','DVCT.om']) await api(actor,'POST',catalog,body,[403,404]);
    let asset = fields(await api('DVBV.om','POST',catalog,body));
    created.push({kind:'ASSET',id:asset.assetId});
    const id = asset.assetId;
    const detail = () => api('DVBV.om','GET',`/api/v2/assets/${id}`);
    const act = async (actor, command, extra={}, statuses=[200]) => {
      const value = await api(actor,'POST',`/api/v2/assets/${id}/actions/${command}`,
        {rowVersion:asset.rowVersion,operationKey:randomUUID(),reason:'P0 TEST asset acceptance',...extra},statuses);
      if(statuses.includes(200)) asset = fields(value);
      return value;
    };
    asset = fields(await api('DVBV.om','PUT',`/api/v2/catalog/assets/${id}?branchId=${branch.branchId}`,
      {rowVersion:asset.rowVersion,fields:{name:`P0 TEST updated asset ${stamp}`,serialNumber:`TEST-${stamp}`}}));
    equal(asset.serialNumber,`TEST-${stamp}`);
    for(const actor of ['DVBV.owner','DVBV.admin','DVCT.om'])
      await act(actor,'move',{roomId:branch.rooms[1].roomId},[403,404]);
    await act('DVBV.om','move',{roomId:foreign.rooms[0].roomId},[400,403,409]);
    await act('DVBV.om','move',{roomId:branch.rooms[1].roomId});
    equal(asset.roomId,branch.rooms[1].roomId);
    await act('DVBV.om','condition',{condition:'FAIR'}); equal(asset.conditionCode,'FAIR');
    const recipient = fixture.actors['DVBV.maintenance'].userId;
    await act('DVBV.om','handover',{userId:recipient});
    await tile('ASSET_HANDOVERS',id,true);
    // Read access alone must never permit Owner/Admin receipt. A manager also cannot accept for someone else.
    for(const actor of ['DVBV.owner','DVBV.admin','DVCT.maintenance','DVBV.om'])
      await act(actor,'accept',{},[403,404]);
    await act('DVBV.maintenance','accept'); equal(asset.custodianUserId,recipient);
    await tile('ASSET_HANDOVERS',id,false);
    const snapshot = await detail(), events = snapshot.history.map(fields);
    check(events.some(e=>e.eventType==='MOVE'&&e.fromRoomId===branch.rooms[0].roomId&&e.toRoomId===branch.rooms[1].roomId),'Asset movement history missing old/new location');
    check(events.some(e=>e.handoverStatus==='ACCEPTED'&&e.actorUserId===recipient),'Recipient confirmation history missing');
    const report = await api('DVBV.om','GET',`/api/v2/insights/reports/assets?branchId=${branch.branchId}`);
    check(report.some(a=>a.assetCode===asset.assetCode&&a.roomId===branch.rooms[1].roomId&&a.conditionCode==='FAIR'),'Populated asset report mismatch');
    await api('DVCT.om','GET',`/api/v2/assets/${id}`,undefined,[403,404]);
    // Keep the audit trail, but leave no active synthetic asset after successful acceptance.
    await act('DVBV.om','retire'); equal(asset.isActive,false);
  });
  console.log(JSON.stringify({verified:results.filter(r=>r.status==='VERIFIED').length,failed:results.filter(r=>r.status==='FAILED').length}));
  return results.every(r=>r.status==='VERIFIED') ? 0 : 1;
}
if(process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try { process.exitCode = await run(process.env.P0_RUNTIME_FIXTURE,
    process.env.P0_RUNTIME_RESULT ?? 'artifacts/p0-workflow/runtime-asset-operations.json'); }
  catch { console.error('P0 asset/operations preflight failed; inspect target/fixture privately.'); process.exitCode=1; }
}
