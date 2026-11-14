import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import ts from "typescript";
const source = ts.transpileModule(
  readFileSync(
    new URL("../src/operationalTasks/api.ts", import.meta.url),
    "utf8",
  ),
  {
    compilerOptions: {
      module: ts.ModuleKind.ESNext,
      target: ts.ScriptTarget.ES2022,
    },
  },
).outputText;
const { readTasks, TaskReadFailure } = await import(
  "data:text/javascript;base64," + Buffer.from(source).toString("base64")
);
const task = {
  id: 4,
  code: "T4",
  branchId: 2,
  departmentId: 3,
  title: "Task",
  status: "TODO",
  progressPercent: 0,
  dueAt: null,
};
const page = { items: [task], totalCount: 1, page: 1, pageSize: 25 };
const signal = new AbortController().signal;
test("task read scopes URL and preserves credentials, cancellation and no-store", async (t) => {
  t.mock.method(globalThis, "fetch", async (url, options) => {
    assert.match(
      url,
      /^\/api\/branches\/2\/operational-tasks\?page=1&pageSize=25&search=/,
    );
    assert.equal(options.credentials, "same-origin");
    assert.equal(options.cache, "no-store");
    assert.equal(options.signal, signal);
    return Response.json(page);
  });
  assert.deepEqual(await readTasks(2, 1, "a & b", null, signal), page);
});
test("foreign branch data is rejected", async (t) => {
  t.mock.method(globalThis, "fetch", async () =>
    Response.json({ ...page, items: [{ ...task, branchId: 9 }] }),
  );
  await assert.rejects(readTasks(2, 1, "", null, signal), /Invalid task page/);
});
test("wrong detail identity is rejected", async (t) => {
  t.mock.method(globalThis, "fetch", async () =>
    Response.json({
      task,
      description: null,
      completionNote: null,
      progress: [],
      progressTotalCount: 0,
    }),
  );
  await assert.rejects(readTasks(2, 1, "", 5, signal), /Invalid task detail/);
});
test("unsafe IDs cause no network request", async (t) => {
  const fetch = t.mock.method(globalThis, "fetch", () =>
    assert.fail("network"),
  );
  await assert.rejects(
    readTasks(Number.MAX_SAFE_INTEGER + 1, 1, "", null, signal),
  );
  assert.equal(fetch.mock.callCount(), 0);
});
test("401 is a typed session failure", async (t) => {
  t.mock.method(
    globalThis,
    "fetch",
    async () => new Response(null, { status: 401 }),
  );
  await assert.rejects(
    readTasks(2, 1, "", null, signal),
    (e) => e instanceof TaskReadFailure && e.status === 401,
  );
});
test("invalid progress history cannot render", async (t) => {
  t.mock.method(globalThis, "fetch", async () =>
    Response.json({
      task,
      description: null,
      completionNote: null,
      progress: [
        {
          id: 1,
          reportedBy: 2,
          reportedAt: "2026-11-15T00:00:00Z",
          progressPercent: 101,
        },
      ],
      progressTotalCount: 1,
    }),
  );
  await assert.rejects(readTasks(2, 1, "", 4, signal), /Invalid task detail/);
});
