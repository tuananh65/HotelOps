import test from 'node:test';import assert from 'node:assert/strict';import {readFileSync} from 'node:fs';import ts from 'typescript';
const compile=n=>ts.transpileModule(readFileSync(new URL(`../src/operationalTasks/${n}.ts`,import.meta.url),'utf8'),{compilerOptions:{module:ts.ModuleKind.ESNext,target:ts.ScriptTarget.ES2022}}).outputText;
const url=s=>'data:text/javascript;base64,'+Buffer.from(s).toString('base64');
const {assignTask,readAssignment}=await import(url(compile('assignment').replace('"./progress"',JSON.stringify(url(compile('progress'))))));
const signal=new AbortController().signal;
const snapshot={etag:'"0000000000000001"',data:{taskId:4,branchId:2,state:'ACCEPTED',assigneeId:8,asOf:'2026-11-15T12:00:00Z',timezone:'Asia/Ho_Chi_Minh',totalCount:1,page:1,pageSize:25,executors:[{userId:9,name:'Executor',openTasks:2,loggedSecondsToday:60}]}};
test('assignment binds current task, owner candidate, ETag and CSRF without retry',async t=>{
 let writes=0;t.mock.method(globalThis,'fetch',async(path,init)=>{assert.equal(init.credentials,'same-origin');assert.equal(init.redirect,'error');assert.equal(init.cache,'no-store');assert.equal(init.signal,signal);
 if(path==='/api/auth/csrf')return Response.json({headerName:'X-CSRF-TOKEN',csrfToken:'synthetic'});
 assert.equal(path,'/api/branches/2/operational-tasks/4/assignment');assert.equal(init.headers['If-Match'],snapshot.etag);assert.deepEqual(JSON.parse(init.body),{assigneeId:9,reason:'Cover'});writes++;
 return Response.json({taskId:4,branchId:2,assigneeId:9,state:'ASSIGNED',messageCode:'MSG08'},{headers:{ETag:'"0000000000000002"'}});});
 await assignTask(2,4,snapshot,9,' Cover ',signal);assert.equal(writes,1);
});
test('unknown executor, self reassign, missing reason and closed workflows never dispatch',async t=>{
 const spy=t.mock.method(globalThis,'fetch',()=>{throw Error('Unexpected');});
 for(const [s,id,reason] of [[snapshot,10,'Cover'],[snapshot,8,'Cover'],[snapshot,9,'  '],[{...snapshot,data:{...snapshot.data,state:'CLOSED'}},9,'Cover'],[{...snapshot,data:{...snapshot.data,branchId:3}},9,'Cover'],[snapshot,9,'x\u0001']])await assert.rejects(assignTask(2,4,s,id,reason,signal));
 assert.equal(spy.mock.callCount(),0);
});
test('stale and ambiguous assignment outcomes are not replayed',async t=>{
 for(const status of [412,503]){let writes=0;t.mock.method(globalThis,'fetch',async path=>path==='/api/auth/csrf'?Response.json({headerName:'X-CSRF-TOKEN',csrfToken:'synthetic'}):(writes++,Response.json({},{status})));
 await assert.rejects(assignTask(2,4,snapshot,9,'Cover',signal),e=>e.status===status);assert.equal(writes,1);t.mock.restoreAll();}
});
test('foreign or unchanged success receipt is unconfirmed',async t=>{
 for(const result of [{taskId:5,etag:'"0000000000000002"'},{taskId:4,etag:snapshot.etag}]){
 t.mock.method(globalThis,'fetch',async path=>path==='/api/auth/csrf'?Response.json({headerName:'X-CSRF-TOKEN',csrfToken:'synthetic'}):Response.json({taskId:result.taskId,branchId:2,assigneeId:9,state:'ASSIGNED',messageCode:'MSG08'},{headers:{ETag:result.etag}}));
 await assert.rejects(assignTask(2,4,snapshot,9,'Cover',signal),/unconfirmed/);t.mock.restoreAll();}
});
test('snapshot validates workload, pagination, scope and sole workflow authority',async t=>{
 for(const data of [{...snapshot.data,taskId:5},{...snapshot.data,assigneeId:null},{...snapshot.data,state:'TODO'},{...snapshot.data,page:2},{...snapshot.data,executors:[{...snapshot.data.executors[0],loggedSecondsToday:-1}]},{...snapshot.data,executors:[snapshot.data.executors[0],snapshot.data.executors[0]]}]){
 t.mock.method(globalThis,'fetch',async()=>Response.json(data,{headers:{ETag:snapshot.etag}}));await assert.rejects(readAssignment(2,4,1,'',signal),/Invalid assignment snapshot/);t.mock.restoreAll();}
});
test('search is encoded and pending assignment permits no reason',async t=>{
 const current={...snapshot,data:{...snapshot.data,state:'PENDING_ASSIGNMENT',assigneeId:null}};
 t.mock.method(globalThis,'fetch',async(path,init)=>{
 if(path.includes('?page=')){assert.equal(path,'/api/branches/2/operational-tasks/4/assignment?page=1&search=A%26B');return Response.json(current.data,{headers:{ETag:snapshot.etag}});}
 if(path==='/api/auth/csrf')return Response.json({headerName:'X-CSRF-TOKEN',csrfToken:'synthetic'});
 assert.equal(JSON.parse(init.body).reason,null);return Response.json({taskId:4,branchId:2,assigneeId:9,state:'ASSIGNED',messageCode:'MSG08'},{headers:{ETag:'"0000000000000002"'}});
 });await readAssignment(2,4,1,'A&B',signal);await assignTask(2,4,current,9,'',signal);
});
