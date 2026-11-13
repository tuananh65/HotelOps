import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { build } from "esbuild";

const bundle = await build({
  entryPoints: ["src/users/userProfileApi.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  write: false,
  metafile: true,
});
const { createUserProfileApi } = await import(
  `data:text/javascript;base64,${Buffer.from(bundle.outputFiles[0].text).toString("base64")}`
);
const privateValue = "PRIVATE-RESPONSE-MUST-NOT-ESCAPE";
const tag = '"ABCDEF0123456789"';
const newTag = '"ABCDEF0123456790"';
const dto = (id = 42) => ({
  id,
  fullName: "Original Name",
  email: "employee@example.test",
  phone: null,
});
const response = (body, status = 200, headers = {}) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json", ...headers },
  });
const success = (id = 42, etag = newTag) =>
  response(dto(id), 200, { ETag: etag });
const csrf = () =>
  response({ csrfToken: "attempt-token", headerName: "X-CSRF-TOKEN" });
function harness({
  get = () => success(42, tag),
  bootstrap = csrf,
  patch = () => success(),
} = {}) {
  const calls = [];
  const api = createUserProfileApi(async (url, options) => {
    calls.push({ url, ...options });
    return url === "/api/auth/csrf"
      ? bootstrap(options)
      : options.method === "PATCH"
        ? patch(options)
        : get(options);
  });
  return { api, calls };
}
function deferred() {
  let resolve;
  const promise = new Promise((r) => {
    resolve = r;
  });
  return { promise, resolve };
}
const invalidIds = [
  0,
  -1,
  1.5,
  NaN,
  Infinity,
  Number.MAX_SAFE_INTEGER + 1,
  "42",
  null,
];
const invalidTags = [
  "",
  "*",
  'W/"ABCDEF0123456789"',
  '"abcdef0123456789"',
  '"ABCDEF0123456789", "ABCDEF0123456790"',
  "ABCDEF0123456789",
  '"ABCDEF012345678"',
  null,
];

test("GET exact management endpoint, headers, detached DTO and ETag", async () => {
  const value = dto();
  value.permissions = [privateValue];
  value.password = privateValue;
  const { api, calls } = harness({
    get: () => response(value, 200, { ETag: tag }),
  });
  assert.deepEqual(await api.getProfile(42), {
    ok: true,
    status: 200,
    data: dto(),
    etag: tag,
  });
  assert.equal(calls.length, 1);
  const c = calls[0];
  assert.equal(c.url, "/api/users/42/profile");
  assert.equal(c.method, "GET");
  assert.equal(c.credentials, "include");
  assert.equal(c.cache, "no-store");
  assert.equal(c.redirect, "error");
  assert.equal(c.body, undefined);
  assert.deepEqual(c.headers, { Accept: "application/json" });
});
for (const userId of invalidIds)
  test(`invalid ID ${String(userId)} sends no GET/PATCH/bootstrap`, async () => {
    const { api, calls } = harness();
    assert.equal((await api.getProfile(userId)).error.kind, "invalid-request");
    assert.equal(
      (await api.updateProfile(userId, tag, { phone: null })).outcome,
      "NOT_DISPATCHED",
    );
    assert.equal(calls.length, 0);
  });
test("positive safe integer maximum survives both URL and response validation", async () => {
  const max = Number.MAX_SAFE_INTEGER;
  const { api, calls } = harness({
    get: () => success(max, tag),
    patch: () => success(max),
  });
  assert.equal((await api.getProfile(max)).data.id, max);
  assert.equal(
    (await api.updateProfile(max, tag, { phone: null })).outcome,
    "CONFIRMED_UPDATED",
  );
  assert.equal(calls[0].url, `/api/users/${max}/profile`);
  assert.equal(calls[2].url, `/api/users/${max}`);
});
for (const etag of invalidTags)
  test(`invalid ETag ${String(etag)}: GET invalid and PATCH never dispatches`, async () => {
    const h = harness({
      get: () => response(dto(), 200, etag === null ? {} : { ETag: etag }),
    });
    assert.equal((await h.api.getProfile(42)).error.kind, "invalid-response");
    const before = h.calls.length;
    assert.deepEqual(await h.api.updateProfile(42, etag, { phone: null }), {
      outcome: "NOT_DISPATCHED",
      error: { kind: "invalid-request", reason: "INVALID_ETAG" },
    });
    assert.equal(h.calls.length, before);
  });
for (const key of ["id", "fullName", "email", "phone"])
  test(`GET requires DTO ${key}`, async () => {
    const value = dto();
    delete value[key];
    const { api } = harness({ get: () => response(value, 200, { ETag: tag }) });
    assert.equal((await api.getProfile(42)).error.kind, "invalid-response");
  });
