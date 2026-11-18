import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import ts from "typescript";
const source = ts.transpileModule(
  readFileSync(
    new URL("../src/operationalTasks/progress.ts", import.meta.url),
    "utf8",
  ),
  {
    compilerOptions: {
      module: ts.ModuleKind.ESNext,
      target: ts.ScriptTarget.ES2022,
    },
  },
).outputText;
const { enterTask, saveTaskProgress, readTaskProgress, TaskProgressError } =
  await import(
    "data:text/javascript;base64," + Buffer.from(source).toString("base64")
  );
const signal = new AbortController().signal;
const current = {
  data: {
    id: 4,
    branchId: 2,
    kind: "task",
    state: "ASSIGNED",
    percent: 0,
    items: [],
    evidence: [],
  },
  etag: '"0000000000000001"',
};
const csrf = { headerName: "X-CSRF-TOKEN", csrfToken: "synthetic" };
const response = (data, etag = '"0000000000000002"') =>
  Response.json(data, { headers: { ETag: etag } });
for (const [action, from, to] of [
  ["accept", "ASSIGNED", "ACCEPTED"],
  ["start", "ACCEPTED", "IN_PROGRESS"],
]) {
  test(`${action} sends fresh CSRF and exact version, validates committed state`, async (t) => {
    const calls = [];
    t.mock.method(globalThis, "fetch", async (url, options) => {
      calls.push(url);
      assert.equal(options.signal, signal);
      assert.equal(options.credentials, "same-origin");
      assert.equal(options.redirect, "error");
      assert.equal(options.cache, "no-store");
      if (url === "/api/auth/csrf") return Response.json(csrf);
      assert.equal(url, `/api/branches/2/operational-tasks/4/${action}`);
      assert.equal(options.method, "POST");
      assert.equal(options.headers["X-CSRF-TOKEN"], "synthetic");
      assert.equal(options.headers["If-Match"], current.etag);
      return response({ ...current.data, state: to });
    });
    const result = await enterTask(
      2,
      4,
      { ...current, data: { ...current.data, state: from } },
      action,
      signal,
    );
    assert.equal(result.data.state, to);
    assert.equal(calls.length, 2);
  });
}
test("entry rejects invalid action, state or version before network", async (t) => {
  const mock = t.mock.method(globalThis, "fetch", () => {
    throw new Error("Network must not run");
  });
  for (const [action, state, etag] of [
    ["start", "ASSIGNED", current.etag],
    ["accept", "IN_PROGRESS", current.etag],
    ["accept", "ASSIGNED", "*"],
    ["complete", "ASSIGNED", current.etag],
  ])
    await assert.rejects(
      enterTask(
        2,
        4,
        { ...current, etag, data: { ...current.data, state } },
        action,
        signal,
      ),
      /Chuyển trạng thái bắt đầu công việc không hợp lệ/,
    );
  assert.equal(mock.mock.callCount(), 0);
});
test("prestart progress is readable but cannot be saved", async (t) => {
  for (const state of ["ASSIGNED", "ACCEPTED"]) {
    const mock = t.mock.method(globalThis, "fetch", async () =>
      response({ ...current.data, state }),
    );
    assert.equal((await readTaskProgress(2, 4, signal)).data.state, state);
    await assert.rejects(
      saveTaskProgress(
        2,
        4,
        { ...current, data: { ...current.data, state } },
        { percent: 40, items: [], note: null },
        signal,
      ),
      /Dữ liệu tiến độ công việc không hợp lệ/,
    );
    assert.equal(mock.mock.callCount(), 1);
    mock.mock.restore();
  }
});
test("entry rejects a successful HTTP response without changed state and version", async (t) => {
  for (const [state, etag] of [
    ["ASSIGNED", '"0000000000000002"'],
    ["ACCEPTED", current.etag],
  ]) {
    const mock = t.mock.method(globalThis, "fetch", async (url) =>
      url === "/api/auth/csrf"
        ? Response.json(csrf)
        : response({ ...current.data, state }, etag),
    );
    await assert.rejects(
      enterTask(2, 4, current, "accept", signal),
      /Chưa xác nhận bắt đầu công việc/,
    );
    mock.mock.restore();
  }
});
test("workday and occupied interval errors preserve approved message codes", async (t) => {
  for (const messageCode of ["MSG30", "MSG31"]) {
    const mock = t.mock.method(globalThis, "fetch", async (url) =>
      url === "/api/auth/csrf"
        ? Response.json(csrf)
        : Response.json({ messageCode }, { status: 409 }),
    );
    await assert.rejects(
      enterTask(
        2,
        4,
        { ...current, data: { ...current.data, state: "ACCEPTED" } },
        "start",
        signal,
      ),
      (e) =>
        e instanceof TaskProgressError &&
        e.status === 409 &&
        e.messageCode === messageCode,
    );
    mock.mock.restore();
  }
});
