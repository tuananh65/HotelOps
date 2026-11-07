import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: ["src/api/dev1/cleaningExecution.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/fs013/execution.mjs",
  define: { "import.meta.env": "{}" },
});
const { readCleaningExecution, reassignmentCommand, mutateCleaningExecution } =
  await import("../.test-output/fs013/execution.mjs");
const tag = '"0000000000000001"',
  key = "12c44400-7ca4-4da1-8990-8d2fc258f211";
const plan = {
  id: 2,
  branchId: 1,
  taskNumber: "CL-2",
  state: "ASSIGNED",
  assignmentId: 4,
  assigneeId: 5,
  actualStartAtUtc: null,
  sourceETag: tag,
  startCapability: "UNAVAILABLE",
  reassignCapability: "PROVISIONAL_NOT_ACCEPTED",
};
test("UC030 accepts only a different assignee, confirmed capability, UTC future deadline and override reason", () => {
  assert.deepEqual(
    reassignmentCommand(
      plan,
      "6",
      "2027-01-01T00:00:00Z",
      "  staffing change  ",
      0,
    ),
    {
      assigneeId: 6,
      dueAt: "2027-01-01T00:00:00.000Z",
      reason: "staffing change",
    },
  );
  for (const change of [
    { state: "PENDING_INSPECTION" },
    { reassignCapability: "UNAVAILABLE" },
    { assigneeId: 6 },
  ])
    assert.throws(() =>
      reassignmentCommand(
        { ...plan, ...change },
        "6",
        "2027-01-01T00:00:00Z",
        "staffing change",
        0,
      ),
    );
  assert.throws(() =>
    reassignmentCommand(plan, "6", "2027-01-01T00:00:00", "staffing change", 0),
  );
  assert.throws(() =>
    reassignmentCommand(plan, "6", "2027-01-01T00:00:00Z", "short", 0),
  );
});
test("Lifecycle read rejects wrong branch, invented capability and mismatched ETag", async () => {
  for (const patch of [
    { branchId: 9 },
    { startCapability: "YES" },
    { sourceETag: '"0000000000000002"' },
  ]) {
    globalThis.fetch = async () =>
      Response.json({ ...plan, ...patch }, { headers: { ETag: tag } });
    await assert.rejects(
      readCleaningExecution("1", "2"),
      (e) => e.status === 502,
    );
  }
});
test("Reassign retains exact key/body/original ETag and makes one write when notification is unavailable", async () => {
  const command = {
    assigneeId: 6,
    dueAt: "2027-01-01T00:00:00Z",
    reason: "staffing change",
  };
  let writes = 0;
  globalThis.fetch = async (url, init) => {
    assert.equal(init.credentials, "include");
    if (url.endsWith("/auth/csrf"))
      return Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "fresh" });
    writes++;
    assert.ok(url.endsWith("/reassign"));
    assert.equal(init.headers["If-Match"], tag);
    assert.equal(init.headers["Idempotency-Key"], key);
    assert.deepEqual(JSON.parse(init.body), command);
    return Response.json({ title: "UNAVAILABLE" }, { status: 503 });
  };
  await assert.rejects(
    mutateCleaningExecution("1", "2", "reassign", tag, command, key),
    (e) => e.status === 503,
  );
  assert.equal(writes, 1);
});
test("Start sends no client actor/time and validates server interval result", async () => {
  globalThis.fetch = async (url, init) => {
    if (url.endsWith("/auth/csrf"))
      return Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "fresh" });
    assert.deepEqual(JSON.parse(init.body), { resolveBlocker: false });
    assert.equal(init.headers["Idempotency-Key"], undefined);
    return Response.json(
      {
        id: 2,
        branchId: 1,
        assignmentId: 4,
        state: "IN_PROGRESS",
        intervalId: 8,
        actualStartAtUtc: "2026-11-07T00:00:00Z",
      },
      { headers: { ETag: tag } },
    );
  };
  assert.equal(
    (
      await mutateCleaningExecution("1", "2", "start", tag, {
        resolveBlocker: false,
      })
    ).intervalId,
    8,
  );
});

test("Closure capabilities fail closed for old metadata and reject invented availability", async () => {
  globalThis.fetch = async () =>
    Response.json(plan, { headers: { ETag: tag } });
  const source = await readCleaningExecution("1", "2");
  assert.equal(source.reportBlockerCapability, "UNAVAILABLE");
  assert.equal(source.completeCapability, "UNAVAILABLE");
  assert.equal(source.resumeCapability, "UNAVAILABLE");
  for (const patch of [
    { reportBlockerCapability: "AVAILABLE" },
    { completeCapability: "AVAILABLE" },
    { resumeCapability: "AVAILABLE" },
  ]) {
    globalThis.fetch = async () =>
      Response.json({ ...plan, ...patch }, { headers: { ETag: tag } });
    await assert.rejects(
      readCleaningExecution("1", "2"),
      (e) => e.status === 502,
    );
  }
});

test("Resume sends explicit confirmation and note through existing CSRF transport", async () => {
  let writes = 0;
  globalThis.fetch = async (url, init) => {
    if (url.endsWith("/auth/csrf"))
      return Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "fresh" });
    writes++;
    assert.ok(url.endsWith("/start"));
    assert.equal(init.headers["If-Match"], tag);
    assert.equal(init.headers["X-CSRF-TOKEN"], "fresh");
    assert.deepEqual(JSON.parse(init.body), {
      resolveBlocker: true,
      note: "Guest has left",
    });
    return Response.json(
      {
        id: 2,
        branchId: 1,
        assignmentId: 4,
        state: "IN_PROGRESS",
        intervalId: 9,
        actualStartAtUtc: "2026-11-07T00:00:00Z",
      },
      { headers: { ETag: tag } },
    );
  };
  assert.equal(
    (
      await mutateCleaningExecution("1", "2", "start", tag, {
        resolveBlocker: true,
        note: "Guest has left",
      })
    ).intervalId,
    9,
  );
  assert.equal(writes, 1);
});

test("Resume rejects invalid resolution notes before any network request", async () => {
  globalThis.fetch = async () => {
    throw Error("UNEXPECTED_NETWORK");
  };
  for (const note of [undefined, "", " ", "bad\nnote", "x".repeat(501)])
    await assert.rejects(
      mutateCleaningExecution("1", "2", "start", tag, {
        resolveBlocker: true,
        note,
      }),
      /INVALID_BLOCKER_RESOLUTION_NOTE/,
    );
});
