import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { build } from "esbuild";

const bundle = await build({
  entryPoints: ["src/users/userBranchMembershipApi.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  write: false,
  metafile: true,
});
const { createUserBranchMembershipApi } = await import(
  `data:text/javascript;base64,${Buffer.from(bundle.outputFiles[0].text).toString("base64")}`
);
const tag = '"ABCDEF0123456789"',
  fresh = '"ABCDEF0123456790"';
const privateValue = "PRIVATE-TOKEN-DETAIL-STACK";
const dto = (userId = 42, branchId = 123) => ({
  accessId: 91,
  userId,
  branchId,
  accessLevel: "STANDARD",
  isPrimary: false,
});
const response = (body, status = 200, headers = {}) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json", ...headers },
  });
const csrf = (token = "attempt-token") =>
  response({ csrfToken: token, headerName: "X-CSRF-TOKEN" });
const success = (data = dto(), etag = fresh) =>
  response(data, 201, { ETag: etag });
function deferred() {
  let resolve;
  const promise = new Promise((r) => {
    resolve = r;
  });
  return { promise, resolve };
}
function harness({ bootstrap = () => csrf(), post = () => success() } = {}) {
  const calls = [];
  const api = createUserBranchMembershipApi(async (url, options) => {
    calls.push({ url, ...options });
    return options.method === "GET" ? bootstrap(options) : post(options, url);
  });
  return {
    api,
    calls,
    run: (signal) => api.addBranchMembership(42, tag, 123, signal),
  };
}
const headers = (call) => Object.fromEntries(new Headers(call.headers));
const invalidIds = [
  0,
  -1,
  1.5,
  NaN,
  Infinity,
  -Infinity,
  Number.MAX_SAFE_INTEGER + 1,
  "123",
  null,
  undefined,
  {},
  1n,
];

test("exact CSRF/POST contract, branchId-only payload and detached allowlisted DTO without Location", async () => {
  const h = harness({
    post: () =>
      success({ ...dto(), detail: privateValue, csrfToken: privateValue }),
  });
  assert.deepEqual(await h.run(), {
    outcome: "CONFIRMED_ADDED",
    status: 201,
    data: dto(),
    etag: fresh,
  });
  assert.equal(h.calls.length, 2);
  for (const call of h.calls) {
    assert.equal(call.credentials, "include");
    assert.equal(call.cache, "no-store");
    assert.equal(call.redirect, "error");
    assert.equal(call.signal, undefined);
    assert.equal(headers(call).authorization, undefined);
  }
  assert.equal(h.calls[0].url, "/api/auth/csrf");
  assert.equal(h.calls[0].method, "GET");
  assert.equal(h.calls[0].body, undefined);
  assert.deepEqual(headers(h.calls[0]), { accept: "application/json" });
  assert.equal(h.calls[1].url, "/api/users/42/branches");
  assert.equal(h.calls[1].method, "POST");
  assert.deepEqual(headers(h.calls[1]), {
    accept: "application/json",
    "content-type": "application/json",
    "x-csrf-token": "attempt-token",
    "if-match": tag,
  });
  assert.equal(h.calls[1].body, '{"branchId":123}');
});
for (const field of ["userId", "branchId"])
  for (const value of invalidIds)
    test(`${field} invalid ${String(value)} rejected before every fetch`, async () => {
      const h = harness();
      const r = await h.api.addBranchMembership(
        field === "userId" ? value : 42,
        tag,
        field === "branchId" ? value : 123,
      );
      assert.equal(r.outcome, "NOT_DISPATCHED");
      assert.equal(r.error.reason, "INVALID_REQUEST");
      assert.equal(h.calls.length, 0);
    });
