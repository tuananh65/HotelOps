import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: ["src/api/dev1/reworkExecution.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/fs016/rework.mjs",
  define: { "import.meta.env": "{}" },
});
const { readRework, mutateRework } =
  await import("../.test-output/fs016/rework.mjs");
const tag = '"0000000000000001"',
  operation = "11111111-1111-4111-8111-111111111111";
const source = {
  id: 3,
  branchId: 2,
  cleaningTaskId: 4,
  inspectionId: 5,
  roomId: 6,
  assigneeId: 7,
  state: "PENDING_ACCEPTANCE",
  dueAtUtc: null,
  sourceETag: tag,
  inspectionETag: tag,
  capability: "PROVISIONAL_NOT_ACCEPTED",
  sourceAuthority: "SYNTHETIC_TEST_ONLY",
  defects: [
    {
      id: 8,
      description: "Synthetic defect",
      remediation: null,
      inspectionAttachmentIds: [],
      remediationAttachmentIds: [],
    },
  ],
};
test("rework read binds identity, ETag and provisional source, cookie transport and cancellation", async () => {
  const abort = new AbortController();
  globalThis.fetch = async (url, opts) => {
    assert.match(url, /\/branches\/2\/reworks\/3\/execution$/);
    assert.equal(opts.credentials, "include");
    assert.equal(opts.signal, abort.signal);
    return Response.json(source, { headers: { ETag: tag } });
  };
  assert.equal((await readRework("2", "3", abort.signal)).inspectionId, 5);
  for (const bad of [
    { ...source, id: 4 },
    { ...source, sourceAuthority: "ACCEPTED" },
    { ...source, defects: [...source.defects, ...source.defects] },
    { ...source, state: "READY" },
  ]) {
    globalThis.fetch = async () =>
      Response.json(bad, { headers: { ETag: tag } });
    await assert.rejects(readRework("2", "3"), /INVALID_REWORK_RESPONSE/);
  }
});
test("rework mutations use current CSRF with original key, ETag and immutable body on explicit retry", async () => {
  const payloads = [];
  globalThis.fetch = async (url, opts) => {
    if (url.endsWith("/auth/csrf"))
      return Response.json({
        headerName: "X-CSRF-TOKEN",
        csrfToken: "rotated",
      });
    payloads.push(opts.body);
    assert.equal(opts.credentials, "include");
    assert.equal(opts.headers["X-CSRF-TOKEN"], "rotated");
    assert.equal(opts.headers["If-Match"], tag);
    assert.equal(opts.headers["Idempotency-Key"], operation);
    return Response.json(
      {
        id: 3,
        branchId: 2,
        cleaningTaskId: 4,
        inspectionId: 5,
        assigneeId: 7,
        action: "accept",
        state: "ACCEPTED",
        operationId: operation,
        sourceETag: tag,
        occurredAtUtc: "2026-11-07T00:00:00Z",
        intervalId: null,
        publicationId: null,
        readiness: "REWORK_REQUIRED",
        notification: "NOT_REQUIRED",
        contract: "PROVISIONAL_NOT_ACCEPTED",
      },
      { headers: { ETag: tag } },
    );
  };
  const command = { note: "Accept synthetic rework" };
  await mutateRework("2", "3", "accept", tag, operation, command);
  await mutateRework("2", "3", "accept", tag, operation, command);
  assert.equal(payloads[0], payloads[1]);
});
test("rework refuses malformed command and missing per-defect remediation before network", async () => {
  globalThis.fetch = () => {
    throw Error("must not fetch");
  };
  for (const command of [
    { note: " " },
    { note: "Submit", remediations: [] },
    { note: "Submit", remediations: [{ defectId: 8, note: " " }] },
    {
      note: "Submit",
      remediations: [
        { defectId: 8, note: "Fixed" },
        { defectId: 8, note: "duplicate" },
      ],
    },
  ])
    await assert.rejects(
      mutateRework("2", "3", "complete", tag, operation, command),
    );
  await assert.rejects(
    mutateRework("2", "3", "accept", "*", operation, { note: "Accept" }),
  );
});
test("rework never retries automatically or falls back on auth, stale or unavailable responses", async () => {
  for (const status of [401, 403, 404, 409, 412, 503]) {
    let writes = 0;
    globalThis.fetch = async (url) => {
      if (url.endsWith("/auth/csrf"))
        return Response.json({
          headerName: "X-CSRF-TOKEN",
          csrfToken: "token",
        });
      writes++;
      return Response.json({ title: "DENIED" }, { status });
    };
    await assert.rejects(
      mutateRework("2", "3", "accept", tag, operation, { note: "Accept" }),
      (e) => e.status === status,
    );
    assert.equal(writes, 1);
  }
});
