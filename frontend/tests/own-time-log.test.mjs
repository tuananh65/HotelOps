import {test} from "node:test";
import assert from "node:assert/strict";
import {build} from "esbuild";
await build({entryPoints:["src/workTime/timeLogApi.ts"],bundle:true,platform:"node",format:"esm",outfile:".test-output/own-time-log/api.mjs"});
const {decodeTimeLog,readTimeLog}=await import("../.test-output/own-time-log/api.mjs");
await build({entryPoints:["src/shared-host/returnUrl.ts"],bundle:true,platform:"node",format:"esm",outfile:".test-output/own-time-log/return.mjs"});
const {approvedReturnDestination,loginDestination}=await import("../.test-output/own-time-log/return.mjs");
test("own time log survives login only at the approved canonical route",()=>{
  assert.equal(loginDestination("/my-time-log",""),"/login?returnTo=%2Fmy-time-log");
  assert.equal(approvedReturnDestination("/my-time-log",true),"/my-time-log");
  assert.equal(approvedReturnDestination("/my-time-log",false),null);
  assert.equal(approvedReturnDestination("/my-time-log?userId=7",true),null);
  assert.equal(approvedReturnDestination("//example.invalid/my-time-log",true),null);
});
const row={intervalId:1,kind:"TASK",workItemId:7,start:"2026-11-16T01:00:00+00:00",end:null,closedSeconds:0,provisionalSeconds:60,version:"AAAAAAAAAAA="};
const body=()=>({branchId:2,date:"2026-11-16",asOf:"2026-11-16T01:01:00Z",page:1,pageSize:50,total:1,workdayClosedSeconds:0,workdayProvisionalSeconds:60,workClosedSeconds:0,workProvisionalSeconds:60,items:[row]});
test("rejects response from another branch date or page and malformed durations",()=>{
  assert.equal(decodeTimeLog(body(),2,"2026-11-16",1).total,1);
  for(const patch of [{branchId:3},{date:"2026-11-17"},{page:2},{workClosedSeconds:-1},{items:[row,row],total:2},{items:[{...row,version:"bad"}]}])
    assert.throws(()=>decodeTimeLog({...body(),...patch},2,"2026-11-16",1));
});
test("read validates identity and fails denial without automatic retry",async()=>{
  let calls=0;globalThis.fetch=async(url,options)=>{calls++;assert.match(url,/\/api\/branches\/2\/workdays\/own-time-log/);assert.equal(options.cache,"no-store");assert.equal(options.redirect,"error");return new Response(null,{status:403});};
  await assert.rejects(readTimeLog(0,"2026-11-16",1,new AbortController().signal));assert.equal(calls,0);
  await assert.rejects(readTimeLog(2,"2026-11-16",1,new AbortController().signal),e=>e.status===403);assert.equal(calls,1);
});