for (const [key, value] of [
  ["id", 43],
  ["id", Number.MAX_SAFE_INTEGER + 1],
  ["fullName", null],
  ["email", 123],
  ["phone", []],
])
  test(`GET invalid DTO ${key} ${String(value)}`, async () => {
    const { api } = harness({
      get: () => response({ ...dto(), [key]: value }, 200, { ETag: tag }),
    });
    assert.equal((await api.getProfile(42)).error.reason, "INVALID_RESPONSE");
  });
for (const [patch, expected] of [
  [{ fullName: " New Name " }, { fullName: "New Name" }],
  [{ email: " test@example.test " }, { email: "test@example.test" }],
  [{ phone: null }, { phone: null }],
  [{ phone: "" }, { phone: null }],
  [{ phone: "   " }, { phone: null }],
  [{ phone: " 123 " }, { phone: "123" }],
  [
    { fullName: "N", email: "E", phone: "P" },
    { fullName: "N", email: "E", phone: "P" },
  ],
])
  test(`PATCH presence/normalization ${JSON.stringify(patch)}`, async () => {
    const { api, calls } = harness();
    assert.equal(
      (await api.updateProfile(42, tag, patch)).outcome,
      "CONFIRMED_UPDATED",
    );
    assert.deepEqual(JSON.parse(calls[1].body), expected);
  });
for (const patch of [
  {},
  null,
  [],
  { fullName: null },
  { email: null },
  { fullName: " " },
  { email: "" },
  { phone: 123 },
  { phone: undefined },
  { fullName: "N".repeat(201) },
  { email: "e".repeat(151) },
  { phone: "1".repeat(21) },
  ...[
    "username",
    "employeeCode",
    "role",
    "roles",
    "branchId",
    "departmentId",
    "accountStatus",
    "password",
    "expectedVersion",
    "rowVersion",
    "toJSON",
    "FullName",
  ].map((key) => ({ phone: null, [key]: privateValue })),
])
  test(`invalid/unknown patch ${JSON.stringify(patch)}`, async () => {
    const { api, calls } = harness();
    const result = await api.updateProfile(42, tag, patch);
    assert.equal(result.outcome, "NOT_DISPATCHED");
    assert.equal(result.error.reason, "INVALID_REQUEST");
    assert.equal(calls.length, 0);
    assert.equal(JSON.stringify(result).includes(privateValue), false);
  });
test("accessors, inherited hooks and symbol fields are rejected without evaluation", async () => {
  let evaluated = false;
  const accessor = {};
  Object.defineProperty(accessor, "phone", {
    enumerable: true,
    get() {
      evaluated = true;
      return privateValue;
    },
  });
  const inherited = Object.create({
    toJSON() {
      evaluated = true;
      return {};
    },
  });
  inherited.phone = null;
  for (const patch of [
    accessor,
    inherited,
    { phone: null, [Symbol("extra")]: privateValue },
  ]) {
    const { api, calls } = harness();
    assert.equal(
      (await api.updateProfile(42, tag, patch)).outcome,
      "NOT_DISPATCHED",
    );
    assert.equal(calls.length, 0);
  }
  assert.equal(evaluated, false);
});
test("max lengths and email syntax stay server-authoritative", async () => {
  const patch = {
    fullName: "N".repeat(200),
    email: "!".repeat(150),
    phone: "!".repeat(20),
  };
  const { api, calls } = harness();
  await api.updateProfile(42, tag, patch);
  assert.deepEqual(JSON.parse(calls[1].body), patch);
});
test("snapshot before deferred bootstrap; exact CSRF/PATCH headers and one mutation", async () => {
  const bootstrap = deferred(),
    mutation = deferred();
  const { api, calls } = harness({
    bootstrap: () => bootstrap.promise,
    patch: () => mutation.promise,
  });
  const patch = { fullName: " New Name ", phone: null };
  const controller = new AbortController();
  const pending = api.updateProfile(42, tag, patch, controller.signal);
  patch.fullName = "MUTATED";
  patch.password = privateValue;
  assert.equal(calls.length, 1);
  assert.equal(calls[0].url, "/api/auth/csrf");
  assert.equal(calls[0].method, "GET");
  assert.equal(calls[0].body, undefined);
  assert.deepEqual(calls[0].headers, { Accept: "application/json" });
  bootstrap.resolve(csrf());
  await new Promise(setImmediate);
  assert.equal(calls.length, 2);
  const c = calls[1];
  assert.equal(c.url, "/api/users/42");
  assert.equal(c.method, "PATCH");
  assert.deepEqual(JSON.parse(c.body), { fullName: "New Name", phone: null });
  assert.deepEqual(c.headers, {
    Accept: "application/json",
    "Content-Type": "application/json",
    "X-CSRF-TOKEN": "attempt-token",
    "If-Match": tag,
  });
  for (const call of calls) {
    assert.equal(call.credentials, "include");
    assert.equal(call.cache, "no-store");
    assert.equal(call.redirect, "error");
    assert.equal(call.signal, controller.signal);
    assert.equal(new Headers(call.headers).has("Authorization"), false);
  }
  mutation.resolve(success());
  assert.equal((await pending).outcome, "CONFIRMED_UPDATED");
  assert.equal(calls.length, 2);
});
for (const responseTag of [tag, newTag])
  test(`200 accepts changed/no-op token ${responseTag}`, async () => {
    const value = { ...dto(), arbitrary: privateValue };
    const { api } = harness({
      patch: () => response(value, 200, { ETag: responseTag }),
    });
    assert.deepEqual(await api.updateProfile(42, tag, { phone: null }), {
      outcome: "CONFIRMED_UPDATED",
      status: 200,
      data: dto(),
      etag: responseTag,
    });
  });
