import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: ["src/notifications/inboxApi.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/rework-notification/inbox.mjs",
});
const { resolveInboxSource, openInboxSource } =
  await import("../.test-output/rework-notification/inbox.mjs");

test("incident closure resolves room and existing participant targets without arbitrary action URLs", async () => {
  const signal = new AbortController().signal;
  for (const path of ["/rooms/8?branchId=9", "/operations/maintenance/11?action=A004A-036", "/operations/incidents/12?action=A004A-023"]) {
    globalThis.fetch = async () => Response.json({ path });
    assert.equal(await resolveInboxSource(7, signal), path);
  }
  for (const path of ["/rooms/8?branchId=0", "/rooms/8?branchId=9&next=evil", "/rooms/9007199254740992?branchId=9",
    "/rooms/8?branchId=9&branchId=10", "/operations/maintenance/11?action=A004A-031", "/operations/incidents/12?action=A004A-023&edit=1"]) {
    globalThis.fetch = async () => Response.json({ path });
    await assert.rejects(resolveInboxSource(7, signal), /Invalid notification source/);
  }
});

test("asset condition follow-up can only open its own notification receipt", async () => {
  const signal = new AbortController().signal;
  globalThis.fetch = async () => Response.json({ path: "/notifications/7/asset-condition" });
  assert.equal(await resolveInboxSource(7, signal), "/notifications/7/asset-condition");
  for (const path of ["/notifications/8/asset-condition", "/notifications/7/asset-condition?assetId=12", "//evil/notifications/7/asset-condition"]) {
    globalThis.fetch = async () => Response.json({ path });
    await assert.rejects(resolveInboxSource(7, signal));
  }
});
test("source resolver uses current session and only accepts canonical rework/history paths", async () => {
  const abort = new AbortController();
  globalThis.fetch = async (url, options) => {
    assert.equal(url, "/api/notifications/7/source");
    assert.equal(options.credentials, "same-origin");
    assert.equal(options.cache, "no-store");
    assert.equal(options.redirect, "error");
    assert.equal(options.signal, abort.signal);
    return Response.json({ path: "/cleaning/reworks/9?branchId=2" });
  };
  assert.equal(
    await resolveInboxSource(7, abort.signal),
    "/cleaning/reworks/9?branchId=2",
  );
  globalThis.fetch = async () => Response.json({path:"/internal-requests?requestId=42"});
  assert.equal(await resolveInboxSource(7,abort.signal),"/internal-requests?requestId=42");
  for (const path of ["/internal-requests?requestId=0", "/internal-requests?requestId=1&next=evil", "/internal-requests?requestId=1&requestId=2", "/internal-requests?requestId=9007199254740992"]) {
    globalThis.fetch = async () => Response.json({path});
    await assert.rejects(resolveInboxSource(7,abort.signal));
  }
  globalThis.fetch = async () =>
    Response.json({ path: "/inspection-history/9?branchId=2" });
  assert.equal(
    await resolveInboxSource(7, abort.signal),
    "/inspection-history/9?branchId=2",
  );
  globalThis.fetch = async () =>
    Response.json({ path: "/inspections/2/9/execution" });
  assert.equal(
    await resolveInboxSource(7, abort.signal),
    "/inspections/2/9/execution",
  );
  for (const path of [
    "/inspections/0/9/execution",
    "/inspections/2/9007199254740992/execution",
    "/inspections/2/9/execution?next=evil",
    "/inspections/reworks/0/assign?branchId=2",
    "/inspections/reworks/9/assign?branchId=0",
    "/inspections/reworks/9007199254740992/assign?branchId=2",
    "/inspections/reworks/9/assign?branchId=9007199254740992",
    "/inspections/reworks/9/assign?branchId=2&next=evil",
    "/inspections/reworks/9/assign?branchId=2&branchId=3",
    "/reinspections/0/9/execution",
    "/reinspections/2/0/execution",
    "/reinspections/2/9007199254740992/execution",
    "/reinspections/2/9/execution?next=evil",
    "https://example.invalid/",
    "//example.invalid",
    "/cleaning/reworks/9?branchId=2&next=evil",
    "/cleaning/reworks/0?branchId=2",
    "/cleaning/reworks/9?branchId=0",
    "/inspection-history/9?branchId=2&next=evil",
    "/inspection-history/9007199254740992?branchId=2",
    "/inspection-history/9?branchId=9007199254740992",
    "/inspection-history/9?branchId=2&branchId=3",
    null,
  ]) {
    globalThis.fetch = async () => Response.json({ path });
    await assert.rejects(
      resolveInboxSource(7, abort.signal),
      /Invalid notification source/,
    );
  }
  globalThis.fetch = async () =>
    Response.json({ path: "/reinspections/2/9/execution" });
  assert.equal(
    await resolveInboxSource(7, abort.signal),
    "/reinspections/2/9/execution",
  );
  globalThis.fetch = async () =>
    Response.json({ path: "/inspections/reworks/9/assign?branchId=2" });
  assert.equal(
    await resolveInboxSource(7, abort.signal),
    "/inspections/reworks/9/assign?branchId=2",
  );
});
test("source denial is not retried or replaced with a stored action URL", async () => {
  for (const status of [401, 403, 404, 503]) {
    let calls = 0;
    globalThis.fetch = async () => {
      calls++;
      return Response.json(
        { path: "/cleaning/reworks/9?branchId=2" },
        { status },
      );
    };
    await assert.rejects(
      resolveInboxSource(7, new AbortController().signal),
      (e) => e.status === status,
    );
    assert.equal(calls, 1);
  }
});