test("positive safe integer boundaries and request-size bound", async () => {
  for (const value of [1, Number.MAX_SAFE_INTEGER]) {
    const h = harness({ post: () => success(dto(value, value)) });
    assert.equal(
      (await h.api.addBranchMembership(value, tag, value)).outcome,
      "CONFIRMED_ADDED",
    );
    assert.equal(h.calls[1].url, `/api/users/${value}/branches`);
    assert.equal(h.calls[1].body, JSON.stringify({ branchId: value }));
    // Both fields are primitives; every valid body is far smaller than the transport limit.
    assert.ok(new TextEncoder().encode(h.calls[1].body).byteLength <= 16384);
  }
});
for (const value of [
  "",
  "*",
  'W/"ABCDEF0123456789"',
  '"abcdef0123456789"',
  `${tag},${fresh}`,
  "ABCDEF0123456789",
  tag + "\n",
  '"ABCDEF012345678"',
  null,
  123,
])
  test(`invalid strong ETag ${JSON.stringify(value)}`, async () => {
    const h = harness();
    const r = await h.api.addBranchMembership(42, value, 123);
    assert.equal(r.outcome, "NOT_DISPATCHED");
    assert.equal(r.error.reason, "INVALID_IF_MATCH");
    assert.equal(h.calls.length, 0);
  });
test("input objects/serialization hooks cannot inject extra fields", async () => {
  let evaluated = false;
  const payload = {
    branchId: 123,
    isPrimary: true,
    departmentId: 7,
    toJSON() {
      evaluated = true;
      return { actor: 7 };
    },
  };
  const h = harness();
  assert.equal(
    (await h.api.addBranchMembership(42, tag, payload)).outcome,
    "NOT_DISPATCHED",
  );
  assert.equal(evaluated, false);
  assert.equal(h.calls.length, 0);
});
test("primitive route/ETag/body snapshots are fixed before first await", async () => {
  const gate = deferred();
  const h = harness({ bootstrap: () => gate.promise });
  let user = 42,
    branch = 123,
    etag = tag;
  const pending = h.api.addBranchMembership(user, etag, branch);
  user = 43;
  branch = 456;
  etag = fresh;
  assert.equal(h.calls.length, 1);
  gate.resolve(csrf());
  await pending;
  assert.equal(h.calls[1].url, "/api/users/42/branches");
  assert.equal(h.calls[1].body, '{"branchId":123}');
  assert.equal(headers(h.calls[1])["if-match"], tag);
});
for (const value of [
  {},
  { csrfToken: "", headerName: "X-CSRF-TOKEN" },
  { csrfToken: "  ", headerName: "X-CSRF-TOKEN" },
  { csrfToken: "token", headerName: "x-csrf-token" },
  { csrfToken: 123, headerName: "X-CSRF-TOKEN" },
  ...["a\nb", "a\rb", "a\0b", "a\tb", "a\u007fb", "界", " token"].map(
    (csrfToken) => ({ csrfToken, headerName: "X-CSRF-TOKEN" }),
  ),
])
  test(`unsafe CSRF ${JSON.stringify(value)} never dispatches POST`, async () => {
    const h = harness({ bootstrap: () => response(value) });
    const r = await h.run();
    assert.equal(r.outcome, "NOT_DISPATCHED");
    assert.equal(r.error.reason, "INVALID_RESPONSE");
    assert.equal(h.calls.length, 1);
  });
for (const status of [400, 401, 403, 503, 204])
  test(`bootstrap HTTP ${status} never POSTs`, async () => {
    const h = harness({
      bootstrap: () =>
        status === 204
          ? new Response(null, { status })
          : response({ title: privateValue }, status),
    });
    const r = await h.run();
    assert.equal(r.outcome, "NOT_DISPATCHED");
    assert.equal(h.calls.length, 1);
    assert.ok(!JSON.stringify(r).includes(privateValue));
  });
for (const bootstrap of [
  () => {
    throw new Error(privateValue);
  },
  () => new Response("{", { headers: { "Content-Type": "application/json" } }),
  () =>
    new Response(privateValue, { headers: { "Content-Type": "text/html" } }),
  () => ({
    status: 200,
    headers: new Headers({ "Content-Type": "application/json" }),
    text: async () => {
      throw new Error(privateValue);
    },
  }),
])
  test(`bootstrap failure variant ${String(bootstrap).slice(0, 45)} is not dispatched`, async () => {
    const h = harness({ bootstrap });
    const r = await h.run();
    assert.equal(r.outcome, "NOT_DISPATCHED");
    assert.equal(h.calls.length, 1);
    assert.ok(!JSON.stringify(r).includes(privateValue));
  });
