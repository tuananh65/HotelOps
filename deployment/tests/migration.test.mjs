import test from 'node:test';
import assert from 'node:assert/strict';
import {execFileSync,spawnSync} from 'node:child_process';
import fs from 'node:fs';
const run=(...args)=>execFileSync(process.execPath,['deployment/render-migration.mjs',...args],{encoding:'utf8'});
test('schema-only cannot target production or an injected identifier',()=>{
 for(const db of ['HotelOpsCompactDB',"HotelOpsCompactTest';DROP DATABASE x;--"])
  assert.notEqual(spawnSync(process.execPath,['deployment/render-migration.mjs','schema-only',db]).status,0);
});
test('fresh TEST schema avoids demo migrations and user/stock seeding',()=>{
 const sql=run('schema-only','HotelOpsCompactCiTest');
 assert.match(sql,/BEGIN TRANSACTION/);assert.match(sql,/COMMIT/);
 assert.ok(!sql.includes('-- 07_V10Roles.sql'));assert.ok(sql.includes('-- p0-role-migration.sql'));
 assert.ok(!sql.includes('INSERT users('));assert.ok(!sql.includes('INSERT stock_movements('));
 assert.match(sql,/p0-finalize.sql/);assert.ok(!sql.includes('Seed-Testing'));
});
test('P0 migration preserves credentials and ledger and uses exact target guards',()=>{
 const sql=run('p0','HotelOpsCompactDB');
 assert.equal((sql.match(/Exact deployment database required/g)??[]).length,9);
 assert.match(sql,/#users_before/);assert.match(sql,/#stock_before/);assert.match(sql,/#audit_before/);
 assert.ok(!sql.includes('DISABLE TRIGGER'));
});
test('deployment never calls schema install or resets SQL volumes',()=>{
 const deploy=fs.readFileSync('deployment/deploy.sh','utf8');
 assert.ok(!deploy.includes('init-database'));assert.ok(!deploy.includes('migrate-database'));
 assert.ok(!deploy.includes('down -v'));assert.match(deploy,/backup-database/);assert.match(deploy,/healthcheck/);
});
test('fresh V2 schema includes lifecycle, assessment, invitations and maintenance staff without legacy fixture recovery',()=>{
 const sql=run('schema-only','HotelOpsCompactCiTest');
 for(const table of ['item_lifecycle_profiles','stock_batches','stock_batch_entries','staff_batch_balances','reusable_positions','reusable_movements','reusable_write_offs','account_invitations'])
  assert.ok(sql.includes(`CREATE TABLE dbo.${table}(`),table);
 assert.match(sql,/VALUES\('MAINTENANCE_STAFF',N'Nhân viên bảo trì',1\)/);
 assert.match(sql,/V2 schema table count mismatch/);
 assert.ok(!sql.includes('-- v2-recover-maintenance.sql'));
 assert.ok(!sql.includes('-- v2-kettle-pending-assets.sql'));
 assert.ok(!sql.includes('Approved mapping identity does not match this baseline'));
 assert.ok(!sql.includes('HotelOpsCompactWorkdayTestV22611250010'));
 const health=fs.readFileSync('deployment/healthcheck.sh','utf8');
 assert.match(health,/x\.tables===57/);
});
