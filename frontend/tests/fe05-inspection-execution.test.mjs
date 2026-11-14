// Offline API/contract tests; compile the exact TypeScript sources in memory.
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const ts = require("typescript");
const sources = resolve(
  import.meta.dirname,
  "../src/features/inspectionExecution",
);
const compile = (name) =>
  ts.transpileModule(readFileSync(resolve(sources, name), "utf8"), {
    fileName: name,
    compilerOptions: {
      module: ts.ModuleKind.ESNext,
      target: ts.ScriptTarget.ES2022,
    },
    reportDiagnostics: true,
  }).outputText;
const data = (js) =>
  "data:text/javascript;base64," + Buffer.from(js, "utf8").toString("base64");
const modelUrl = data(compile("model.ts"));
const model = await import(modelUrl);
const apiSource = compile("api.ts").replace(
  /from ["']\.\/model["']/g,
  `from ${JSON.stringify(modelUrl)}`,
);
const { createInspectionApi, InspectionApiError } = await import(
  data(apiSource)
);
const tag = '"0123456789ABCDEF"';
const item = {
  id: null,
  itemId: 17,
  criterion: "Bed clean",
  description: null,
  mandatory: true,
  passCondition: null,
  outcome: null,
  note: null,
  at: null,
};
const value = (state = "ASSIGNED", branch = 7) => ({
  id: 42,
  branchId: branch,
  roomId: 10,
  cleaningTaskId: 11,
  inspectorId: 20,
  versionId: 31,
  state,
  dueAt: null,
  criteria: [item],
  defects: [],
  evidence: [],
});
const json = (data, status = 200, headers = {}) =>
  new Response(JSON.stringify(data), {
    status,
    headers: { "Content-Type": "application/json", ETag: tag, ...headers },
  });
const csrf = () => json({ csrfToken: "csrf", headerName: "X-CSRF-TOKEN" });
const source = { value: value("INSPECTING"), etag: tag };
function client(
  send,
  onSessionInvalid = () => {},
  controller = new AbortController(),
) {
  return createInspectionApi(
    { signal: controller.signal, onSessionInvalid },
    send,
  );
}
test("default denial and stable session/principal/branch scope", () => {
  const ctx = {
    authorization: "granted",
    sessionKey: "s1",
    principalId: "p1",
    branchId: 7,
    inspectionPerform: true,
  };
  assert.equal(model.scopeOf(undefined), null);
  for (const change of [
    { authorization: "denied" },
    { inspectionPerform: false },
    { sessionKey: "" },
    { principalId: "" },
    { branchId: -1 },
  ])
    assert.equal(model.scopeOf({ ...ctx, ...change }), null);
  const current = model.scopeOf(ctx);
  assert.notEqual(current.key, model.scopeOf({ ...ctx, sessionKey: "s2" }).key);
  assert.notEqual(
    current.key,
    model.scopeOf({ ...ctx, principalId: "p2" }).key,
  );
  assert.notEqual(current.key, model.scopeOf({ ...ctx, branchId: 8 }).key);
  assert.equal(current.key, model.scopeOf({ ...ctx, branchId: 7 }).key);
});
test("read GET uses credentials/no-store; rejects cross-branch and missing ETag", async () => {
  let count = 0;
  const api = client(async (path, init) => {
    count++;
    assert.match(path, /\/api\/branches\/7\/inspections\/42\/execution$/);
    assert.equal(init.method, "GET");
    assert.equal(init.credentials, "include");
    assert.equal(init.cache, "no-store");
    return json(value());
  });
  assert.equal((await api.read(7, 42)).value.id, 42);
  assert.equal(count, 1);
  await assert.rejects(
    client(async () => json(value("ASSIGNED", 8))).read(7, 42),
    { status: 503 },
  );
  await assert.rejects(
    client(async () => json(value(), 200, { ETag: '"bad"' })).read(7, 42),
    { status: 503 },
  );
});
test("start/save/finish use CSRF, strong If-Match, and backend state", async () => {
  const requests = [];
  const api = client(async (path, init) => {
    requests.push({ path, init });
    if (path === "/api/auth/csrf") return csrf();
    return json(
      value(path.endsWith("/finish") ? "PENDING_CONCLUSION" : "INSPECTING"),
    );
  });
  await api.start(7, 42, tag, "Start");
  await api.save(
    7,
    42,
    source,
    [{ itemId: 17, outcome: "FAIL", note: "Stain" }],
    "Inspect",
  );
  assert.equal(
    (await api.finish(7, 42, tag, "End")).value.state,
    "PENDING_CONCLUSION",
  );
  assert.equal(requests.length, 6);
  for (const r of requests.filter((r) => r.path !== "/api/auth/csrf")) {
    assert.equal(r.init.headers["If-Match"], tag);
    assert.equal(r.init.headers["X-CSRF-TOKEN"], "csrf");
    assert.equal(r.init.credentials, "include");
  }
  assert.equal(requests.filter((r) => r.path.endsWith("/results")).length, 1);
  assert.equal(requests.filter((r) => r.path.endsWith("/finish")).length, 1);
});
test("401 sync callback for read, write and CSRF; 403 local only", async () => {
  let events = [];
  let api = client(
    async () => {
      events.push("response");
      return json({}, 401);
    },
    () => events.push("invalidate"),
  );
  await assert.rejects(api.read(7, 42), { status: 401 });
  assert.deepEqual(events, ["response", "invalidate"]);
  events = [];
  await assert.rejects(api.start(7, 42, tag, "Start"), { status: 401 });
  assert.deepEqual(events, ["response", "invalidate"]);
  events = [];
  api = client(
    async () => json({}, 403),
    () => events.push("invalidate"),
  );
  await assert.rejects(api.read(7, 42), { status: 403 });
  assert.deepEqual(events, []);
});
test("401 on mutation response invokes captured callback without retry", async () => {
  let calls = 0,
    invalids = 0;
  const api = client(
    async (path) => {
      calls++;
      return path === "/api/auth/csrf" ? csrf() : json({}, 401);
    },
    () => invalids++,
  );
  await assert.rejects(api.start(7, 42, tag, "Start"), { status: 401 });
  assert.equal(calls, 2);
  assert.equal(invalids, 1);
});
test("aborted/superseded response cannot revoke newer generation", async () => {
  const controller = new AbortController();
  let respond;
  let invalids = 0;
  const api = client(
    () =>
      new Promise((resolve) => {
        respond = resolve;
      }),
    () => invalids++,
    controller,
  );
  const operation = api.read(7, 42);
  await Promise.resolve();
  controller.abort();
  respond(json({}, 401));
  await assert.rejects(operation);
  assert.equal(invalids, 0);
});
test("session invalidated during CSRF bootstrap never dispatches mutation", async () => {
  const ctl = new AbortController();
  let n = 0;
  const api = client(
    async (path) => {
      n++;
      if (path === "/api/auth/csrf") {
        ctl.abort();
        return csrf();
      }
      throw Error("Mutation must not dispatch");
    },
    () => {},
    ctl,
  );
  await assert.rejects(api.start(7, 42, tag, "Start"));
  assert.equal(n, 1);
});
test("409/412/428 preserve conflict semantics; 503 and network remain uncertain", async () => {
  for (const status of [409, 412, 428, 503]) {
    const api = client(async (path) =>
      path === "/api/auth/csrf" ? csrf() : json({}, status),
    );
    await assert.rejects(
      api.start(7, 42, tag, "Start"),
      (e) =>
        e instanceof InspectionApiError &&
        e.status === status &&
        e.uncertain === (status === 503),
    );
  }
  const api = client(async (path) =>
    path === "/api/auth/csrf" ? csrf() : Promise.reject(Error("broken")),
  );
  await assert.rejects(
    api.start(7, 42, tag, "Start"),
    (e) => e instanceof InspectionApiError && e.status === 0 && e.uncertain,
  );
});
test("defect fails before network when severity catalog is missing or untrusted", async () => {
  let calls = 0;
  const api = client(async () => {
    calls++;
    return csrf();
  });
  await assert.rejects(
    api.defect(7, 42, tag, undefined, {
      itemId: 17,
      severityId: 1,
      description: "x",
      location: "y",
    }),
    { status: 403 },
  );
  await assert.rejects(
    api.defect(7, 42, tag, [{ id: 4, code: "MINOR", name: "Minor" }], {
      itemId: 17,
      severityId: 999,
      description: "x",
      location: "y",
    }),
    { status: 403 },
  );
  assert.equal(calls, 0);
});
test("finish unexpected result fails uncertain, never implies PASS or READY", async () => {
  const api = client(async (path) =>
    path === "/api/auth/csrf" ? csrf() : json(value("READY")),
  );
  await assert.rejects(
    api.finish(7, 42, tag, "End"),
    (e) => e.status === 503 && e.uncertain === true,
  );
});
test("criteria bind exactly to inspection's assigned version", () => {
  assert.equal(
    model.validateResults(source.value, [
      { itemId: 17, outcome: "PASS", note: null },
    ]).length,
    1,
  );
  assert.throws(() =>
    model.validateResults(source.value, [
      { itemId: 18, outcome: "PASS", note: null },
    ]),
  );
  assert.throws(() =>
    model.validateResults(source.value, [
      { itemId: 17, outcome: "FAIL", note: null },
      { itemId: 17, outcome: "PASS", note: null },
    ]),
  );
  assert.throws(() =>
    model.decodeInspection({ ...value(), criteria: [item, item] }, 7, 42, tag),
  );
});
test("No implicit severity and evidence authority", () => {
  assert.equal(model.trustedSeverity(undefined, 4), false);
  assert.equal(
    model.trustedSeverity([{ id: 4, code: "MINOR", name: "Minor" }], 4),
    true,
  );
  assert.equal(
    model.scopeOf({
      authorization: "granted",
      sessionKey: "s",
      principalId: "p",
      branchId: 7,
      inspectionPerform: false,
    }),
    null,
  );
});

test("evidence uploads validate format, extension, signature, size and cancellation", async () => {
  const png = new Uint8Array([137, 80, 78, 71, 13, 10, 26, 10]);
  const encoded = await model.encodeEvidence(
    new File([png], "evidence.png", { type: "image/png" }),
  );
  assert.deepEqual(encoded, {
    name: "evidence.png",
    mimeType: "image/png",
    base64: "iVBORw0KGgo=",
  });
  for (const file of [
    new File([png], "evidence.html", { type: "image/png" }),
    new File([png], "evidence.png", { type: "text/html" }),
    new File(["bad bytes"], "evidence.png", { type: "image/png" }),
    new File([png], "../evidence.png", { type: "image/png" }),
    new File([], "evidence.png", { type: "image/png" }),
    new File([new Uint8Array(model.MAX_EVIDENCE_BYTES + 1)], "evidence.png", {
      type: "image/png",
    }),
  ])
    await assert.rejects(model.encodeEvidence(file));
  const lifetime = new AbortController();
  lifetime.abort();
  await assert.rejects(
    model.encodeEvidence(
      new File([png], "evidence.png", { type: "image/png" }),
      lifetime.signal,
    ),
    { name: "AbortError" },
  );
});

test("weak/malformed ETags and invalid scope fail before any mutation or CSRF", async () => {
  let calls = 0;
  const api = client(async () => {
    calls++;
    return csrf();
  });
  for (const invalid of [
    "",
    'W/"0123456789ABCDEF"',
    '"0123456789abcdef"',
    '"0123"',
  ])
    await assert.rejects(api.start(7, 42, invalid, "Start"), { status: 428 });
  await assert.rejects(
    api.save(
      8,
      42,
      source,
      [{ itemId: 17, outcome: "PASS", note: null }],
      "Save",
    ),
    { status: 400 },
  );
  assert.equal(calls, 0);
});
