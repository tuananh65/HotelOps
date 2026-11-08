import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { build } from "esbuild";

const bundle = await build({
  entryPoints: ["src/users/userActivationApi.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  write: false,
  metafile: true,
});
const { createUserActivationApi } = await import(
  `data:text/javascript;base64,${Buffer.from(bundle.outputFiles[0].text).toString("base64")}`
);
const tag = '"ABCDEF0123456789"';
const fresh = '"ABCDEF0123456790"';
const reason = "Account approved";
const secret = "RAW-PRIVATE-DETAIL-TOKEN";
const response = (value, status = 200, headers = {}) =>
  new Response(JSON.stringify(value), {
    status,
    headers: { "Content-Type": "application/json", ...headers },
  });
const csrf = (token = "attempt-token") =>
  response({ csrfToken: token, headerName: "X-CSRF-TOKEN" });
const success = () =>
  response({ userId: 42, accountStatus: "ACTIVE" }, 200, { ETag: fresh });
function harness({ bootstrap = () => csrf(), patch = success } = {}) {
  const calls = [];
  const api = createUserActivationApi(async (url, options) => {
    calls.push({ url, ...options });
    return options.method === "GET" ? bootstrap(options) : patch(options);
  });
  return {
    api,
    calls,
    run: (signal) => api.activateUser(42, tag, reason, signal),
  };
}
function deferred() {
  let resolve;
  const promise = new Promise((r) => {
    resolve = r;
  });
  return { promise, resolve };
}
const headers = (c) => Object.fromEntries(new Headers(c.headers));

test("exact request URLs/methods/headers, reason-only body and detached public success", async () => {
  const h = harness({
    patch: () =>
      response({ userId: 42, accountStatus: "ACTIVE", secret }, 200, {
        ETag: fresh,
      }),
  });
  assert.deepEqual(await h.api.activateUser(42, tag, `  ${reason}  `), {
    outcome: "CONFIRMED_ACTIVATED",
    status: 200,
    data: { userId: 42, accountStatus: "ACTIVE" },
    etag: fresh,
  });
  assert.equal(h.calls.length, 2);
  for (const call of h.calls) {
    assert.equal(call.credentials, "include");
    assert.equal(call.cache, "no-store");
    assert.equal(call.redirect, "error");
    assert.equal(call.signal, undefined);
  }
  assert.equal(h.calls[0].url, "/api/auth/csrf");
  assert.equal(h.calls[0].method, "GET");
  assert.equal(h.calls[0].body, undefined);
  assert.deepEqual(headers(h.calls[0]), { accept: "application/json" });
  assert.equal(h.calls[1].url, "/api/users/42/activate");
  assert.equal(h.calls[1].method, "PATCH");
  assert.deepEqual(headers(h.calls[1]), {
    accept: "application/json",
    "content-type": "application/json",
    "if-match": tag,
    "x-csrf-token": "attempt-token",
  });
  assert.equal(h.calls[1].body, JSON.stringify({ reason }));
});
for (const id of [
  0,
  -1,
  1.5,
  NaN,
  Infinity,
  Number.MAX_SAFE_INTEGER + 1,
  "42",
  null,
  undefined,
]) {
  test(`invalid userId ${String(id)} fails before fetch`, async () => {
    const h = harness();
    const result = await h.api.activateUser(id, tag, reason);
    assert.equal(result.outcome, "NOT_DISPATCHED");
    assert.equal(result.error.reason, "INVALID_REQUEST");
    assert.equal(h.calls.length, 0);
  });
}
test("maximum safe integer remains exact in URL", async () => {
  const h = harness({
    patch: () =>
      response(
        { userId: Number.MAX_SAFE_INTEGER, accountStatus: "ACTIVE" },
        200,
        { ETag: fresh },
      ),
  });
  assert.equal(
    (await h.api.activateUser(Number.MAX_SAFE_INTEGER, tag, reason)).outcome,
    "CONFIRMED_ACTIVATED",
  );
  assert.equal(
    h.calls[1].url,
    `/api/users/${Number.MAX_SAFE_INTEGER}/activate`,
  );
});
for (const value of [
  "",
  "*",
  'W/"ABCDEF0123456789"',
  '"abcdef0123456789"',
  `${tag},${fresh}`,
  "ABCDEF0123456789",
  tag + "\n",
  null,
]) {
  test(`invalid strong ETag ${JSON.stringify(value)}`, async () => {
    const h = harness();
    const r = await h.api.activateUser(42, value, reason);
    assert.equal(r.error.reason, "INVALID_IF_MATCH");
    assert.equal(r.outcome, "NOT_DISPATCHED");
    assert.equal(h.calls.length, 0);
  });
}
for (const value of [
  null,
  undefined,
  123,
  {},
  new String(reason),
  "",
  " 123456789 ",
  "\u0085123456789\u0085",
]) {
  test(`invalid reason ${JSON.stringify(value)} before fetch`, async () => {
    const h = harness();
    assert.equal(
      (await h.api.activateUser(42, tag, value)).outcome,
      "NOT_DISPATCHED",
    );
    assert.equal(h.calls.length, 0);
  });
}
for (const value of ["1234567890", "😀😀😀😀😀", "\uFEFF123456789"]) {
  test(`ten UTF16 code units accepted ${JSON.stringify(value)}`, async () => {
    const h = harness();
    assert.equal(
      (await h.api.activateUser(42, tag, value)).outcome,
      "CONFIRMED_ACTIVATED",
    );
    assert.equal(h.calls[1].body, JSON.stringify({ reason: value }));
  });
}
test(".NET whitespace trimming and immutable primitive snapshot before CSRF await", async () => {
  const gate = deferred();
  const h = harness({ bootstrap: () => gate.promise });
  let value = "\u0085 1234567890 \u0085";
  const pending = h.api.activateUser(42, tag, value);
  value = "replacement";
  assert.equal(h.calls.length, 1);
  gate.resolve(csrf());
  await pending;
  assert.equal(h.calls[1].body, '{"reason":"1234567890"}');
});
test("serialized UTF8 body exactly 16KiB passes; one byte over fails before fetch", async () => {
  const overhead = new TextEncoder().encode(
    JSON.stringify({ reason: "" }),
  ).length;
  const exact = "a".repeat(16384 - overhead);
  const h = harness();
  assert.equal(
    (await h.api.activateUser(42, tag, exact)).outcome,
    "CONFIRMED_ACTIVATED",
  );
  assert.equal(new TextEncoder().encode(h.calls[1].body).length, 16384);
  const over = harness();
  assert.equal(
    (await over.api.activateUser(42, tag, exact + "a")).error.reason,
    "REQUEST_TOO_LARGE",
  );
  assert.equal(over.calls.length, 0);
});
test("UTF8 and JSON escaping count toward body size without arbitrary character max", async () => {
  for (const value of ["界".repeat(5500), '"'.repeat(8200)]) {
    const h = harness();
    assert.equal(
      (await h.api.activateUser(42, tag, value)).error.reason,
      "REQUEST_TOO_LARGE",
    );
    assert.equal(h.calls.length, 0);
  }
  const h = harness();
  assert.equal(
    (await h.api.activateUser(42, tag, "a".repeat(10000))).outcome,
    "CONFIRMED_ACTIVATED",
  );
});
for (const value of [
  {},
  { csrfToken: "", headerName: "X-CSRF-TOKEN" },
  { csrfToken: " \t", headerName: "X-CSRF-TOKEN" },
  { csrfToken: "x", headerName: "other" },
  { csrfToken: 3, headerName: "X-CSRF-TOKEN" },
  { csrfToken: "x\ny", headerName: "X-CSRF-TOKEN" },
]) {
  test(`invalid CSRF bootstrap ${JSON.stringify(value)}`, async () => {
    const h = harness({ bootstrap: () => response(value) });
    const r = await h.run();
    assert.equal(r.outcome, "NOT_DISPATCHED");
    assert.equal(r.error.reason, "INVALID_RESPONSE");
    assert.equal(h.calls.length, 1);
  });
}
test("bootstrap 503 is NOT_DISPATCHED and never PATCHes", async () => {
  const h = harness({ bootstrap: () => response({ title: secret }, 503) });
  const r = await h.run();
  assert.equal(r.outcome, "NOT_DISPATCHED");
  assert.equal(r.error.reason, "AUTH_SERVICE_UNAVAILABLE");
  assert.equal(h.calls.length, 1);
});
test("bootstrap network failure is NOT_DISPATCHED", async () => {
  const h = harness({
    bootstrap: () => {
      throw new Error(secret);
    },
  });
  const r = await h.run();
  assert.equal(r.outcome, "NOT_DISPATCHED");
  assert.equal(r.error.kind, "network");
  assert.equal(h.calls.length, 1);
});
test("pre-abort prevents all fetch; abort during bootstrap prevents PATCH", async () => {
  const c = new AbortController();
  c.abort();
  const h = harness();
  assert.equal((await h.run(c.signal)).outcome, "NOT_DISPATCHED");
  assert.equal(h.calls.length, 0);
  const d = new AbortController();
  const late = deferred();
  const k = harness({ bootstrap: () => late.promise });
  const pending = k.run(d.signal);
  d.abort();
  late.resolve(csrf());
  const r = await pending;
  assert.equal(r.outcome, "NOT_DISPATCHED");
  assert.equal(r.error.kind, "cancelled");
  assert.equal(k.calls.length, 1);
});
test("concurrent attempts keep independent CSRF and target snapshots", async () => {
  const a = deferred(),
    b = deferred();
  let i = 0;
  const h = harness({ bootstrap: () => (i++ === 0 ? a.promise : b.promise) });
  const p = h.run(),
    q = h.api.activateUser(43, fresh, "Second reason");
  b.resolve(csrf("second-token"));
  await q;
  a.resolve(csrf("first-token"));
  await p;
  const patches = h.calls.filter((c) => c.method === "PATCH");
  assert.equal(patches.length, 2);
  assert.equal(patches[0].url, "/api/users/43/activate");
  assert.equal(headers(patches[0])["x-csrf-token"], "second-token");
  assert.equal(patches[1].url, "/api/users/42/activate");
  assert.equal(headers(patches[1])["x-csrf-token"], "first-token");
});
const failures = [
  [400, "INVALID_IF_MATCH"],
  [400, "VALIDATION_FAILED"],
  [400, "CSRF_VALIDATION_FAILED"],
  [401, "SESSION_INVALID"],
  [403, "ACTIVATION_NOT_ALLOWED"],
  [403, "PASSWORD_CHANGE_REQUIRED"],
  [404, "USER_NOT_FOUND"],
  [409, "INVALID_STATUS_TRANSITION"],
  [412, "STALE_USER_VERSION"],
  [428, "IF_MATCH_REQUIRED"],
  [413, "REQUEST_TOO_LARGE"],
  [415, "UNSUPPORTED_MEDIA_TYPE"],
  [500, "INTERNAL_SERVER_ERROR"],
  [503, "AUTH_SERVICE_UNAVAILABLE"],
];
for (const [status, code] of failures) {
  for (const property of ["code", "title"]) {
    test(`${status} allowlisted ${property} ${code}, sanitized and never retried`, async () => {
      const h = harness({
        patch: () =>
          response(
            {
              [property]: code,
              detail: secret,
              traceId: secret,
              stack: secret,
            },
            status,
            { "Content-Type": "application/problem+json" },
          ),
      });
      const r = await h.run();
      assert.equal(r.outcome, status >= 500 ? "OUTCOME_UNKNOWN" : "REJECTED");
      assert.deepEqual(r.error, { kind: "http", reason: code, status });
      assert.equal(JSON.stringify(r).includes(secret), false);
      assert.equal(h.calls.length, 2);
    });
  }
}
test("HTTPS title mapped; framework and conflicting ProblemDetails safely fall back", async () => {
  for (const [body, expected] of [
    [{ title: "HTTPS is required" }, "HTTPS_REQUIRED"],
    [{ title: secret }, "VALIDATION_FAILED"],
    [{ code: "CSRF_VALIDATION_FAILED", status: 401 }, "VALIDATION_FAILED"],
  ]) {
    const h = harness({ patch: () => response(body, 400) });
    const r = await h.run();
    assert.equal(r.outcome, "REJECTED");
    assert.equal(r.error.reason, expected);
    assert.equal(JSON.stringify(r).includes(secret), false);
  }
});
for (const [body, etag] of [
  [{ userId: 43, accountStatus: "ACTIVE" }, fresh],
  [{ id: 42, accountStatus: "ACTIVE" }, fresh],
  [{ userId: "42", accountStatus: "ACTIVE" }, fresh],
  [{ userId: 42, accountStatus: "INACTIVE" }, fresh],
  [{ userId: 42, accountStatus: "ACTIVE" }, ""],
  [{ userId: 42, accountStatus: "ACTIVE" }, 'W/"ABCDEF0123456790"'],
  [{ userId: 42, accountStatus: "ACTIVE" }, tag],
  [null, fresh],
]) {
  test(`invalid success body/tag ${JSON.stringify([body, etag])} is unknown`, async () => {
    const h = harness({ patch: () => response(body, 200, { ETag: etag }) });
    const r = await h.run();
    assert.equal(r.outcome, "OUTCOME_UNKNOWN");
    assert.equal(r.error.reason, "INVALID_RESPONSE");
    assert.equal(h.calls.length, 2);
  });
}
for (const status of [200, 204, 201, 302, 429, 502]) {
  test(`unexpected/non-JSON response ${status} cannot confirm mutation`, async () => {
    const h = harness({
      patch: () =>
        new Response(status === 204 ? null : secret, {
          status,
          headers: { "Content-Type": "text/html", ETag: fresh },
        }),
    });
    const r = await h.run();
    assert.equal(r.outcome, "OUTCOME_UNKNOWN");
    assert.equal(JSON.stringify(r).includes(secret), false);
    assert.equal(h.calls.length, 2);
  });
}
test("malformed JSON success unknown; malformed 4xx safely rejected", async () => {
  for (const status of [200, 400]) {
    const h = harness({
      patch: () =>
        new Response("{", {
          status,
          headers: { "Content-Type": "application/json", ETag: fresh },
        }),
    });
    assert.equal(
      (await h.run()).outcome,
      status === 200 ? "OUTCOME_UNKNOWN" : "REJECTED",
    );
    assert.equal(h.calls.length, 2);
  }
});
for (const name of ["Error", "TimeoutError", "AbortError"]) {
  test(`PATCH ${name} remains unknown without retry`, async () => {
    const h = harness({
      patch: () => {
        const e = new Error(secret);
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
    assert.equal(JSON.stringify(r).includes(secret), false);
    assert.equal(h.calls.length, 2);
  });
}
test("body-read failure after PATCH remains unknown even for 4xx", async () => {
  for (const status of [200, 400]) {
    const h = harness({
      patch: () => ({
        status,
        headers: new Headers({ "Content-Type": "application/json" }),
        text: async () => {
          throw new Error(secret);
        },
      }),
    });
    assert.equal((await h.run()).outcome, "OUTCOME_UNKNOWN");
    assert.equal(h.calls.length, 2);
  }
});
test("late 200 after abort, including abort during body-read, cannot publish success", async () => {
  for (const duringBody of [false, true]) {
    const c = new AbortController(),
      gate = deferred();
    const h = harness({
      patch: () =>
        duringBody
          ? {
              status: 200,
              headers: new Headers({
                "Content-Type": "application/json",
                ETag: fresh,
              }),
              text: () => gate.promise,
            }
          : gate.promise,
    });
    const pending = h.run(c.signal);
    await new Promise((resolve) => setImmediate(resolve));
    assert.equal(h.calls.length, 2);
    assert.equal(h.calls[1].signal, c.signal);
    c.abort();
    gate.resolve(
      duringBody
        ? JSON.stringify({ userId: 42, accountStatus: "ACTIVE" })
        : success(),
    );
    const r = await pending;
    assert.equal(r.outcome, "OUTCOME_UNKNOWN");
    assert.equal(r.error.kind, "cancelled");
    assert.equal(h.calls.length, 2);
  }
});
test("each sequential attempt bootstraps anew; API only exposes activation", async () => {
  let i = 0;
  const h = harness({ bootstrap: () => csrf(`token-${++i}`) });
  assert.deepEqual(Object.keys(h.api), ["activateUser"]);
  await h.run();
  await h.run();
  assert.equal(h.calls.length, 4);
  assert.equal(headers(h.calls[1])["x-csrf-token"], "token-1");
  assert.equal(headers(h.calls[3])["x-csrf-token"], "token-2");
});
test("production import graph has no storage, logging, mocks, auth-store or other operations", async () => {
  assert.deepEqual(Object.keys(bundle.metafile.inputs), [
    "src/users/userActivationApi.ts",
  ]);
  for (const path of [
    "src/users/userActivationApi.ts",
    "src/users/userActivationContracts.ts",
  ]) {
    const source = await readFile(path, "utf8");
    assert.doesNotMatch(
      source,
      /localStorage|sessionStorage|console\.|services\.|AuthStore|authStore|deactivate|\/status|Authorization|Bearer/,
    );
  }
});

test("bootstrap malformed/non-JSON/body-read failures never dispatch PATCH", async () => {
  for (const bootstrap of [
    () =>
      new Response("{", { headers: { "Content-Type": "application/json" } }),
    () => new Response(secret, { headers: { "Content-Type": "text/html" } }),
    () => ({
      status: 200,
      headers: new Headers({ "Content-Type": "application/json" }),
      text: async () => {
        throw new Error(secret);
      },
    }),
  ]) {
    const h = harness({ bootstrap });
    const result = await h.run();
    assert.equal(result.outcome, "NOT_DISPATCHED");
    assert.equal(h.calls.length, 1);
    assert.equal(JSON.stringify(result).includes(secret), false);
  }
});

test("missing response ETag is unknown and non-JSON framework 415 is rejected", async () => {
  const h = harness({
    patch: () => response({ userId: 42, accountStatus: "ACTIVE" }),
  });
  assert.equal((await h.run()).outcome, "OUTCOME_UNKNOWN");
  const k = harness({ patch: () => new Response(secret, { status: 415 }) });
  const result = await k.run();
  assert.equal(result.outcome, "REJECTED");
  assert.equal(result.error.reason, "UNSUPPORTED_MEDIA_TYPE");
  assert.equal(h.calls.length, 2);
  assert.equal(k.calls.length, 2);
});
