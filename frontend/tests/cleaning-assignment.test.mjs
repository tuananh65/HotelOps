import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: ["src/api/dev1/cleaningAssignment.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/fs006/assignment.mjs",
  define: { "import.meta.env": "{}" },
});
const { readAssignmentPlan, buildAssignmentDraft } =
  await import("../.test-output/fs006/assignment.mjs");
const plan = {
  task: {
    id: 12,
    number: "API-CL-12",
    roomId: 3,
    roomNumber: "101",
    branchId: 7,
    recordedStatus: "PENDING",
    dueAt: "2026-11-06T12:00:00",
    workflowState: "PENDING_ASSIGNMENT",
  },
  candidates: [
    {
      id: 5,
      name: "API Staff",
      departmentId: 2,
      openTasks: 2,
      openTasksInWindow: 1,
    },
  ],
  recordedShifts: [
    {
      id: 1,
      userId: 5,
      from: "2026-11-06T00:00:00",
      to: "2026-11-06T08:00:00",
      recordedShiftStatus: "PLANNED",
      recordedAssignmentStatus: "ASSIGNED",
    },
  ],
  total: 1,
  filters: {
    search: null,
    from: "2026-11-06T00:00:00Z",
    to: "2026-11-07T00:00:00Z",
    page: 1,
    pageSize: 20,
  },
  checklist: null,
  capturedAt: "2026-11-06T00:00:00Z",
  workflowStateVerified: true,
};
test("assignment planning uses existing GET cookie transport with cancellation, no mutation", async () => {
  const abort = new AbortController();
  globalThis.fetch = async (url, options) => {
    assert.equal(options.method, "GET");
    assert.equal(options.credentials, "include");
    assert.equal(options.signal, abort.signal);
    assert.match(
      url,
      /\/api\/branches\/7\/cleaning-tasks\/12\/assignment-options\?/,
    );
    assert.equal(
      new URL(url, "https://localhost").searchParams.get("search"),
      "API Staff",
    );
    return Response.json(plan);
  };
  assert.equal(
    (await readAssignmentPlan("7", "12", 1, " API Staff ", abort.signal)).task
      .id,
    12,
  );
});
test("assignment plan rejects foreign identities, malformed candidates, unsafe IDs and unrelated shifts", async () => {
  for (const v of [
    { ...plan, task: { ...plan.task, branchId: 8 } },
    { ...plan, task: { ...plan.task, id: 13 } },
    {
      ...plan,
      candidates: [{ ...plan.candidates[0], id: Number.MAX_SAFE_INTEGER + 1 }],
    },
    { ...plan, recordedShifts: [{ ...plan.recordedShifts[0], userId: 6 }] },
    { ...plan, filters: { ...plan.filters, page: 2 } },
  ]) {
    globalThis.fetch = async () => Response.json(v);
    await assert.rejects(
      readAssignmentPlan("7", "12"),
      /INVALID_ASSIGNMENT_PLAN/,
    );
  }
});
test("assignment planning propagates real statuses once, without fallback or POST", async () => {
  for (const status of [400, 401, 403, 404, 409, 503]) {
    let count = 0;
    globalThis.fetch = async (_, o) => {
      assert.equal(o.method, "GET");
      count++;
      return Response.json({ title: "DENIED" }, { status });
    };
    await assert.rejects(
      readAssignmentPlan("7", "12"),
      (e) => e.status === status,
    );
    assert.equal(count, 1);
  }
});
test("invalid planning scope/filter never fetches; abort remains cancellation", async () => {
  globalThis.fetch = () => {
    throw Error("must not fetch");
  };
  for (const args of [
    ["demo", "12"],
    ["7", "12", 0],
    ["7", "12", 1, "bad\nquery"],
  ])
    await assert.rejects(readAssignmentPlan(...args));
  const abort = new AbortController();
  abort.abort();
  globalThis.fetch = async () => {
    throw new DOMException("cancelled", "AbortError");
  };
  await assert.rejects(
    readAssignmentPlan("7", "12", 1, "", abort.signal),
    (e) => e.name === "AbortError",
  );
});
test("assignment draft maps exact existing request fields without network, timestamps or actor from mock", () => {
  globalThis.fetch = () => {
    throw Error("draft must not fetch");
  };
  const draft = buildAssignmentDraft(
    plan,
    "5",
    "2026-11-06T12:00:00Z",
    "  prepare only  ",
    Date.parse("2026-11-06T00:00:00Z"),
  );
  assert.deepEqual(draft, {
    assigneeId: 5,
    dueAt: "2026-11-06T12:00:00.000Z",
    reason: "prepare only",
    adoptUnassignedLegacy: false,
  });
});
test("draft never authorizes legacy adoption, reassignment, unknown candidate or invalid deadline/reason", () => {
  const now = Date.parse("2026-11-06T00:00:00Z");
  const build = (
    p = plan,
    user = "5",
    due = "2026-11-06T12:00:00Z",
    reason = "reason",
  ) => buildAssignmentDraft(p, user, due, reason, now);
  assert.throws(() => build({ ...plan, workflowStateVerified: false }));
  assert.throws(() =>
    build({ ...plan, task: { ...plan.task, workflowState: "ASSIGNED" } }),
  );
  assert.throws(() => build(plan, "6"));
  assert.throws(() => build(plan, "5", "2026-11-06T12:00:00"));
  assert.throws(() => build(plan, "5", "2026-11-05T12:00:00Z"));
  assert.throws(() => build(plan, "5", "2026-11-06T12:00:00Z", "bad\nreason"));
  assert.throws(() =>
    build(plan, "5", "2026-11-06T12:00:00Z", "x".repeat(501)),
  );
});
