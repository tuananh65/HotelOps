import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import ts from "typescript";
const url = (text) =>
  "data:text/javascript;base64," + Buffer.from(text).toString("base64");
const compile = (name) =>
  ts.transpileModule(
    readFileSync(
      new URL(`../src/operationalTasks/${name}.ts`, import.meta.url),
      "utf8",
    ),
    {
      compilerOptions: {
        module: ts.ModuleKind.ESNext,
        target: ts.ScriptTarget.ES2022,
      },
    },
  ).outputText;
const api = url(compile("api")),
  progress = url(compile("progress"));
const planning = url(
  compile("planning")
    .replace('"./progress"', JSON.stringify(progress))
    .replace('"./api"', JSON.stringify(api)),
);
const { readReopen, reopenTask } = await import(
  url(
    compile("reopen")
      .replace('"./planning"', JSON.stringify(planning))
      .replace('"./api"', JSON.stringify(api)),
  )
);
const { readTaskSubmission } = await import(
  url(compile("submission").replace('"./progress"', JSON.stringify(progress)))
);
const signal = new AbortController().signal;
const data = {
  taskId: 7,
  branchId: 1,
  state: "CLOSED",
  canReopen: true,
  previousAssigneeId: 5,
  executors: [
    {
      userId: 5,
      name: "Synthetic executor",
      openTasks: 0,
      loggedSecondsToday: 30,
    },
  ],
  totalCount: 1,
  page: 1,
  pageSize: 25,
  history: [],
};
const snapshot = { data, etag: '"0000000000000001"' };
test("Reopen reads reject authority scope, malformed revisions and inconsistent candidates", async (t) => {
  for (const bad of [
    { ...data, branchId: 2 },
    { ...data, state: "IN_PROGRESS" },
    { ...data, canReopen: false },
    { ...data, previousAssigneeId: null },
    { ...data, executors: [...data.executors, ...data.executors] },
    { ...data, history: [{ previousSubmissionId: 0 }] },
  ]) {
    t.mock.method(globalThis, "fetch", async () =>
      Response.json(bad, { headers: { ETag: snapshot.etag } }),
    );
    await assert.rejects(readReopen(1, 7, 1, "", signal));
  }
  t.mock.method(globalThis, "fetch", async () =>
    Response.json(data, { headers: { ETag: 'W/"0000000000000001"' } }),
  );
  await assert.rejects(readReopen(1, 7, 1, "", signal));
});
test("Reopen allows same assignee but requires reason instructions and current Closed snapshot", async (t) => {
  let writes = 0;
  t.mock.method(globalThis, "fetch", async (path, init) => {
    assert.equal(init.credentials, "same-origin");
    assert.equal(init.cache, "no-store");
    assert.equal(init.redirect, "error");
    if (path === "/api/auth/csrf")
      return Response.json({
        headerName: "X-CSRF-TOKEN",
        csrfToken: "synthetic",
      });
    writes++;
    assert.equal(path, "/api/branches/1/operational-tasks/7/reopen");
    assert.equal(init.method, "POST");
    assert.equal(init.headers["If-Match"], snapshot.etag);
    assert.equal(init.headers["X-CSRF-TOKEN"], "synthetic");
    assert.deepEqual(JSON.parse(init.body), {
      assigneeId: 5,
      reason: "Result needs remediation",
      instructions: "Repair\nInspect",
    });
    return Response.json(
      {
        taskId: 7,
        branchId: 1,
        assigneeId: 5,
        state: "ASSIGNED",
        messageCode: "MSG08",
      },
      { headers: { ETag: '"0000000000000002"' } },
    );
  });
  await reopenTask(
    1,
    7,
    snapshot,
    5,
    " Result needs remediation ",
    "Repair\nInspect",
    signal,
  );
  assert.equal(writes, 1);
  for (const [assignee, reason, instructions] of [
    [0, "Valid long reason", "Repair"],
    [6, "Valid long reason", "Repair"],
    [5, "short", "Repair"],
    [5, "Valid long reason", " \n "],
    [5, "Valid long reason", "x".repeat(4001)],
  ])
    await assert.rejects(
      reopenTask(1, 7, snapshot, assignee, reason, instructions, signal),
    );
  await assert.rejects(
    reopenTask(
      1,
      7,
      { ...snapshot, data: { ...data, state: "ASSIGNED" } },
      5,
      "Valid long reason",
      "Repair",
      signal,
    ),
  );
  assert.equal(writes, 1);
});
test("Stale or ambiguous Reopen never retries or fabricates success", async (t) => {
  for (const status of [412, 503, 200]) {
    let writes = 0;
    t.mock.method(globalThis, "fetch", async (path) => {
      if (path === "/api/auth/csrf")
        return Response.json({
          headerName: "X-CSRF-TOKEN",
          csrfToken: "synthetic",
        });
      writes++;
      return Response.json(
        {
          taskId: 7,
          branchId: 1,
          assigneeId: 5,
          state: "ASSIGNED",
          messageCode: "MSG08",
        },
        { status, headers: { ETag: snapshot.etag } },
      );
    });
    await assert.rejects(
      reopenTask(1, 7, snapshot, 5, "Valid long reason", "Repair", signal),
    );
    assert.equal(writes, 1);
  }
});
test("Reopen refuses malformed CSRF and cancellation before mutation", async (t) => {
  let writes = 0;
  t.mock.method(globalThis, "fetch", async (path) => {
    if (path !== "/api/auth/csrf") writes++;
    return Response.json({ headerName: "Wrong", csrfToken: "synthetic" });
  });
  await assert.rejects(
    reopenTask(1, 7, snapshot, 5, "Valid long reason", "Repair", signal),
  );
  assert.equal(writes, 0);
});
test("Historical submission remains bound to requested task cycle and evidence path", async (t) => {
  const historical = {
    id: 9,
    taskId: 7,
    branchId: 1,
    submittedBy: 5,
    submittedAt: "2026-11-15T10:00:00Z",
    resultNote: "Original result",
    handoverNote: null,
    criteria: [],
    evidence: [
      {
        id: 12,
        entityType: "TASK",
        entityId: 7,
        name: "proof.png",
        downloadPath:
          "/api/branches/1/operational-tasks/7/submission/evidence/12?submission=9",
      },
    ],
  };
  t.mock.method(globalThis, "fetch", async (path) => {
    assert.equal(
      path,
      "/api/branches/1/operational-tasks/7/submission?submission=9",
    );
    return Response.json(historical);
  });
  assert.equal(
    (await readTaskSubmission(1, 7, signal, 9)).resultNote,
    "Original result",
  );
  for (const bad of [
    { ...historical, id: 10 },
    { ...historical, taskId: 8 },
    {
      ...historical,
      evidence: [{ ...historical.evidence[0], downloadPath: "/other" }],
    },
  ]) {
    t.mock.method(globalThis, "fetch", async () => Response.json(bad));
    await assert.rejects(readTaskSubmission(1, 7, signal, 9));
  }
});
