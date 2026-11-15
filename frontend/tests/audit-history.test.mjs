import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: ["src/audit/auditHistory.ts"],
  outfile: ".test-output/auditHistory.mjs",
  bundle: true,
  platform: "node",
  format: "esm",
});
const { recordHistoryQuery, auditChanges, auditValue } =
  await import("../.test-output/auditHistory.mjs");
test("record history preserves branch scope but removes period actor action and search constraints", () => {
  const q = new URLSearchParams(
    recordHistoryQuery(
      { entityType: "rooms", entityId: "42&action=DELETE" },
      "branchId=7&from=2026-11-15&actorId=9&action=UPDATE&search=test&page=9",
    ),
  );
  assert.equal(q.get("entityId"), "42&action=DELETE");
  assert.equal(q.get("branchId"), "7");
  assert.equal(q.get("page"), "1");
  for (const key of ["from", "to", "actorId", "action", "search"])
    assert.equal(q.has(key), false);
});
test("field comparison preserves explicit null versus missing values", () => {
  const result = auditChanges(
    { status: "PENDING", closedAt: "2026-11-14", isActive: true },
    { status: "COMPLETED", closedAt: null, isActive: true },
  );
  assert.deepEqual(
    result.map((r) => r.field),
    ["closedAt", "status"],
  );
  assert.equal(auditValue(result[0].after, result[0].afterRecorded), "null");
});
test("creation values do not invent a before value", () => {
  const result = auditChanges(null, { status: "PENDING" });
  assert.equal(result[0].beforeRecorded, false);
  assert.equal(auditValue(result[0].before, false), "Not recorded or withheld");
});
test("redaction markers and nested payloads are not reconstructed as business changes", () => {
  assert.deepEqual(auditChanges({ withheld: true }, "[withheld]"), []);
  assert.deepEqual(auditChanges({ status: { secret: "opaque" } }, {}), []);
});
test("falsy recorded values remain distinguishable", () => {
  for (const v of [false, 0, ""]) assert.equal(auditValue(v, true), String(v));
});
