import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import ts from "typescript";
const compile=name=>ts.transpileModule(readFileSync(new URL(`../src/operationalTasks/${name}.ts`,import.meta.url),"utf8"),{compilerOptions:{module:ts.ModuleKind.ESNext,target:ts.ScriptTarget.ES2022}}).outputText;
const url=s=>"data:text/javascript;base64,"+Buffer.from(s).toString("base64");
const {decideVerification,readVerification}=await import(url(compile("verification").replace('"./progress"',JSON.stringify(url(compile("progress"))))));
const signal=new AbortController().signal;
const current={data:{taskId:4,branchId:2,submissionId:11,state:"PENDING_VERIFICATION",actualEffortSeconds:90,canDecide:true,denialCode:null,decisions:[]},etag:'"0000000000000001"'};
test("verification binds the exact submission, ETag, CSRF and authority snapshot",async t=>{
 const calls=[];t.mock.method(globalThis,"fetch",async(path,init)=>{calls.push(path);assert.equal(init.credentials,"same-origin");assert.equal(init.redirect,"error");assert.equal(init.cache,"no-store");assert.equal(init.signal,signal);
  if(path==="/api/auth/csrf")return Response.json({headerName:"X-CSRF-TOKEN",csrfToken:"synthetic"});
  assert.equal(path,"/api/branches/2/operational-tasks/4/verification");assert.equal(init.headers["If-Match"],current.etag);assert.deepEqual(JSON.parse(init.body),{submissionId:11,decision:"RETURN",note:"Correct the submitted work"});
  return Response.json({taskId:4,branchId:2,submissionId:11,state:"ACCEPTED",messageCode:"MSG20"},{headers:{ETag:'"0000000000000002"'}});
 });assert.deepEqual(await decideVerification(2,4,current,"RETURN","Correct the submitted work",signal),{state:"ACCEPTED",messageCode:"MSG20"});assert.equal(calls.length,2);
});
test("self or denied review, closed state, foreign scope and short Return never dispatch",async t=>{
 const fetch=t.mock.method(globalThis,"fetch",()=>{throw Error("Unexpected request");});
 for(const snapshot of [{...current,data:{...current.data,canDecide:false}},{...current,data:{...current.data,state:"CLOSED"}},{...current,data:{...current.data,branchId:3}}])await assert.rejects(decideVerification(2,4,snapshot,"ACCEPT","Verified",signal));
 await assert.rejects(decideVerification(2,4,current,"RETURN","short",signal));await assert.rejects(decideVerification(2,4,current,"ACCEPT","   ",signal));assert.equal(fetch.mock.callCount(),0);
});
test("stale verification is surfaced without replay",async t=>{
 let writes=0;t.mock.method(globalThis,"fetch",async path=>path==="/api/auth/csrf"?Response.json({headerName:"X-CSRF-TOKEN",csrfToken:"synthetic"}):(writes++,Response.json({},{status:412})));
 await assert.rejects(decideVerification(2,4,current,"ACCEPT","Verified",signal),e=>e.status===412);assert.equal(writes,1);
});
test("verification success with wrong workflow or unchanged version is unconfirmed",async t=>{
 for(const result of [{state:"ACCEPTED",etag:'"0000000000000002"'},{state:"CLOSED",etag:current.etag}]){
  t.mock.method(globalThis,"fetch",async path=>path==="/api/auth/csrf"?Response.json({headerName:"X-CSRF-TOKEN",csrfToken:"synthetic"}):Response.json({taskId:4,branchId:2,submissionId:11,state:result.state,messageCode:"MSG08"},{headers:{ETag:result.etag}}));
  await assert.rejects(decideVerification(2,4,current,"ACCEPT","Verified",signal),/unconfirmed/);t.mock.restoreAll();
 }
});
test("read rejects foreign task and impossible separation-of-duties history",async t=>{
 const invalid=[{...current.data,taskId:5},{...current.data,decisions:[{submissionId:11,decision:"ACCEPT",note:"Verified",requestedBy:8,submittedBy:9,performedBy:[10],verifiedBy:10,decidedAt:"2026-11-15T11:00:00Z",actualEffortSeconds:90}]}];
 for(const data of invalid){t.mock.method(globalThis,"fetch",async()=>Response.json(data,{headers:{ETag:current.etag}}));await assert.rejects(readVerification(2,4,signal),/Invalid verification snapshot/);t.mock.restoreAll();}
});
