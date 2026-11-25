import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const [mode,database]=process.argv.slice(2);
if(!['schema-only','p0'].includes(mode)||!/^HotelOpsCompact[A-Za-z0-9_]{0,90}$/.test(database??''))throw Error('Explicit Compact target and migration mode required');
if(mode==='schema-only'&&!/^HotelOpsCompact.*Test/.test(database))throw Error('Empty schema creation is restricted to isolated TEST');
const base=['01_Schema.sql','02_Integrity.sql','03_DocumentGuards.sql','04_Level2.sql','06_Workday.sql','enable-room-reservations.sql','enable-paper-inspection.sql','enable-room-remediation.sql','add-department-description.sql'];
const p0=['p0-role-migration.sql','p0-supplies.sql','p0-store-policy.sql','p0-readiness.sql','p0-workflow-guards.sql','p0-requests.sql','p0-room-request-backfill.sql','p0-supply-defaults.sql','p0-finalize.sql'];
// Structural deltas only. Recovery of existing employees, inventory classification,
// kettle registration and cleanup require target-specific evidence and are never replayed here.
const v2=['v2-checklist-permissions.sql','v2-room-assessment.sql','v2-assessment-guards.sql','v2-invitations.sql','v2-inventory-batches.sql','v2-reusable-inventory.sql','v2-reusable-legacy-settlement.sql','v2-reusable-shift-configuration.sql'];
// Schema-only CI never seeds users, passwords, branches or stock. Historical 07–10
// installers contain demo-specific data conversion and must not be replayed.
const files=mode==='p0'?p0:[...base,...p0.filter(f=>f!=='p0-supply-defaults.sql'),...v2];
let result=`:ON ERROR EXIT\nUSE [${database}];\nGO\nSET XACT_ABORT ON; BEGIN TRANSACTION;\nIF DB_NAME()<>N'${database}' THROW 51900,'Wrong deployment database',1;\nGO\n`;
if(mode==='p0')result+='SELECT user_id,username,password_hash,security_stamp INTO #users_before FROM users;\nSELECT * INTO #stock_before FROM stock_movements;\nSELECT work_event_id INTO #events_before FROM work_events;\nSELECT audit_id INTO #audit_before FROM audit_logs;\nGO\n';
for(const file of files){
 if(mode==='schema-only'&&file===v2[0]){
  result+=`INSERT roles(code,name,is_active) VALUES('MAINTENANCE_STAFF',N'Nhân viên bảo trì',1);
UPDATE roles SET name=N'Trưởng đội bảo trì',description=N'Maintenance Lead' WHERE code='MAINTENANCE_SUPERVISOR';
INSERT role_permissions(role_id,permission_id,data_scope)
SELECT r.role_id,p.permission_id,g.scope FROM (VALUES ('MAINTENANCE_VIEW','OWN'),('MAINTENANCE_EXECUTE','OWN'),('ASSET_VIEW','BRANCH'),('ATTENDANCE_RECORD','OWN'),('IR_VIEW','OWN'),('IR_CREATE','OWN'),('IR_PROCESS','OWN'))g(code,scope)
JOIN permissions p ON p.code=g.code CROSS JOIN roles r WHERE r.code='MAINTENANCE_STAFF';
INSERT role_permissions(role_id,permission_id,data_scope)
SELECT r.role_id,p.permission_id,'BRANCH' FROM roles r CROSS JOIN permissions p WHERE r.code='MAINTENANCE_SUPERVISOR' AND p.code='MAINTENANCE_ASSIGN'
AND NOT EXISTS(SELECT 1 FROM role_permissions rp WHERE rp.role_id=r.role_id AND rp.permission_id=p.permission_id);
GO\n`;
 }
 if(mode==='schema-only'&&file==='p0-role-migration.sql'){
  const catalog=fs.readFileSync(path.join(root,'src/HotelOps.Api/Compact/Security/PermissionCatalog.cs'),'utf8');
  const codes=catalog.match(/Codes=\("([A-Z0-9_ ]+)"\)/)?.[1].split(' ');
  if(!codes?.length)throw Error('Review permission catalog format');
  result+='INSERT permissions(code,module_code,is_active) SELECT v.code,v.module_code,1 FROM (VALUES '+codes.map(c=>`('${c}','${c.split('_')[0]}')`).join(',')+')v(code,module_code) WHERE NOT EXISTS(SELECT 1 FROM permissions p WHERE p.code=v.code);\n';
  const roles=['SYS_ADMIN','OWNER','OPERATION_MANAGER','HOUSEKEEPING_SUPERVISOR','HK_STAFF','RECEPTION','STOREKEEPER','MAINTENANCE_SUPERVISOR'];
  result+='INSERT roles(code,name,is_active) VALUES '+roles.map(c=>`('${c}',N'${c}',1)`).join(',')+';\nGO\n';
 }
 let source=fs.readFileSync(path.join(root,'scripts/database/compact-v2',file),'utf8');
 if(mode==='schema-only'&&file==='v2-inventory-batches.sql'){
  const marker='-- Only mapping accepted catalog identities;';
  if(source.split(marker).length!==2)throw Error('Review inventory schema/legacy-data boundary');
  source=source.split(marker)[0]+'COMMIT;\n';
 }
 if(file.startsWith('v2-')){
  const guard=/^IF\s+DB_NAME\(\)\s*<>\s*'HotelOpsCompactWorkdayTestV22611250010'\s+THROW[^\r\n]+;\r?$/gm;
  if([...source.matchAll(guard)].length!==1)throw Error(`Review target guard in ${file}`);
  source=source.replace(guard,`IF DB_NAME()<>N'${database}' THROW 51900,'Exact deployment database required',1;`);
 }
 if(file.startsWith('p0-')){
  const guard=/^IF DB_NAME\(\) NOT LIKE [^\r\n]+ THROW [^\r\n]+;\r?$/gm;
  if([...source.matchAll(guard)].length!==1)throw Error(`Review target guard in ${file}`);
  source=source.replace(guard,`IF DB_NAME()<>N'${database}' THROW 51900,'Exact deployment database required',1;`);
 }
 result+=`\n-- ${file}\n${source}\nGO\n`;
}
if(mode==='schema-only')result+=`IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>57 THROW 51990,'V2 schema table count mismatch',1;
IF EXISTS(SELECT 1 FROM users) OR EXISTS(SELECT 1 FROM inventory_items) OR EXISTS(SELECT 1 FROM stock_movements)
 THROW 51990,'Fresh schema must not contain operational seed data',1;
GO\n`;
if(mode==='p0')result+=`IF EXISTS(SELECT * FROM #users_before EXCEPT SELECT user_id,username,password_hash,security_stamp FROM users)
 OR (SELECT COUNT(*) FROM users)<>(SELECT COUNT(*) FROM #users_before) THROW 51990,'User preservation failed',1;
IF EXISTS(SELECT * FROM #stock_before EXCEPT SELECT * FROM stock_movements)
 OR (SELECT COUNT(*) FROM stock_movements)<>(SELECT COUNT(*) FROM #stock_before) THROW 51990,'Stock preservation failed',1;
IF EXISTS(SELECT work_event_id FROM #events_before EXCEPT SELECT work_event_id FROM work_events)
 OR EXISTS(SELECT audit_id FROM #audit_before EXCEPT SELECT audit_id FROM audit_logs) THROW 51990,'History preservation failed',1;\n`;
result+=`IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1)
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1)
 OR EXISTS(SELECT 1 FROM sys.triggers WHERE parent_class=1 AND is_disabled=1) THROW 51990,'Untrusted database guards',1;
COMMIT;
GO
`;
process.stdout.write(result);
