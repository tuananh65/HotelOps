import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import ts from "typescript";
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
const progressUrl =
  "data:text/javascript;base64," +
  Buffer.from(compile("progress")).toString("base64");
const source = compile("submission").replace(
  '"./progress"',
  JSON.stringify(progressUrl),
);
const { submitTask, readTaskSubmission } = await import(
  "data:text/javascript;base64," + Buffer.from(source).toString("base64")
);
const current = {
  data: {
    id: 4,
    branchId: 2,
    kind: "task",
    state: "IN_PROGRESS",
    percent: 40,
    items: [{ id: 1, label: "Required", required: true, outcome: null }],
    evidence: [],
  },
  etag: '"0000000000000001"',
};
const input = {
  items: [{ id: 1, outcome: "FAIL", note: null }],
  resultNote: "First\nSecond",
  handoverNote: null,
  photos: [],
};
const signal = new AbortController().signal;
test("submission binds criteria, multiline notes, CSRF and exact ETag without closing", async (t) => {
  const calls = [];
  t.mock.method(globalThis, "fetch", async (url, options) => {
    calls.push(url);
    assert.equal(options.credentials, "same-origin");
    assert.equal(options.cache, "no-store");
    assert.equal(options.redirect, "error");
    assert.equal(options.signal, signal);
    if (url === "/api/auth/csrf")
      return Response.json({
        headerName: "X-CSRF-TOKEN",
        csrfToken: "synthetic",
      });
    assert.equal(url, "/api/branches/2/operational-tasks/4/submit");
    assert.equal(options.headers["If-Match"], current.etag);
    assert.deepEqual(JSON.parse(options.body), input);
    return Response.json(
      { ...current.data, state: "PENDING_VERIFICATION" },
      { headers: { ETag: '"0000000000000002"' } },
    );
  });
  await submitTask(2, 4, current, input, signal);
  assert.equal(calls.length, 2);
});
test("paused, accepted, pending, foreign and stale-looking snapshots do not dispatch", async (t) => {
  const mock = t.mock.method(globalThis, "fetch", () => {
    throw new Error("Network forbidden");
  });
  for (const change of [
    { state: "PAUSED" },
    { state: "ACCEPTED" },
    { state: "PENDING_VERIFICATION" },
    { branchId: 3 },
  ])
    await assert.rejects(
      submitTask(
        2,
        4,
        { ...current, data: { ...current.data, ...change } },
        input,
        signal,
      ),
      /Invalid submission/,
    );
  await assert.rejects(
    submitTask(2, 4, { ...current, etag: "*" }, input, signal),
    /Invalid submission/,
  );
  assert.equal(mock.mock.callCount(), 0);
});
test("missing criteria remains a correctable business error and is never retried", async (t) => {
  const mock = t.mock.method(globalThis, "fetch", async (url) =>
    url === "/api/auth/csrf"
      ? Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "s" })
      : Response.json(
          { messageCode: "MSG03", missingCriteria: ["Required"] },
          { status: 400 },
        ),
  );
  await assert.rejects(
    submitTask(2, 4, current, input, signal),
    (e) =>
      e.status === 400 &&
      e.messageCode === "MSG03" &&
      e.missingCriteria[0] === "Required",
  );
  assert.equal(mock.mock.callCount(), 2);
});
test("success requires pending verification and a new strong version", async (t) => {
  for (const [state, etag] of [
    ["COMPLETED", '"0000000000000002"'],
    ["PENDING_VERIFICATION", current.etag],
  ]) {
    const mock = t.mock.method(globalThis, "fetch", async (url) =>
      url === "/api/auth/csrf"
        ? Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "s" })
        : Response.json(
            { ...current.data, state },
            { headers: { ETag: etag } },
          ),
    );
    await assert.rejects(
      submitTask(2, 4, current, input, signal),
      /could not be confirmed/,
    );
    mock.mock.restore();
  }
});
test("review rejects a snapshot with foreign identity or an unscoped evidence link", async (t) => {
  const data = {
    id: 8,
    taskId: 4,
    branchId: 2,
    submittedBy: 7,
    submittedAt: "2026-11-15T11:00:00Z",
    resultNote: null,
    handoverNote: null,
    criteria: [],
    evidence: [],
  };
  for (const body of [
    { ...data, branchId: 3 },
    {
      ...data,
      evidence: [
        {
          id: 9,
          entityType: "TASK",
          entityId: 4,
          name: "proof.png",
          downloadPath: "https://foreign.invalid/file",
        },
      ],
    },
  ]) {
    const mock = t.mock.method(globalThis, "fetch", async () =>
      Response.json(body),
    );
    await assert.rejects(
      readTaskSubmission(2, 4, signal),
      /Invalid task submission response/,
    );
    mock.mock.restore();
  }
});
