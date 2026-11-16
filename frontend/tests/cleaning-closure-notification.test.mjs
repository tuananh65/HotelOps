import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({ entryPoints: ["src/notifications/inboxApi.ts"], bundle: true, platform: "node",
  format: "esm", outfile: ".test-output/cleaning-closure-notification/inbox.mjs" });
const { resolveInboxSource, openInboxSource } = await import("../.test-output/cleaning-closure-notification/inbox.mjs");

test("cleaning closure opens only the exact supervisor monitor route", async () => {
  const signal = new AbortController().signal;
  const path = "/cleaning/tasks/42?branchId=2&monitor=1";
  const calls = [];
  globalThis.fetch = async (url, options) => {
    calls.push(url);
    if (url.endsWith("/source")) return Response.json({ path });
    if (url.endsWith("/csrf")) return Response.json({ csrfToken: "fixture", headerName: "X-CSRF-TOKEN" });
    assert.equal(options.headers["X-CSRF-TOKEN"], "fixture");
    return new Response(null, { status: 204 });
  };
  assert.equal(await openInboxSource(7, signal, () => true), path);
  assert.deepEqual(calls, ["/api/notifications/7/source", "/api/auth/csrf", "/api/notifications/7/read"]);
  for (const invalid of [
    "/cleaning/tasks/42?branchId=2", path + "&execution=1", path + "&branchId=3",
    path.replace("monitor=1", "monitor=0"), path.replace("branchId=2", "branchId=0"),
    path.replace("tasks/42", "tasks/9007199254740992"),
  ]) {
    globalThis.fetch = async () => Response.json({ path: invalid });
    await assert.rejects(resolveInboxSource(7, signal), /Invalid notification source/);
  }
});

test("cancellation source opens only its own receipt and preserves CSRF mark-read", async () => {
  const signal=new AbortController().signal;
  const path="/notifications/7/cleaning-cancellation";
  const calls=[];
  globalThis.fetch=async (url,options)=>{
    calls.push(url);
    if(url.endsWith("/source")) return Response.json({path});
    if(url.endsWith("/csrf")) return Response.json({csrfToken:"fresh",headerName:"X-CSRF-TOKEN"});
    assert.equal(options.headers["X-CSRF-TOKEN"],"fresh");
    return new Response(null,{status:204});
  };
  assert.equal(await openInboxSource(7,signal,()=>true),path);
  assert.deepEqual(calls,["/api/notifications/7/source","/api/auth/csrf","/api/notifications/7/read"]);
  for(const bad of ["/notifications/8/cleaning-cancellation",path+"?taskId=1",path+"/edit","//evil/notifications/7/cleaning-cancellation"]){
    globalThis.fetch=async ()=>Response.json({path:bad});
    await assert.rejects(resolveInboxSource(7,signal),/Invalid notification source/);
  }
});