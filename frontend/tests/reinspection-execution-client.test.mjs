import { test } from "node:test";
import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { build } from "esbuild";
await build({
  entryPoints: ["src/api/dev1/reinspectionExecution.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/reinspection-execution.mjs",
  define: { "import.meta.env": "{}" },
});
const { readReinspection, concludeReinspection } =
  await import("../.test-output/reinspection-execution.mjs");
const tag = '"0000000000000001"',
  operation = randomUUID();
const source = () => ({
  id: 8,
  reworkId: 2,
  branchId: 1,
  roomId: 3,
  cleaningTaskId: 4,
  originalInspectionId: 5,
  inspectorId: 7,
  versionId: 6,
  state: "ASSIGNED",
  sourceETag: tag,
  reworkETag: tag,
  bindingETag: tag,
  defects: [
    {
      id: 9,
      description: "Defect",
      remediation: "Repaired",
      fixed: null,
      note: null,
      originalPhotos: [10],
      remediationPhotos: [11],
      reinspectionPhotos: [],
    },
  ],
  effectiveReadiness: "BLOCKED",
  messageCode: "MSG17",
});
const decisions = [{ defectId: 9, fixed: true, note: "Verified", photos: [] }];
const receipt = () => ({
  id: 8,
  reworkId: 2,
  branchId: 1,
  roomId: 3,
  cleaningTaskId: 4,
  state: "PASSED",
  reworkState: "COMPLETED",
  sourceETag: tag,
  reworkETag: tag,
  operationId: operation,
  effectiveReadiness: "BLOCKED",
  messageCode: "MSG17",
  notification: "PENDING_DELIVERY",
  contract: "FE05_REINSPECTION_CONCLUSION_V1",
});
const scope = () => ({
  signal: new AbortController().signal,
  isCurrent: () => true,
});
test("execution rejects foreign, duplicate, already decided or unversioned source", async () => {
  for (const patch of [
    null,
    { id: 7 },
    { branchId: 2 },
    { bindingETag: "*" },
    { messageCode: null },
    { defects: [source().defects[0], source().defects[0]] },
    { defects: [{ ...source().defects[0], fixed: true }] },
  ]) {
    globalThis.fetch = async () =>
      Response.json({ ...source(), ...patch }, { headers: { ETag: tag } });
    if (patch)
      await assert.rejects(
        readReinspection("1", "8", scope().signal),
        /INVALID_REINSPECTION_RESPONSE/,
      );
    else assert.equal((await readReinspection("1", "8", scope().signal)).id, 8);
  }
});
test("replacement authority during CSRF bootstrap cannot send conclusion", async () => {
  let current = true,
    calls = 0;
  globalThis.fetch = async () => {
    ++calls;
    current = false;
    return Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "token" });
  };
  await assert.rejects(
    concludeReinspection("1", "8", source(), operation, "Checked", decisions, {
      ...scope(),
      isCurrent: () => current,
    }),
    /Session or source changed/,
  );
  assert.equal(calls, 1);
});
test("conclusion binds exact source, three ETags, CSRF and operation without retry", async () => {
  const calls = [];
  globalThis.fetch = async (url, init) => {
    calls.push({ url, init });
    return url.endsWith("/csrf")
      ? Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "token" })
      : Response.json(receipt(), { headers: { ETag: tag } });
  };
  assert.equal(
    (
      await concludeReinspection(
        "1",
        "8",
        source(),
        operation,
        "Checked",
        decisions,
        scope(),
      )
    ).state,
    "PASSED",
  );
  assert.equal(calls.length, 2);
  const { url, init } = calls[1];
  assert.equal(url, "/api/branches/1/reinspections/8/conclude");
  assert.equal(init.headers["If-Match"], tag);
  assert.equal(init.headers["Idempotency-Key"], operation);
  assert.equal(init.headers["X-CSRF-TOKEN"], "token");
  assert.equal(init.credentials, "include");
  assert.deepEqual(JSON.parse(init.body), {
    reworkETag: tag,
    bindingETag: tag,
    reason: "Checked",
    defects: decisions,
  });
});
test("incoherent receipt never publishes success or retries a write", async () => {
  for (const patch of [
    { id: 99 },
    { roomId: 99 },
    { operationId: randomUUID() },
    { sourceETag: "*" },
    { contract: "candidate" },
    { state: "FAILED" },
    { reworkState: "FAILED" },
    { effectiveReadiness: "OUT_OF_SERVICE" },
    { messageCode: "MSG08" },
    { notification: "DELIVERED" },
  ]) {
    let posts = 0;
    globalThis.fetch = async (url) =>
      url.endsWith("/csrf")
        ? Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "token" })
        : (++posts,
          Response.json(
            { ...receipt(), ...patch },
            { headers: { ETag: tag } },
          ));
    await assert.rejects(
      concludeReinspection(
        "1",
        "8",
        source(),
        operation,
        "Checked",
        decisions,
        scope(),
      ),
      /INVALID_REINSPECTION_RESPONSE/,
    );
    assert.equal(posts, 1);
  }
});
test("missing, duplicate or foreign decisions cannot start CSRF or mutation", async () => {
  let calls = 0;
  globalThis.fetch = async () => {
    ++calls;
    throw Error("Unexpected network");
  };
  for (const values of [
    [],
    [...decisions, ...decisions],
    [{ ...decisions[0], defectId: 99 }],
  ])
    await assert.rejects(
      concludeReinspection(
        "1",
        "8",
        source(),
        operation,
        "Checked",
        values,
        scope(),
      ),
      /INVALID_REINSPECTION_COMMAND/,
    );
  assert.equal(calls, 0);
});
