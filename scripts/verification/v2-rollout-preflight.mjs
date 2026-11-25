import assert from 'node:assert/strict';
import {readFile, realpath} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import {dirname, resolve, relative, isAbsolute} from 'node:path';
import {fileURLToPath} from 'node:url';

// Offline inspection only. No SQL client, process spawning, secrets, or writes.
assert.equal(process.argv.length,2,'No arguments accepted; this tool cannot apply migrations or select a database.');
const root=await realpath(resolve(dirname(fileURLToPath(import.meta.url)),'../..'));
const manifest=JSON.parse(await readFile(resolve(root,'docs/restructure-v2/rollout-manifest.json'),'utf8'));
assert.equal(manifest.formatVersion,1);
assert.equal(manifest.databaseMutationAllowed,false);
assert.equal(manifest.allowedScriptDatabase,'HotelOpsCompactWorkdayTestV22611250010');
const seen=new Set(),checks=[];
async function verifiedFile(entry){
  assert.match(entry.sha256,/^[a-f0-9]{64}$/);
  const path=await realpath(resolve(root,entry.path)),local=relative(root,path);
  assert.ok(local&&!local.startsWith('..')&&!isAbsolute(local),'File must remain inside repository.');
  const bytes=await readFile(path);
  assert.equal(createHash('sha256').update(bytes).digest('hex'),entry.sha256,'Reviewed hash changed: '+entry.path);
  return bytes.toString('utf8');
}
for(const [index,step] of manifest.steps.entries()){
  assert.equal(step.order,index+1);assert.ok(!seen.has(step.id),'Duplicate step');
  for(const dependency of step.dependsOn)assert.ok(seen.has(dependency),'Dependency must precede '+step.id+': '+dependency);
  const sql=await verifiedFile(step);
  assert.match(sql,/IF\s+DB_NAME\(\)\s*<>\s*'HotelOpsCompactWorkdayTestV22611250010'\s+THROW/i,'Required TEST-only guard missing: '+step.id);
  if(step.mutatesDatabase){assert.match(sql,/SET XACT_ABORT ON/i);assert.match(sql,/BEGIN TRANSACTION/i);assert.match(sql,/COMMIT/i);}
  seen.add(step.id);checks.push({id:step.id,hash:'PASS',databaseGuard:'PASS',dependencies:'PASS'});
}
assert.equal(seen.size,11,'Reviewed delta contains eleven ordered steps.');
const gate=await verifiedFile(manifest.schemaGate);
for(const count of [57,234,116,38])assert.ok(gate.includes(')='+count),'SchemaGate count missing: '+count);
assert.ok(gate.includes("value)='3.0.0'"));
console.log(JSON.stringify({at:new Date().toISOString(),status:'PASS_OFFLINE_PREFLIGHT',databaseConnected:false,databaseMutated:false,checks,schemaGateHash:'PASS',releaseStatus:manifest.releaseStatus,limits:'Hashes, ordering and guard text only. No backup, restore, migration, SQL assertion, Linux verification or cutover executed.'},null,2));
