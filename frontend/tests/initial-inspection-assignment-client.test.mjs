import { test } from "node:test";
import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { build } from "esbuild";
await build({
  entryPoints: ["src/api/dev1/initialInspectionAssignment.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/initial-inspection-assignment.mjs",
  define: { "import.meta.env": "{}" },
});
const { readInitialInspectionOptions, assignInitialInspection } =
  await import("../.test-output/initial-inspection-assignment.mjs");
const tag = '"0000000000000001"',
  operation = randomUUID();
const options = () => ({
  taskId: 2,
  branchId: 1,
  roomId: 3,
  cycleETag: tag,
  checklistVersionId: 6,
  existingInspectorId: null,
  taskETag: tag,
  existingInspectionId: null,
  inspectors: [{ id: 7, name: "Supervisor" }],
  roomNumber: "101",
  checklistName: "Room inspection",
  versionNo: 1,
  canAssign: true,
  unavailableReason: null,
});
const command = {
  inspectorId: 7,
  checklistVersionId: 6,
  cycleETag: tag,
  dueAt: "2026-11-17T01:00:00Z",
  reason: "Inspect submitted rework",
};
const scope = () => ({
  signal: new AbortController().signal,
  isCurrent: () => true,
});
const receipt = () => ({
  id: 8,
  taskId: 2,
  branchId: 1,
  roomId: 3,
  versionId: 6,
  inspectorId: 7,
  state: "ASSIGNED",
  sourceETag: tag,
  operationId: operation,
  notification: "PENDING_DELIVERY",
  contract: "FE05_INITIAL_INSPECTION_ASSIGNMENT_V1",
});

test("assignment options require matching scope, ETag and actionable state", async () => {
  for (const patch of [
    null,
    { branchId: 99 },
    { taskId: 99 },
    { taskETag: "*" },
    { cycleETag: "*" },
    { checklistVersionId: null },
    { versionNo: null },
    { existingInspectionId: 8 },
    { inspectors: [] },
    {
      inspectors: [
        { id: 7, name: "A" },
        { id: 7, name: "B" },
      ],
    },
  ]) {
    globalThis.fetch = async () =>
      Response.json({ ...options(), ...patch }, { headers: { ETag: tag } });
    if (patch)
      await assert.rejects(
        readInitialInspectionOptions("1", "2", new AbortController().signal),
        /INVALID_INITIAL_INSPECTION_RESPONSE/,
      );
    else
      assert.equal(
        (
          await readInitialInspectionOptions(
            "1",
            "2",
            new AbortController().signal,
          )
        ).canAssign,
        true,
      );
  }
});

test("session replacement during CSRF bootstrap sends no assignment", async () => {
  let current = true;
  const calls = [];
  globalThis.fetch = async (url) => {
    calls.push(url);
    current = false;
    return Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "token" });
  };
  await assert.rejects(
    assignInitialInspection("1", "2", tag, operation, command, {
      signal: new AbortController().signal,
      isCurrent: () => current,
    }),
    /Session or source changed/,
  );
  assert.deepEqual(calls, ["/api/auth/csrf"]);
});

test("assignment transport binds CSRF, operation and rework ETag without automatic retry", async () => {
  const calls = [];
  globalThis.fetch = async (url, init) => {
    calls.push({ url, init });
    return url.endsWith("/csrf")
      ? Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "token" })
      : Response.json(receipt(), { status: 201, headers: { ETag: tag } });
  };
  assert.equal(
    (await assignInitialInspection("1", "2", tag, operation, command, scope()))
      .id,
    8,
  );
  assert.equal(calls.length, 2);
  const { url, init } = calls[1];
  assert.equal(url, "/api/branches/1/cleaning-tasks/2/inspections");
  assert.equal(init.credentials, "include");
  assert.equal(init.cache, "no-store");
  assert.equal(init.redirect, "error");
  assert.equal(init.headers["If-Match"], tag);
  assert.equal(init.headers["Idempotency-Key"], operation);
  assert.equal(init.headers["X-CSRF-TOKEN"], "token");
  assert.equal(init.headers.Authorization, undefined);
  assert.deepEqual(JSON.parse(init.body), command);
});

test("foreign or malformed writer receipts never publish success", async () => {
  for (const patch of [
    { branchId: 2 },
    { taskId: 3 },
    { operationId: "other" },
    { contract: "candidate" },
    { state: "PASSED" },
    { sourceETag: "*" },
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
      assignInitialInspection("1", "2", tag, operation, command, scope()),
      /INVALID_INITIAL_INSPECTION_RESPONSE/,
    );
    assert.equal(posts, 1);
  }
});

test("invalid command is rejected before any CSRF or mutation request", async () => {
  let calls = 0;
  globalThis.fetch = async () => {
    calls++;
    throw Error("Unexpected transport");
  };
  for (const patch of [
    { inspectorId: 0 },
    { reason: " " },
    { reason: "x".repeat(501) },
    { dueAt: "invalid" },
  ])
    await assert.rejects(
      assignInitialInspection(
        "1",
        "2",
        tag,
        operation,
        { ...command, ...patch },
        scope(),
      ),
      /INVALID_INITIAL_INSPECTION_COMMAND/,
    );
  await assert.rejects(
    assignInitialInspection(
      "1",
      "2",
      tag,
      [8, 4, 4, 4, 12].map((length) => "0".repeat(length)).join("-"),
      command,
      scope(),
    ),
    /INVALID_INITIAL_INSPECTION_COMMAND/,
  );
  assert.equal(calls, 0);
});