for (const responseTag of invalidTags)
  test(`malformed PATCH response ETag ${String(responseTag)} is unknown`, async () => {
    const { api, calls } = harness({
      patch: () =>
        response(dto(), 200, responseTag === null ? {} : { ETag: responseTag }),
    });
    const result = await api.updateProfile(42, tag, { phone: null });
    assert.equal(result.outcome, "OUTCOME_UNKNOWN");
    assert.equal(result.error.reason, "INVALID_RESPONSE");
    assert.equal(calls.length, 2);
  });
for (const value of [
  { ...dto(), id: 43 },
  { ...dto(), email: null },
  { id: 42 },
  { ...dto(), phone: [] },
  null,
])
  test(`malformed PATCH DTO ${JSON.stringify(value)}`, async () => {
    const { api } = harness({
      patch: () => response(value, 200, { ETag: tag }),
    });
    assert.equal(
      (await api.updateProfile(42, tag, { phone: null })).outcome,
      "OUTCOME_UNKNOWN",
    );
  });
for (const [status, details, reason, outcome] of [
  [400, {}, "VALIDATION_FAILED", "REJECTED"],
  [
    400,
    { code: "CSRF_VALIDATION_FAILED" },
    "CSRF_VALIDATION_FAILED",
    "REJECTED",
  ],
  [401, {}, "SESSION_INVALID", "REJECTED"],
  [403, {}, "FORBIDDEN", "REJECTED"],
  [
    403,
    { code: "PASSWORD_CHANGE_REQUIRED" },
    "PASSWORD_CHANGE_REQUIRED",
    "REJECTED",
  ],
  [404, {}, "NOT_FOUND", "REJECTED"],
  [409, { title: "Email is already in use" }, "EMAIL_IN_USE", "REJECTED"],
  [409, { title: "LAST_DURABLE_ADMIN" }, "LAST_DURABLE_ADMIN", "REJECTED"],
  [409, {}, "UNEXPECTED_HTTP", "OUTCOME_UNKNOWN"],
  [
    409,
    { title: "Email is already in use", status: 400 },
    "UNEXPECTED_HTTP",
    "OUTCOME_UNKNOWN",
  ],
  [412, {}, "STALE_VERSION", "REJECTED"],
  [428, {}, "IF_MATCH_REQUIRED", "REJECTED"],
  [415, {}, "UNSUPPORTED_MEDIA_TYPE", "REJECTED"],
  [500, {}, "UNEXPECTED_HTTP", "OUTCOME_UNKNOWN"],
  [503, {}, "SERVICE_UNAVAILABLE", "OUTCOME_UNKNOWN"],
  [
    503,
    { title: "GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE" },
    "GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE",
    "OUTCOME_UNKNOWN",
  ],
])
  test(`HTTP ${status} ${reason}: sanitized and never replayed`, async () => {
    const errorResponse = () =>
      response(
        {
          title: privateValue,
          detail: privateValue,
          traceId: privateValue,
          stack: privateValue,
          password: privateValue,
          ...details,
        },
        status,
        { "Content-Type": "application/problem+json" },
      );
    const { api, calls } = harness({
      get: errorResponse,
      patch: errorResponse,
    });
    const read = await api.getProfile(42);
    assert.equal(read.ok, false);
    assert.deepEqual(read.error, { kind: "http", status, reason });
    const result = await api.updateProfile(42, tag, { phone: null });
    assert.deepEqual(result, {
      outcome,
      error: { kind: "http", status, reason },
    });
    assert.equal(calls.length, 3);
    assert.equal(JSON.stringify([read, result]).includes(privateValue), false);
  });