test("pre-abort prevents fetch; abort during CSRF/body read prevents POST", async () => {
  const c = new AbortController();
  c.abort();
  const h = harness();
  assert.equal((await h.run(c.signal)).outcome, "NOT_DISPATCHED");
  assert.equal(h.calls.length, 0);
  for (const duringBody of [false, true]) {
    const d = new AbortController(),
      gate = deferred();
    const k = harness({
      bootstrap: () =>
        duringBody
          ? {
              status: 200,
              headers: new Headers({ "Content-Type": "application/json" }),
              text: () => gate.promise,
            }
          : gate.promise,
    });
    const pending = k.run(d.signal);
    await new Promise((r) => setImmediate(r));
    d.abort();
    gate.resolve(
      duringBody
        ? JSON.stringify({ csrfToken: "token", headerName: "X-CSRF-TOKEN" })
        : csrf(),
    );
    const result = await pending;
    assert.equal(result.outcome, "NOT_DISPATCHED");
    assert.equal(result.error.kind, "cancelled");
    assert.equal(k.calls.length, 1);
  }
});
test("concurrent attempts use independent CSRF, destinations and versions", async () => {
  const a = deferred(),
    b = deferred();
  let n = 0;
  const h = harness({
    bootstrap: () => (n++ === 0 ? a.promise : b.promise),
    post: (o, url) =>
      success(
        dto(Number(url.split("/")[3]), JSON.parse(o.body).branchId),
        '"ABCDEF0123456791"',
      ),
  });
  const p = h.run(),
    q = h.api.addBranchMembership(43, fresh, 456);
  b.resolve(csrf("second-token"));
  assert.equal((await q).outcome, "CONFIRMED_ADDED");
  a.resolve(csrf("first-token"));
  assert.equal((await p).outcome, "CONFIRMED_ADDED");
  const posts = h.calls.filter((c) => c.method === "POST");
  assert.equal(posts.length, 2);
  assert.equal(posts[0].url, "/api/users/43/branches");
  assert.equal(posts[0].body, '{"branchId":456}');
  assert.equal(headers(posts[0])["x-csrf-token"], "second-token");
  assert.equal(headers(posts[0])["if-match"], fresh);
  assert.equal(posts[1].url, "/api/users/42/branches");
  assert.equal(posts[1].body, '{"branchId":123}');
  assert.equal(headers(posts[1])["x-csrf-token"], "first-token");
});
const mappings = [
  [400, "INVALID_IF_MATCH"],
  [400, "VALIDATION_FAILED"],
  [400, "BRANCH_UNAVAILABLE"],
  [400, "CSRF_VALIDATION_FAILED"],
  [400, "HTTPS_REQUIRED"],
  [401, "SESSION_INVALID"],
  [403, "BRANCH_ASSIGNMENT_NOT_ALLOWED"],
  [403, "PASSWORD_CHANGE_REQUIRED"],
  [404, "USER_NOT_FOUND"],
  [409, "TARGET_NOT_ACTIVE"],
  [409, "MEMBERSHIP_EXISTS"],
  [409, "EXISTING_ROLE_PATH_CONFLICT"],
  [409, "OPERATIONS_MANAGER_SCOPE_CONFLICT"],
  [412, "STALE_USER_VERSION"],
  [428, "IF_MATCH_REQUIRED"],
  [413, "REQUEST_TOO_LARGE"],
  [415, "UNSUPPORTED_MEDIA_TYPE"],
  [500, "INTERNAL_SERVER_ERROR"],
  [503, "AUTH_SERVICE_UNAVAILABLE"],
];
for (const [status, code] of mappings)
  for (const field of ["code", "title"])
    test(`${status} allowlisted ${field} ${code}, sanitized without retry`, async () => {
      const h = harness({
        post: () =>
          response(
            {
              status,
              [field]: code,
              detail: privateValue,
              traceId: privateValue,
              stack: privateValue,
              csrfToken: privateValue,
            },
            status,
            { "Content-Type": "application/problem+json" },
          ),
      });
      const r = await h.run();
      assert.equal(r.outcome, status >= 500 ? "OUTCOME_UNKNOWN" : "REJECTED");
      assert.deepEqual(r.error, { kind: "http", reason: code, status });
      assert.ok(!JSON.stringify(r).includes(privateValue));
      assert.equal(h.calls.length, 2);
    });
