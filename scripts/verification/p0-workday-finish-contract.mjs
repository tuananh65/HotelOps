import assert from 'node:assert/strict';
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import ts from '../../frontend/node_modules/typescript/lib/typescript.js';

const read = path => readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8');
const service = read('src/HotelOps.Api/Compact/Services/WorkdayService.cs');
const supplies = read('src/HotelOps.Api/Compact/Services/StaffSupplyService.cs');
const expiry = read('src/HotelOps.Api/Compact/Services/ShiftExpiryService.cs');
assert.doesNotMatch(expiry, /role\.Code != "HK_STAFF"/);
assert.match(expiry, /Attendance expires for every role/);
assert.ok(service.indexOf('var previous=await db.WorkdayEvents') < service.indexOf('Output.Version(row.RowVersion,request.RowVersion)'));
assert.match(service, /await supplies\.End\(id,request.OperationKey,ct\);\s*row.Status="ENDED";row.EndedAt\?\?=Now/);
assert.match(service, /PREVIOUS_SHIFT_RECONCILIATION_REQUIRED/);
assert.match(service, /canEnd=endBlockedReason==null/);
assert.match(supplies, /RequestWorkflowService.RefillType/);
assert.match(supplies, /work.Status!="PENDING_INSPECTION"&&work.Status!="PENDING_REINSPECTION"&&work.Status!="PENDING_VERIFICATION"/);
assert.match(supplies, /now>=start&&\(allowFinishing\|\|now<end\)/);
const result = ts.transpileModule(read('frontend/src/compact/workday.tsx'), {
  fileName: 'workday.tsx', reportDiagnostics: true,
  compilerOptions: { jsx: ts.JsxEmit.ReactJSX, target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext },
});
assert.equal(result.diagnostics?.filter(d => d.category === ts.DiagnosticCategory.Error).length, 0);
mkdirSync(new URL('../../artifacts/p0-workflow/', import.meta.url), { recursive: true });
writeFileSync(new URL('../../artifacts/p0-workflow/workday-syntax.mjs', import.meta.url), result.outputText);
console.log('Workday static contracts and TSX syntax passed. Runtime/concurrency not tested.');
