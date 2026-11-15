import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: ["src/api/dev1/reinspectionOriginal.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/reinspection-original.mjs",
  define: { "import.meta.env": "{}" },
});
const { readReinspectionOriginal, reinspectionOriginalEvidenceUrl } =
  await import("../.test-output/reinspection-original.mjs");
const tag = '"0000000000000001"';
const source = () => ({
  reinspectionId: 8,
  reworkId: 2,
  branchId: 1,
  id: 5,
  roomId: 3,
  cleaningTaskId: 4,
  inspectorId: 7,
  versionId: 6,
  state: "FAILED",
  sourceETag: tag,
  summary: "Original rejection",
  criteria: [
    {
      itemId: 10,
      criterion: "Clean",
      description: null,
      outcome: "FAIL",
      note: "Dust",
      evidenceIds: [12],
    },
  ],
  defects: [
    {
      id: 9,
      itemId: 10,
      description: "Dust",
      location: "Desk",
      evidenceIds: [12],
    },
  ],
  evidence: [
    {
      id: 12,
      entityType: "INSPECTION",
      entityId: 5,
      name: "photo.png",
      downloadPath: "/api/branches/1/reinspections/8/original/evidence/12",
    },
  ],
});
test("original read accepts a different original inspector and only a locally scoped file URL", async () => {
  const original = global.fetch;
  try {
    global.fetch = async (url, options) => {
      assert.equal(url, "/api/branches/1/reinspections/8/original");
      assert.equal(options.method, "GET");
      return new Response(JSON.stringify(source()), {
        headers: { ETag: tag, "Content-Type": "application/json" },
      });
    };
    assert.equal(
      (await readReinspectionOriginal("1", "8", new AbortController().signal))
        .inspectorId,
      7,
    );
    assert.equal(
      reinspectionOriginalEvidenceUrl("1", "8", 12),
      source().evidence[0].downloadPath,
    );
    assert.throws(() => reinspectionOriginalEvidenceUrl("1", "8", -1));
  } finally {
    global.fetch = original;
  }
});
test("original read rejects substituted identities, out-of-scope evidence, malformed results and stale header", async () => {
  const original = global.fetch;
  try {
    for (const mutate of [
      (d) => (d.branchId = 2),
      (d) => (d.reinspectionId = 9),
      (d) => (d.id = 8),
      (d) => (d.state = "PASSED"),
      (d) => (d.sourceETag = '"0000000000000002"'),
      (d) => (d.criteria = null),
      (d) => (d.criteria[0].outcome = "UNVERIFIED"),
      (d) => d.criteria.push(d.criteria[0]),
      (d) => (d.defects[0].itemId = 999),
      (d) => (d.defects[0].evidenceIds = [99]),
      (d) => (d.evidence[0].entityId = 8),
      (d) => (d.evidence[0].entityType = "CLEANING_TASK"),
      (d) => (d.evidence[0].downloadPath = "https://evil.invalid/photo"),
      (d) => d.evidence.push(d.evidence[0]),
    ]) {
      const data = source();
      mutate(data);
      global.fetch = async () =>
        new Response(JSON.stringify(data), {
          headers: { ETag: tag, "Content-Type": "application/json" },
        });
      await assert.rejects(
        readReinspectionOriginal("1", "8", new AbortController().signal),
        (error) => error.status === 502,
      );
    }
  } finally {
    global.fetch = original;
  }
});