test("HTTPS literal title and code-less framework errors sanitized by status", async () => {
  for (const [status, value, expected] of [
    [400, { title: "HTTPS is required" }, "HTTPS_REQUIRED"],
    [400, { title: privateValue }, "VALIDATION_FAILED"],
    [403, {}, "BRANCH_ASSIGNMENT_NOT_ALLOWED"],
    [415, {}, "UNSUPPORTED_MEDIA_TYPE"],
  ]) {
    const h = harness({ post: () => response(value, status) });
    const r = await h.run();
    assert.equal(r.outcome, "REJECTED");
    assert.equal(r.error.reason, expected);
    assert.ok(!JSON.stringify(r).includes(privateValue));
    assert.equal(h.calls.length, 2);
  }
});
for (const value of [
  {},
  { code: privateValue },
  { title: privateValue },
  { code: "INVALID_STATUS_TRANSITION" },
  { status: 400, code: "MEMBERSHIP_EXISTS" },
])
  test(`unknown/mismatched 409 ${JSON.stringify(value)} is indeterminate`, async () => {
    const h = harness({ post: () => response(value, 409) });
    const r = await h.run();
    assert.equal(r.outcome, "OUTCOME_UNKNOWN");
    assert.equal(r.error.reason, "UNEXPECTED_HTTP");
    assert.equal(h.calls.length, 2);
    assert.ok(!JSON.stringify(r).includes(privateValue));
  });
const invalidDtos = [
  null,
  [],
  {},
  ...[
    ["accessId", 0],
    ["accessId", Number.MAX_SAFE_INTEGER + 1],
    ["accessId", "91"],
    ["accessId", 1.5],
    ["userId", 43],
    ["userId", "42"],
    ["userId", 0],
    ["branchId", 456],
    ["branchId", "123"],
    ["branchId", 0],
    ["accessLevel", "ADMIN"],
    ["accessLevel", "standard"],
    ["isPrimary", true],
    ["isPrimary", null],
    ["isPrimary", undefined],
  ].map(([field, value]) => ({ ...dto(), [field]: value })),
];
for (const value of invalidDtos)
  test(`invalid 201 DTO ${JSON.stringify(value)} is unknown`, async () => {
    const h = harness({ post: () => success(value) });
    const r = await h.run();
    assert.equal(r.outcome, "OUTCOME_UNKNOWN");
    assert.equal(r.error.reason, "INVALID_RESPONSE");
    assert.equal(h.calls.length, 2);
  });
for (const etag of [
  null,
  "",
  tag,
  '"abcdef0123456790"',
  'W/"ABCDEF0123456790"',
  `${tag},${fresh}`,
])
  test(`invalid/unchanged response ETag ${String(etag)}`, async () => {
    const h = harness({
      post: () => (etag === null ? response(dto(), 201) : success(dto(), etag)),
    });
    assert.equal((await h.run()).outcome, "OUTCOME_UNKNOWN");
    assert.equal(h.calls.length, 2);
  });
for (const status of [200, 204, 202, 302, 429, 502])
  test(`unexpected HTTP ${status} cannot confirm insertion`, async () => {
    const h = harness({
      post: () =>
        status === 204
          ? new Response(null, { status })
          : response(dto(), status, { ETag: fresh }),
    });
    assert.equal((await h.run()).outcome, "OUTCOME_UNKNOWN");
    assert.equal(h.calls.length, 2);
  });
for (const status of [201, 409, 400])
  for (const type of ["application/json", "text/html"])
    test(`malformed/non-JSON ${status} ${type} uses conservative outcome`, async () => {
      const h = harness({
        post: () =>
          new Response("{", {
            status,
            headers: { "Content-Type": type, ETag: fresh },
          }),
      });
      assert.equal(
        (await h.run()).outcome,
        status === 400 ? "REJECTED" : "OUTCOME_UNKNOWN",
      );
      assert.equal(h.calls.length, 2);
    });