for (const body of [
  {},
  { csrfToken: "", headerName: "X-CSRF-TOKEN" },
  { csrfToken: "token\n", headerName: "X-CSRF-TOKEN" },
  { csrfToken: "token", headerName: "Other" },
  { csrfToken: 123, headerName: "X-CSRF-TOKEN" },
])
  test(`invalid CSRF ${JSON.stringify(body)} prevents PATCH`, async () => {
    const { api, calls } = harness({ bootstrap: () => response(body) });
    assert.equal(
      (await api.updateProfile(42, tag, { phone: null })).outcome,
      "NOT_DISPATCHED",
    );
    assert.equal(calls.length, 1);
  });
for (const status of [400, 401, 403, 503])
  test(`bootstrap HTTP ${status} is not dispatched`, async () => {
    const { api, calls } = harness({
      bootstrap: () => response({ detail: privateValue }, status),
    });
    assert.equal(
      (await api.updateProfile(42, tag, { phone: null })).outcome,
      "NOT_DISPATCHED",
    );
    assert.equal(calls.length, 1);
  });
for (const mode of ["network", "abort", "timeout", "body-read"])
  test(`GET and post-dispatch ${mode} distinct safe failures`, async () => {
    const fail = () => {
      if (mode === "body-read")
        return {
          status: 200,
          headers: new Headers({ "Content-Type": "application/json" }),
          text: async () => {
            throw new Error(privateValue);
          },
        };
      throw mode === "abort"
        ? new DOMException(privateValue, "AbortError")
        : mode === "timeout"
          ? new DOMException(privateValue, "TimeoutError")
          : new Error(privateValue);
    };
    const { api, calls } = harness({ get: fail, patch: fail });
    const read = await api.getProfile(42);
    assert.equal(read.error.kind, mode === "abort" ? "cancelled" : "network");
    const result = await api.updateProfile(42, tag, { phone: null });
    assert.equal(result.outcome, "OUTCOME_UNKNOWN");
    assert.equal(result.error.kind, mode === "abort" ? "cancelled" : "network");
    assert.equal(calls.length, 3);
    assert.equal(JSON.stringify([read, result]).includes(privateValue), false);
  });
test("bootstrap network/body-read failure never dispatches PATCH", async () => {
  for (const bootstrap of [
    () => {
      throw new Error(privateValue);
    },
    () => ({
      status: 200,
      headers: new Headers({ "Content-Type": "application/json" }),
      text: async () => {
        throw new Error(privateValue);
      },
    }),
  ]) {
    const { api, calls } = harness({ bootstrap });
    assert.equal(
      (await api.updateProfile(42, tag, { phone: null })).outcome,
      "NOT_DISPATCHED",
    );
    assert.equal(calls.length, 1);
  }
});
test("pre-abort sends no fetch; abort during bootstrap never dispatches PATCH", async () => {
  const aborted = new AbortController();
  aborted.abort();
  const h = harness();
  assert.equal(
    (await h.api.getProfile(42, aborted.signal)).error.kind,
    "cancelled",
  );
  assert.equal(
    (await h.api.updateProfile(42, tag, { phone: null }, aborted.signal))
      .outcome,
    "NOT_DISPATCHED",
  );
  assert.equal(h.calls.length, 0);
  const gate = deferred(),
    controller = new AbortController();
  const { api, calls } = harness({ bootstrap: () => gate.promise });
  const pending = api.updateProfile(
    42,
    tag,
    { phone: null },
    controller.signal,
  );
  controller.abort();
  gate.resolve(csrf());
  assert.equal((await pending).outcome, "NOT_DISPATCHED");
  assert.equal(calls.length, 1);
});
test("late GET/PATCH after abort cannot publish data or ETag", async () => {
  const get = deferred(),
    patch = deferred(),
    g = new AbortController(),
    p = new AbortController();
  const { api, calls } = harness({
    get: () => get.promise,
    patch: () => patch.promise,
  });
  const reading = api.getProfile(42, g.signal);
  g.abort();
  get.resolve(success());
  const read = await reading;
  assert.equal(read.error.kind, "cancelled");
  assert.equal(read.data, undefined);
  const updating = api.updateProfile(42, tag, { phone: null }, p.signal);
  await new Promise(setImmediate);
  p.abort();
  patch.resolve(success());
  const result = await updating;
  assert.equal(result.outcome, "OUTCOME_UNKNOWN");
  assert.equal(result.error.kind, "cancelled");
  assert.equal(result.data, undefined);
  assert.equal(calls.length, 3);
});
for (const type of ["text/html", "application/json"])
  test(`non-JSON/malformed bodies ${type} are safe`, async () => {
    const invalid = () =>
      new Response(privateValue, {
        status: 200,
        headers: { "Content-Type": type, ETag: tag },
      });
    const { api } = harness({ get: invalid, patch: invalid });
    const read = await api.getProfile(42),
      update = await api.updateProfile(42, tag, { phone: null });
    assert.equal(read.error.kind, "invalid-response");
    assert.equal(update.outcome, "OUTCOME_UNKNOWN");
    assert.equal(JSON.stringify([read, update]).includes(privateValue), false);
  });