test("opening an authorized source marks only its notification with CSRF before navigation", async () => {
  const calls=[];
  globalThis.fetch=async(url,options)=>{
    calls.push(url);
    if(url.endsWith("/source"))return Response.json({path:"/internal-requests?requestId=42"});
    if(url==="/api/auth/csrf")return Response.json({csrfToken:"fixture",headerName:"X-CSRF-TOKEN"});
    assert.equal(url,"/api/notifications/7/read");
    assert.equal(options.method,"POST");assert.equal(options.headers["X-CSRF-TOKEN"],"fixture");
    assert.equal(options.body,undefined);
    return new Response(null,{status:204});
  };
  assert.equal(await openInboxSource(7,new AbortController().signal,()=>true),"/internal-requests?requestId=42");
  assert.deepEqual(calls,["/api/notifications/7/source","/api/auth/csrf","/api/notifications/7/read"]);
});

test("denied or invalid source cannot mark read or acknowledge", async () => {
  for(const reply of [{status:403},{status:404},{path:"https://example.invalid"}]){
    const calls=[];
    globalThis.fetch=async(url)=>{calls.push(url);return Response.json(reply,{status:reply.status??200});};
    await assert.rejects(openInboxSource(7,new AbortController().signal,()=>true));
    assert.deepEqual(calls,["/api/notifications/7/source"]);
  }
});

test("identity changes at source, CSRF or read completion cannot release a navigation path", async () => {
  for(const changedAt of ["source","csrf","read"]){
    let current=true;const calls=[];
    globalThis.fetch=async(url)=>{
      calls.push(url);
      const stage=url.endsWith("/source")?"source":url.endsWith("/csrf")?"csrf":"read";
      if(stage===changedAt)current=false;
      return stage==="source"?Response.json({path:"/internal-requests?requestId=42"})
        :stage==="csrf"?Response.json({csrfToken:"fixture",headerName:"X-CSRF-TOKEN"}):new Response(null,{status:204});
    };
    await assert.rejects(openInboxSource(7,new AbortController().signal,()=>current),/Phiên đăng nhập đã thay đổi/);
    assert.equal(calls.length,changedAt==="source"?1:changedAt==="csrf"?2:3);
  }
});

test("failed read confirmation does not navigate or retry a mutation", async () => {
  const calls=[];
  globalThis.fetch=async(url)=>{
    calls.push(url);
    return url.endsWith("/source")?Response.json({path:"/internal-requests?requestId=42"})
      :url.endsWith("/csrf")?Response.json({csrfToken:"fixture",headerName:"X-CSRF-TOKEN"}):new Response(null,{status:503});
  };
  await assert.rejects(openInboxSource(7,new AbortController().signal,()=>true),e=>e.status===503);
  assert.equal(calls.filter(url=>url.endsWith("/read")).length,1);
});