for (const name of ["Error", "TimeoutError", "AbortError"])
  test(`POST ${name} unknown, no retry or exception leak`, async () => {
    const h = harness({
      post: () => {
        const e = new Error(privateValue);
        e.name = name;
        throw e;
      },
    });
    const r = await h.run();
    assert.equal(r.outcome, "OUTCOME_UNKNOWN");
    assert.equal(
      r.error.kind,
      name === "TimeoutError"
        ? "timeout"
        : name === "AbortError"
          ? "cancelled"
          : "network",
    );
    assert.equal(h.calls.length, 2);
    assert.ok(!JSON.stringify(r).includes(privateValue));
  });
test("synchronous fetch throw at POST invocation is potentially dispatched", async () => {
  const calls = [];
  const api = createUserBranchMembershipApi((url, o) => {
    calls.push(o.method);
    if (o.method === "GET") return Promise.resolve(csrf());
    throw new Error(privateValue);
  });
  const r = await api.addBranchMembership(42, tag, 123);
  assert.equal(r.outcome, "OUTCOME_UNKNOWN");
  assert.deepEqual(calls, ["GET", "POST"]);
});
for (const status of [201, 400, 409])
  test(`body-read failure after ${status} remains unknown`, async () => {
    const h = harness({
      post: () => ({
        status,
        headers: new Headers({
          "Content-Type": "application/json",
          ETag: fresh,
        }),
        text: async () => {
          throw new Error(privateValue);
        },
      }),
    });
    const r = await h.run();
    assert.equal(r.outcome, "OUTCOME_UNKNOWN");
    assert.equal(h.calls.length, 2);
    assert.ok(!JSON.stringify(r).includes(privateValue));
  });
test("late 201 after abort including body-read cannot publish confirmed success", async () => {
  for (const duringBody of [false, true]) {
    const c = new AbortController(),
      gate = deferred();
    const h = harness({
      post: () =>
        duringBody
          ? {
              status: 201,
              headers: new Headers({
                "Content-Type": "application/json",
                ETag: fresh,
              }),
              text: () => gate.promise,
            }
          : gate.promise,
    });
    const pending = h.run(c.signal);
    await new Promise((r) => setImmediate(r));
    assert.equal(h.calls.length, 2);
    assert.equal(h.calls[1].signal, c.signal);
    c.abort();
    gate.resolve(duringBody ? JSON.stringify(dto()) : success());
    const r = await pending;
    assert.equal(r.outcome, "OUTCOME_UNKNOWN");
    assert.equal(r.error.kind, "cancelled");
    assert.equal(h.calls.length, 2);
  }
});
test("explicit sequential attempts bootstrap again and expose only addBranchMembership", async () => {
  let n = 0;
  const h = harness({ bootstrap: () => csrf(`token-${++n}`) });
  assert.deepEqual(Object.keys(h.api), ["addBranchMembership"]);
  await h.run();
  await h.run();
  assert.equal(h.calls.length, 4);
  assert.equal(headers(h.calls[1])["x-csrf-token"], "token-1");
  assert.equal(headers(h.calls[3])["x-csrf-token"], "token-2");
});
test("production module has no mock/auth-state/storage/logging dependencies", async () => {
  assert.deepEqual(Object.keys(bundle.metafile.inputs), [
    "src/users/userBranchMembershipApi.ts",
  ]);
  for (const path of [
    "src/users/userBranchMembershipApi.ts",
    "src/users/userBranchMembershipContracts.ts",
  ]) {
    const s = await readFile(path, "utf8");
    assert.doesNotMatch(
      s,
      /localStorage|sessionStorage|indexedDB|console\.|services\.|AuthStore|AuthProvider|Bearer|Authorization/,
    );
    assert.doesNotMatch(
      s,
      /\b(remove|revoke|regrant|transfer|department)\w*\s*\(/i,
    );
  }
  const saved = Object.getOwnPropertyDescriptor(globalThis, "localStorage");
  let touches = 0;
  Object.defineProperty(globalThis, "localStorage", {
    configurable: true,
    get() {
      touches++;
      throw new Error("forbidden");
    },
  });
  try {
    assert.equal((await harness().run()).outcome, "CONFIRMED_ADDED");
    assert.equal(touches, 0);
  } finally {
    if (saved) Object.defineProperty(globalThis, "localStorage", saved);
    else delete globalThis.localStorage;
  }
});
