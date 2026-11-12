import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { build } from "esbuild";

const bundle = await build({
  entryPoints: ["src/users/createUserApi.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  write: false,
  metafile: true,
});
const { createCreateUserApi } = await import(
  `data:text/javascript;base64,${Buffer.from(bundle.outputFiles[0].text).toString("base64")}`
);
const secret = "PRIVATE-CREDENTIAL-NOT-FOR-ERRORS";
const input = () => ({
  employeeCode: " E1 ",
  username: " staff ",
  fullName: " Staff Name ",
  email: " staff@example.test ",
  branchId: 1,
  departmentId: 2,
  roleCode: "HK_STAFF",
});
const normalized = () => ({
  ...input(),
  employeeCode: "E1",
  username: "staff",
  fullName: "Staff Name",
  email: "staff@example.test",
  phone: null,
});
const payload = () => ({
  user: { id: 42, ...normalized(), accountStatus: "ACTIVE" },
  temporaryCredential: {
    password: secret,
    expiresAtUtc: "2026-11-08T08:30:00.1234567Z",
  },
});
const response = (body, status = 200, headers = {}) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json", ...headers },
  });
const csrf = () =>
  response({ csrfToken: "csrf-request-token", headerName: "X-CSRF-TOKEN" });
const created = (options) => {
  const value = payload();
  if (options?.body)
    value.user = { ...value.user, ...JSON.parse(options.body) };
  return response(value, 201, { Location: "/api/users/42" });
};
function harness(post = created, bootstrap = csrf) {
  const calls = [];
  const api = createCreateUserApi(async (url, options) => {
    calls.push({ url, ...options });
    return url === "/api/auth/csrf" ? bootstrap(options) : post(options);
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

test("exact bootstrap/POST contract, whitelist and success-only handoff", async () => {
  const { api, calls } = harness();
  const result = await api.createUser(input());
  assert.equal(result.outcome, "CONFIRMED_CREATED");
  assert.equal(result.data.temporaryCredential.password, secret);
  assert.equal(
    result.data.temporaryCredential.expiresAtUtc,
    payload().temporaryCredential.expiresAtUtc,
  );
  assert.equal(result.location, "/api/users/42");
  assert.deepEqual(
    calls.map((c) => [c.url, c.method]),
    [
      ["/api/auth/csrf", "GET"],
      ["/api/users", "POST"],
    ],
  );
  for (const c of calls) {
    assert.equal(c.credentials, "include");
    assert.equal(c.cache, "no-store");
    assert.equal(c.redirect, "error");
    assert.equal(new Headers(c.headers).get("Accept"), "application/json");
    assert.equal(new Headers(c.headers).has("Authorization"), false);
    assert.equal(new Headers(c.headers).has("If-Match"), false);
  }
  assert.equal(new Headers(calls[0].headers).has("X-CSRF-TOKEN"), false);
  assert.equal(calls[0].body, undefined);
  assert.equal(
    new Headers(calls[1].headers).get("Content-Type"),
    "application/json",
  );
  assert.equal(
    new Headers(calls[1].headers).get("X-CSRF-TOKEN"),
    "csrf-request-token",
  );
  assert.deepEqual(JSON.parse(calls[1].body), normalized());
  assert.equal(JSON.stringify(result).includes("csrf-request-token"), false);
});

for (const phone of [undefined, null, "", "   ", " 123 "])
  test(`phone normalization ${String(phone)}`, async () => {
    const { api, calls } = harness();
    assert.equal(
      (await api.createUser({ ...input(), phone })).outcome,
      "CONFIRMED_CREATED",
    );
    assert.equal(
      JSON.parse(calls[1].body).phone,
      phone === " 123 " ? "123" : null,
    );
  });
for (const roleCode of [
  "HK_STAFF",
  "HK_SUPERVISOR",
  "MAINTENANCE_STAFF",
  "MAINTENANCE_LEAD",
  "STOREKEEPER",
  "RECEPTION",
])
  test(`allowed role ${roleCode}`, async () => {
    const { api, calls } = harness();
    await api.createUser({ ...input(), roleCode });
    assert.equal(JSON.parse(calls[1].body).roleCode, roleCode);
  });
for (const [key, value] of [
  ["employeeCode", ""],
  ["username", " "],
  ["fullName", null],
  ["email", ""],
  ["employeeCode", "x".repeat(41)],
  ["username", "x".repeat(101)],
  ["fullName", "x".repeat(201)],
  ["email", "x".repeat(151)],
  ["phone", "x".repeat(21)],
  ["phone", 123],
  ["roleCode", "SYS_ADMIN"],
  ["roleCode", ["HK_STAFF"]],
  ["roleCode", "hk_staff"],
  ...[0, -1, 1.5, NaN, Infinity, Number.MAX_SAFE_INTEGER + 1, "1"].flatMap(
    (v) => [
      ["branchId", v],
      ["departmentId", v],
    ],
  ),
  ...[
    "password",
    "accountStatus",
    "actorId",
    "permissions",
    "scope",
    "expiry",
    "roles",
    "Username",
    "toJSON",
  ].map((k) => [k, secret]),
])
  test(`reject invalid/extra input ${key}:${String(value)}`, async () => {
    const { api, calls } = harness();
    const result = await api.createUser({ ...input(), [key]: value });
    assert.deepEqual(result, {
      outcome: "NOT_DISPATCHED",
      error: { reason: "INVALID_REQUEST" },
    });
    assert.equal(calls.length, 0);
    assert.equal(JSON.stringify(result).includes(secret), false);
  });
test("maximum safe IDs and field lengths; no invented HR/email regex", async () => {
  const { api, calls } = harness();
  await api.createUser({
    ...input(),
    employeeCode: "!".repeat(40),
    username: "x".repeat(100),
    fullName: "x".repeat(200),
    email: "x".repeat(150),
    phone: "x".repeat(20),
    branchId: Number.MAX_SAFE_INTEGER,
    departmentId: Number.MAX_SAFE_INTEGER,
  });
  assert.equal(calls.length, 2);
  assert.equal(JSON.parse(calls[1].body).branchId, Number.MAX_SAFE_INTEGER);
});
test("reject accessors and semantic alias without evaluating secret getter", async () => {
  let evaluated = false;
  const value = input();
  Object.defineProperty(value, "username", {
    get() {
      evaluated = true;
      return secret;
    },
    enumerable: true,
  });
  const { api, calls } = harness();
  assert.equal((await api.createUser(value)).outcome, "NOT_DISPATCHED");
  assert.equal(evaluated, false);
  assert.equal(calls.length, 0);
});
test("snapshot precedes deferred bootstrap; exactly one deferred POST", async () => {
  const get = deferred(),
    post = deferred();
  const { api, calls } = harness(
    () => post.promise,
    () => get.promise,
  );
  const request = input();
  const pending = api.createUser(request);
  request.username = "changed";
  request.password = secret;
  assert.equal(calls.length, 1);
  get.resolve(csrf());
  await new Promise(setImmediate);
  assert.deepEqual(JSON.parse(calls[1].body), normalized());
  assert.equal(calls.length, 2);
  post.resolve(created());
  assert.equal((await pending).outcome, "CONFIRMED_CREATED");
  assert.equal(calls.length, 2);
});
for (const body of [
  {},
  { csrfToken: "token", headerName: "Other" },
  { csrfToken: "bad\nvalue", headerName: "X-CSRF-TOKEN" },
  { csrfToken: "", headerName: "X-CSRF-TOKEN" },
])
  test(`invalid CSRF ${JSON.stringify(body)}`, async () => {
    const { api, calls } = harness(created, () => response(body));
    assert.equal((await api.createUser(input())).outcome, "NOT_DISPATCHED");
    assert.equal(calls.length, 1);
  });
for (const status of [400, 401, 403, 503])
  test(`bootstrap HTTP ${status} never sends POST`, async () => {
    const { api, calls } = harness(created, () =>
      response({ detail: secret }, status),
    );
    const result = await api.createUser(input());
    assert.equal(result.outcome, "NOT_DISPATCHED");
    assert.equal(calls.length, 1);
    assert.equal(JSON.stringify(result).includes(secret), false);
  });
test("bootstrap network failure never dispatches", async () => {
  const { api, calls } = harness(created, () => {
    throw new Error(secret);
  });
  assert.equal((await api.createUser(input())).outcome, "NOT_DISPATCHED");
  assert.equal(calls.length, 1);
});
test("pre-aborted request sends neither GET nor POST", async () => {
  const controller = new AbortController();
  controller.abort();
  const { api, calls } = harness();
  assert.equal(
    (await api.createUser(input(), controller.signal)).outcome,
    "NOT_DISPATCHED",
  );
  assert.equal(calls.length, 0);
});
test("abort during deferred bootstrap prevents POST even if fetch ignores signal", async () => {
  const controller = new AbortController(),
    get = deferred();
  const { api, calls } = harness(created, () => get.promise);
  const pending = api.createUser(input(), controller.signal);
  controller.abort();
  get.resolve(csrf());
  assert.deepEqual(await pending, {
    outcome: "NOT_DISPATCHED",
    error: { reason: "CANCELLED" },
  });
  assert.equal(calls.length, 1);
});
for (const [status, body, reason, outcome] of [
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
  [415, {}, "UNSUPPORTED_MEDIA_TYPE", "REJECTED"],
  [409, { title: "LAST_DURABLE_ADMIN" }, "LAST_DURABLE_ADMIN", "REJECTED"],
  [
    503,
    { title: "GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE" },
    "GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE",
    "OUTCOME_UNKNOWN",
  ],
  [500, {}, "UNEXPECTED_HTTP", "OUTCOME_UNKNOWN"],
  [503, {}, "SERVICE_UNAVAILABLE", "OUTCOME_UNKNOWN"],
  [409, {}, "UNEXPECTED_HTTP", "OUTCOME_UNKNOWN"],
  [204, {}, "UNEXPECTED_HTTP", "OUTCOME_UNKNOWN"],
])
  test(`POST ${status} ${reason} never replays`, async () => {
    const { api, calls } = harness(() =>
      status === 204
        ? new Response(null, { status })
        : response(
            { ...body, detail: secret, traceId: secret, password: secret },
            status,
            { "Content-Type": "application/problem+json" },
          ),
    );
    assert.deepEqual(await api.createUser(input()), {
      outcome,
      error: { status, reason },
    });
    assert.equal(calls.length, 2);
  });
for (const duplicateFields of [[], ["employeeCode", "username", "email"]])
  test(`duplicate fields ${duplicateFields}`, async () => {
    const { api } = harness(() =>
      response(
        {
          title: "Employee code, username or email already exists",
          duplicateFields,
          detail: secret,
        },
        409,
      ),
    );
    assert.deepEqual(await api.createUser(input()), {
      outcome: "REJECTED",
      error: { status: 409, reason: "DUPLICATE", duplicateFields },
    });
  });
test("untrusted duplicate extensions and raw titles do not escape", async () => {
  const { api } = harness(() =>
    response(
      {
        title: "Employee code, username or email already exists",
        duplicateFields: ["username", secret],
        detail: secret,
      },
      409,
    ),
  );
  const result = await api.createUser(input());
  assert.equal(result.outcome, "OUTCOME_UNKNOWN");
  assert.equal(JSON.stringify(result).includes(secret), false);
});
for (const mode of ["network", "abort", "timeout", "body-read"])
  test(`post-dispatch ${mode} stays unknown`, async () => {
    const { api, calls } = harness(() => {
      if (mode === "body-read")
        return {
          status: 201,
          headers: new Headers({ "Content-Type": "application/json" }),
          text: async () => {
            throw new Error(secret);
          },
        };
      throw mode === "abort"
        ? new DOMException(secret, "AbortError")
        : mode === "timeout"
          ? new DOMException(secret, "TimeoutError")
          : new Error(secret);
    });
    const result = await api.createUser(input());
    assert.equal(result.outcome, "OUTCOME_UNKNOWN");
    assert.equal(
      result.error.reason,
      mode === "abort" ? "CANCELLED" : "NETWORK",
    );
    assert.equal(JSON.stringify(result).includes(secret), false);
    assert.equal(calls.length, 2);
  });
for (const [label, mutate, location] of [
  [
    "missing user",
    (p) => {
      delete p.user;
    },
  ],
  [
    "missing field",
    (p) => {
      delete p.user.email;
    },
  ],
  [
    "unsafe ID",
    (p) => {
      p.user.id = Number.MAX_SAFE_INTEGER + 1;
    },
  ],
  [
    "wrong role",
    (p) => {
      p.user.roleCode = "SYS_ADMIN";
    },
  ],
  [
    "bad phone",
    (p) => {
      p.user.phone = 123;
    },
  ],
  [
    "empty secret",
    (p) => {
      p.temporaryCredential.password = "";
    },
  ],
  [
    "missing credential",
    (p) => {
      delete p.temporaryCredential;
    },
  ],
  [
    "bad expiry",
    (p) => {
      p.temporaryCredential.expiresAtUtc = "2026-02-30T00:00:00Z";
    },
  ],
  ["missing location", () => {}, null],
  ["wrong location", () => {}, "/api/users/43"],
  ["absolute location", () => {}, "https://other.test/api/users/42"],
])
  test(`invalid 201 ${label} never exposes secret`, async () => {
    const { api, calls } = harness(() => {
      const p = payload();
      mutate(p);
      return response(
        p,
        201,
        location === null ? {} : { Location: location ?? "/api/users/42" },
      );
    });
    const result = await api.createUser(input());
    assert.equal(result.outcome, "OUTCOME_UNKNOWN");
    assert.equal(result.error.reason, "INVALID_RESPONSE");
    assert.equal(JSON.stringify(result).includes(secret), false);
    assert.equal(calls.length, 2);
  });
for (const type of ["text/html", "application/json"])
  test(`malformed/non-JSON 201 ${type}`, async () => {
    const { api } = harness(
      () =>
        new Response(secret, {
          status: 201,
          headers: { "Content-Type": type, Location: "/api/users/42" },
        }),
    );
    const result = await api.createUser(input());
    assert.equal(result.outcome, "OUTCOME_UNKNOWN");
    assert.equal(JSON.stringify(result).includes(secret), false);
  });
test("only DTO fields survive a valid response", async () => {
  const { api } = harness(() => {
    const p = payload();
    p.csrfToken = secret;
    p.user.permissions = [secret];
    p.temporaryCredential.hash = secret;
    return response(p, 201, { Location: "/api/users/42" });
  });
  assert.deepEqual((await api.createUser(input())).data, payload());
});
test("late 201 after caller abort cannot return credential", async () => {
  const post = deferred(),
    controller = new AbortController();
  const { api, calls } = harness(() => post.promise);
  const pending = api.createUser(input(), controller.signal);
  await new Promise(setImmediate);
  assert.equal(calls.length, 2);
  controller.abort();
  post.resolve(created());
  const result = await pending;
  assert.equal(result.outcome, "OUTCOME_UNKNOWN");
  assert.equal(result.error.reason, "CANCELLED");
  assert.equal(JSON.stringify(result).includes(secret), false);
});
test("each attempt bootstraps afresh and has no runtime mock/backend/storage dependency", async () => {
  let getCount = 0;
  const { api } = harness(created, () => {
    getCount++;
    return csrf();
  });
  await api.createUser(input());
  await api.createUser(input());
  assert.equal(getCount, 2);
});
test("production dependency/security inspection", async () => {
  assert.deepEqual(Object.keys(bundle.metafile.inputs).sort(), [
    "src/users/createUserApi.ts",
  ]);
  const source = await readFile("src/users/createUserApi.ts", "utf8");
  assert.doesNotMatch(
    source,
    /localStorage|sessionStorage|indexedDB|console\.|navigator\.|services\.can|Authorization|setTimeout/,
  );
});

for (const key of [
  "employeeCode",
  "username",
  "fullName",
  "email",
  "branchId",
  "departmentId",
  "roleCode",
]) {
  test(`missing request field ${key} prevents all fetches`, async () => {
    const request = input();
    delete request[key];
    const { api, calls } = harness();
    assert.equal((await api.createUser(request)).outcome, "NOT_DISPATCHED");
    assert.equal(calls.length, 0);
  });
}
for (const expiry of [
  "2026-11-08",
  "2026-11-08T25:00:00Z",
  "2026-11-08T00:60:00Z",
  "0000-01-01T00:00:00Z",
  "invalid",
  null,
]) {
  test(`reject invalid expiry ${expiry}`, async () => {
    const { api } = harness(() => {
      const p = payload();
      p.temporaryCredential.expiresAtUtc = expiry;
      return response(p, 201, { Location: "/api/users/42" });
    });
    const result = await api.createUser(input());
    assert.equal(result.outcome, "OUTCOME_UNKNOWN");
    assert.equal(JSON.stringify(result).includes(secret), false);
  });
}
for (const key of [
  "id",
  "employeeCode",
  "username",
  "fullName",
  "email",
  "phone",
  "branchId",
  "departmentId",
  "roleCode",
  "accountStatus",
]) {
  test(`201 missing required user field ${key}`, async () => {
    const { api } = harness(() => {
      const p = payload();
      delete p.user[key];
      return response(p, 201, { Location: "/api/users/42" });
    });
    assert.equal((await api.createUser(input())).outcome, "OUTCOME_UNKNOWN");
  });
}
for (const status of [401, 403, 503]) {
  test(`unreadable CSRF body preserves actual ${status} without POST`, async () => {
    const { api, calls } = harness(created, () => ({
      status,
      headers: new Headers({ "Content-Type": "application/json" }),
      text: async () => {
        throw new Error(secret);
      },
    }));
    const result = await api.createUser(input());
    assert.equal(result.outcome, "NOT_DISPATCHED");
    assert.equal(result.error.status, status);
    assert.equal(calls.length, 1);
    assert.ok(!JSON.stringify(result).includes(secret));
  });
}
test("unreadable POST401 stays unknown and retains session recovery status", async () => {
  const { api, calls } = harness(() => ({
    status: 401,
    headers: new Headers({ "Content-Type": "application/json" }),
    text: async () => {
      throw new Error(secret);
    },
  }));
  const result = await api.createUser(input());
  assert.equal(result.outcome, "OUTCOME_UNKNOWN");
  assert.equal(result.error.status, 401);
  assert.equal(calls.length, 2);
  assert.ok(!JSON.stringify(result).includes(secret));
});
test("POST transport failure does not inherit successful bootstrap200 status", async () => {
  const { api } = harness(() => {
    throw new Error(secret);
  });
  const result = await api.createUser(input());
  assert.equal(result.outcome, "OUTCOME_UNKNOWN");
  assert.equal(result.error.status, undefined);
});

test("bootstrap malformed JSON and body-read rejection never reach POST", async () => {
  for (const bootstrap of [
    () =>
      new Response("not-json", {
        headers: { "Content-Type": "application/json" },
      }),
    () => ({
      status: 200,
      headers: new Headers({ "Content-Type": "application/json" }),
      text: async () => {
        throw new Error(secret);
      },
    }),
  ]) {
    const { api, calls } = harness(created, bootstrap);
    const result = await api.createUser(input());
    assert.equal(result.outcome, "NOT_DISPATCHED");
    assert.equal(calls.length, 1);
    assert.equal(JSON.stringify(result).includes(secret), false);
  }
});
test("concurrent attempts cannot share a bootstrap token", async () => {
  const gates = [deferred(), deferred()];
  const calls = [];
  let next = 0;
  const api = createCreateUserApi(async (url, options) => {
    calls.push({ url, ...options });
    if (url === "/api/auth/csrf") return gates[next++].promise;
    return created();
  });
  const first = api.createUser(input());
  const second = api.createUser(input());
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
      .filter((c) => c.method === "POST")
      .map((c) => new Headers(c.headers).get("X-CSRF-TOKEN")),
    ["second-token", "first-token"],
  );
  assert.equal(calls.length, 4);
});

for (const [field, wrong] of Object.entries({
  employeeCode: "OTHER",
  username: "another",
  fullName: "Other person",
  email: "other@example.test",
  phone: "123",
  branchId: 99,
  departmentId: 99,
  roleCode: "RECEPTION",
})) {
  test(`201 mismatched ${field} never hands off credential`, async () => {
    const value = payload();
    value.user[field] = wrong;
    const { api, calls } = harness(() =>
      response(value, 201, { Location: "/api/users/42" }),
    );
    const result = await api.createUser(input());
    assert.deepEqual(result, {
      outcome: "OUTCOME_UNKNOWN",
      error: { reason: "INVALID_RESPONSE", status: 201 },
    });
    assert.equal(calls.filter((c) => c.method === "POST").length, 1);
    assert.ok(!JSON.stringify(result).includes(secret));
    assert.ok(!JSON.stringify(result).includes("csrf-request-token"));
  });
}
test("matching success remains correlated to pre-await request snapshot", async () => {
  const wait = deferred(),
    value = input();
  const { api, calls } = harness(created, () => wait.promise);
  const attempt = api.createUser(value);
  value.username = "changed";
  value.branchId = 99;
  wait.resolve(csrf());
  const result = await attempt;
  assert.equal(result.outcome, "CONFIRMED_CREATED");
  assert.equal(result.data.user.username, "staff");
  assert.equal(result.data.user.branchId, 1);
  assert.equal(calls.filter((c) => c.method === "POST").length, 1);
});