test("concurrent attempts use only their own fresh CSRF tokens", async () => {
  const gates = [deferred(), deferred()],
    calls = [];
  let next = 0;
  const api = createUserProfileApi(async (url, options) => {
    calls.push({ url, ...options });
    return options.method === "PATCH" ? success() : gates[next++].promise;
  });
  const first = api.updateProfile(42, tag, { phone: null }),
    second = api.updateProfile(42, tag, { fullName: "Name" });
  gates[1].resolve(
    response({ csrfToken: "second-token", headerName: "X-CSRF-TOKEN" }),
  );
  await second;
  gates[0].resolve(
    response({ csrfToken: "first-token", headerName: "X-CSRF-TOKEN" }),
  );
  await first;
  assert.deepEqual(
    calls
      .filter((c) => c.method === "PATCH")
      .map((c) => c.headers["X-CSRF-TOKEN"]),
    ["second-token", "first-token"],
  );
  assert.equal(calls.length, 4);
});
test("new explicit attempt bootstraps again; no global profile/token cache", async () => {
  const { api, calls } = harness();
  await api.getProfile(42);
  await api.getProfile(42);
  await api.updateProfile(42, tag, { phone: null });
  await api.updateProfile(42, tag, { phone: null });
  assert.equal(calls.length, 6);
  assert.equal(calls.filter((c) => c.url === "/api/auth/csrf").length, 2);
});
test("runtime import graph and forbidden side effects", async () => {
  assert.deepEqual(Object.keys(bundle.metafile.inputs), [
    "src/users/userProfileApi.ts",
  ]);
  const source = await readFile("src/users/userProfileApi.ts", "utf8");
  assert.doesNotMatch(
    source,
    /localStorage|sessionStorage|indexedDB|console\.|navigator\.|setTimeout|services\.can|AuthStore|Authorization/,
  );
});

test("request ETag containing a trailing newline is rejected before fetch", async () => {
  const { api, calls } = harness();
  assert.equal(
    (await api.updateProfile(42, `${tag}\n`, { phone: null })).outcome,
    "NOT_DISPATCHED",
  );
  assert.equal(calls.length, 0);
});

for (const stage of ["get", "csrf"])
  for (const status of [401, 403, 503])
    test("092 unreadable profile " + stage + " " + status, async () => {
      const bad = () => {
        const r = response({}, status);
        r.text = async () => {
          throw Error(privateValue);
        };
        return r;
      };
      const { api, calls } = harness(
        stage === "get" ? { get: bad } : { bootstrap: bad },
      );
      const r =
        stage === "get"
          ? await api.getProfile(42)
          : await api.updateProfile(42, tag, { fullName: "New Name" });
      assert.equal(r.error.kind, "http");
      assert.equal(r.error.status, status);
      assert.equal(
        r.error.reason,
        status === 401
          ? "SESSION_INVALID"
          : status === 403
            ? "FORBIDDEN"
            : "SERVICE_UNAVAILABLE",
      );
      if (stage === "csrf") assert.equal(r.outcome, "NOT_DISPATCHED");
      assert.deepEqual(
        calls.map((c) => c.method),
        ["GET"],
      );
      assert.ok(!JSON.stringify(r).includes(privateValue));
    });

for (const status of [401, 503, 200])
  test("092 unreadable PATCH " + status + " stays uncertain", async () => {
    const { api, calls } = harness({
      patch: () => {
        const r = response({}, status);
        r.text = async () => {
          throw Error(privateValue);
        };
        return r;
      },
    });
    const r = await api.updateProfile(42, tag, { fullName: "New Name" });
    assert.equal(r.outcome, "OUTCOME_UNKNOWN");
    assert.equal(r.error.status, status);
    assert.deepEqual(
      calls.map((c) => c.method),
      ["GET", "PATCH"],
    );
    assert.ok(!JSON.stringify(r).includes(privateValue));
  });
