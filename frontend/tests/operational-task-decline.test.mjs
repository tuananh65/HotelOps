import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import ts from "typescript";
async function module(path) {
  const code = ts.transpileModule(readFileSync(new URL(path, import.meta.url), "utf8"), { compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } }).outputText;
  return import("data:text/javascript;base64," + Buffer.from(code).toString("base64"));
}
const { declineTask, TaskProgressError } = await module("../src/operationalTasks/progress.ts");
const { readTasks } = await module("../src/operationalTasks/api.ts");
const current = { data: { id: 4, branchId: 2, kind: "task", state: "ASSIGNED", percent: 0, items: [], evidence: [] }, etag: '"0000000000000001"' };
const signal = new AbortController().signal;
const csrf = { headerName: "X-CSRF-TOKEN", csrfToken: "synthetic" };
const result = { taskId: 4, branchId: 2, state: "PENDING_ASSIGNMENT", messageCode: "MSG19" };
test("Decline confirms identity/version and makes no lost-owner GET or automatic retry", async t => {
  const calls = []; t.mock.method(globalThis, "fetch", async (url, options) => {
    calls.push(url); assert.equal(options.signal, signal); assert.equal(options.credentials, "same-origin"); assert.equal(options.cache, "no-store"); assert.equal(options.redirect, "error");
    if (url === "/api/auth/csrf") return Response.json(csrf);
    assert.equal(url, "/api/branches/2/operational-tasks/4/decline"); assert.equal(options.headers["If-Match"], current.etag); assert.equal(options.headers["X-CSRF-TOKEN"], csrf.csrfToken);
    assert.deepEqual(JSON.parse(options.body), { reason: "No" }); return Response.json(result, { headers: { ETag: '"0000000000000002"' } });
  });
  assert.deepEqual(await declineTask(2, 4, current, " No ", signal), result); assert.equal(calls.length, 2);
});
test("Decline validates Assigned state, reason, size and controls before network", async t => {
  t.mock.method(globalThis, "fetch", () => { throw new Error("Network should not run"); });
  for (const reason of ["", " \n ", "x".repeat(4001), "bad\u0001"]) await assert.rejects(declineTask(2,4,current,reason,signal), /valid decline reason/);
  await assert.rejects(declineTask(2,4,{...current,data:{...current.data,state:"ACCEPTED"}},"No",signal), /valid decline reason/);
});
test("Decline refuses wrong identity, old ETag and incomplete success receipts", async t => {
  for (const [body, etag] of [[{...result, taskId:5},'"0000000000000002"'],[result,current.etag],[{...result,messageCode:null},'"0000000000000002"']]) {
    t.mock.method(globalThis,"fetch",async url => url === "/api/auth/csrf" ? Response.json(csrf) : Response.json(body,{headers:{ETag:etag}}));
    await assert.rejects(declineTask(2,4,current,"No",signal),/could not be confirmed/);
  }
});
test("Decline propagates authority/stale failures without retry",async t=>{
  for(const status of [403,412,503]) { let calls=0;t.mock.method(globalThis,"fetch",async url=>{calls++;return url==="/api/auth/csrf"?Response.json(csrf):Response.json({}, {status});});
    await assert.rejects(declineTask(2,4,current,"No",signal),e=>e instanceof TaskProgressError&&e.status===status);assert.equal(calls,2); }
});
test("Task list passes canonical workflow filter and rejects invented state",async t=>{
  t.mock.method(globalThis,"fetch",async url=>{assert.equal(new URL(url,"https://localhost").searchParams.get("workflowStatus"),"PENDING_ASSIGNMENT");return Response.json({items:[],totalCount:0,page:1,pageSize:25});});
  await readTasks(2,1,"",null,signal,"PENDING_ASSIGNMENT");await assert.rejects(readTasks(2,1,"",null,signal,"TODO"),/Invalid workflow filter/);
});
