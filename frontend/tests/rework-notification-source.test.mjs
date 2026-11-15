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
const { resolveInboxSource } =
  await import("../.test-output/rework-notification/inbox.mjs");
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
  globalThis.fetch = async () =>
    Response.json({ path: "/inspection-history/9?branchId=2" });
  assert.equal(
    await resolveInboxSource(7, abort.signal),
    "/inspection-history/9?branchId=2",
  );
  for (const path of [
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
