import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: ["src/notifications/inboxApi.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/selected-read/inbox.mjs",
});
const { markInboxRead } =
  await import("../.test-output/selected-read/inbox.mjs");
test("acknowledgment requires selected IDs and uses its explicit endpoint", async () => {
  const calls=[];const signal=new AbortController().signal;
  globalThis.fetch=async(url,options)=>{calls.push({url,options});return url==="/api/auth/csrf"?Response.json({csrfToken:"synthetic",headerName:"X-CSRF-TOKEN"}):new Response(null,{status:204});};
  await assert.rejects(markInboxRead(null,signal,()=>true,"acknowledge"));
  assert.equal(calls.length,0);
  await markInboxRead([4,5],signal,()=>true,"acknowledge");
  assert.equal(calls[1].url,"/api/notifications/acknowledge");
  assert.deepEqual(JSON.parse(calls[1].options.body),{ids:[4,5]});
});
test("selected read validates before network and deduplicates a bounded batch", async () => {
  const signal = new AbortController().signal;
  let calls = [];
  globalThis.fetch = async (url, options) => {
    calls.push({ url, options });
    return url === "/api/auth/csrf"
      ? Response.json({ csrfToken: "synthetic", headerName: "X-CSRF-TOKEN" })
      : new Response(null, { status: 204 });
  };
  for (const ids of [
    [],
    [0],
    [-1],
    [1.5],
    [Number.MAX_SAFE_INTEGER + 1],
    Array(101).fill(1),
  ])
    await assert.rejects(markInboxRead(ids, signal, () => true));
  assert.equal(calls.length, 0);
  await markInboxRead([4, 5, 4], signal, () => true);
  assert.equal(calls[1].url, "/api/notifications/read-selected");
  assert.deepEqual(JSON.parse(calls[1].options.body), { ids: [4, 5] });
  assert.equal(calls[1].options.headers["X-CSRF-TOKEN"], "synthetic");
  assert.equal(calls[1].options.credentials, "same-origin");
  assert.equal(calls[1].options.redirect, "error");
});
test("authority replacement after CSRF prevents selected mutation", async () => {
  let count = 0;
  globalThis.fetch = async () => {
    count++;
    return Response.json({
      csrfToken: "synthetic",
      headerName: "X-CSRF-TOKEN",
    });
  };
  await assert.rejects(
    markInboxRead([4, 5], new AbortController().signal, () => false),
    /Phiên đăng nhập đã thay đổi/,
  );
  assert.equal(count, 1);
});
