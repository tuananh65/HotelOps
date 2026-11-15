import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({ entryPoints: ["src/internalRequests/historyApi.ts", "src/shared-host/returnUrl.ts"], outdir: ".test-output/ir-history", bundle: true, platform: "node", format: "esm", outExtension: { ".js": ".mjs" } });
const { decodeHistory, decodeTimeline, readHistory, HistoryFailure } = await import("../.test-output/ir-history/internalRequests/historyApi.mjs");
const { approvedReturnDestination } = await import("../.test-output/ir-history/shared-host/returnUrl.mjs");
const page = () => ({ items: [{ id: 1, number: "IR1", branchId: 2, receivingDepartmentId: null, subject: "<script>literal</script>", status: "SUBMITTED", deadline: null, overdue: false, currentAssigneeId: null }], totalCount: 1, page: 1, pageSize: 25, snapshotUtc: "2026-11-16T00:00:00Z" });
test("history decoder rejects unsafe IDs, malformed counts and non-UTC dates", () => {
  assert.equal(decodeHistory(page()).items[0].subject, "<script>literal</script>");
  for (const value of [{ ...page(), totalCount: -1 }, { ...page(), pageSize: 101 }, { ...page(), snapshotUtc: "2026-11-16" }, { ...page(), items: [{ ...page().items[0], id: 1e20 }] }, { ...page(), items: [{ ...page().items[0], subject: {} }] }]) assert.throws(() => decodeHistory(value));
});
test("timeline decoder binds identity and rejects malformed history", () => {
  const value = { requestId: 1, eventTotalCount: 0, assignmentTotalCount: 0, events: [], assignments: [], feedback: null };
  assert.equal(decodeTimeline(value, 1).requestId, 1);
  assert.throws(() => decodeTimeline(value, 2));
  assert.throws(() => decodeTimeline({ ...value, assignments: [{}] }, 1));
  assert.throws(() => decodeTimeline({ ...value, feedback: { comment: {} } }, 1));
});
test("history transport carries cookies, cancellation and no-store without mutations", async () => {
  const original = globalThis.fetch, controller = new AbortController();
  try {
    globalThis.fetch = async (url, options) => {
      assert.equal(url, "/api/internal-requests?page=1"); assert.equal(options.credentials, "same-origin");
      assert.equal(options.cache, "no-store"); assert.equal(options.signal, controller.signal); assert.equal(options.method, undefined);
      return new Response(JSON.stringify(page()), { headers: { "content-type": "application/json" } });
    };
    assert.equal(decodeHistory(await readHistory("?page=1", controller.signal)).totalCount, 1);
    globalThis.fetch = async () => new Response("", { status: 403 });
    await assert.rejects(readHistory("", controller.signal), e => e instanceof HistoryFailure && e.status === 403);
  } finally { globalThis.fetch = original; }
});
test("history return destination requires revalidation and accepts no extra redirect parameters", () => {
  assert.equal(approvedReturnDestination("/internal-requests", true), "/internal-requests");
  assert.equal(approvedReturnDestination("/internal-requests", false), null);
  assert.equal(approvedReturnDestination("/internal-requests?requestId=42", true), "/internal-requests?requestId=42");
  for (const path of ["/internal-requests?requestId=0", "/internal-requests?requestId=1&requestId=2", "/internal-requests?requestId=1&next=evil", "/internal-requests?requestId=9007199254740992"])
    assert.equal(approvedReturnDestination(path, true), null);
  assert.equal(approvedReturnDestination("/internal-requests?redirect=evil", true), null);
});
