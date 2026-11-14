import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";

const bundle = await build({
  entryPoints: ["src/users/usersApi.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  write: false,
  metafile: true,
});
const { createUsersApi } = await import(
  `data:text/javascript;base64,${Buffer.from(bundle.outputFiles[0].text).toString("base64")}`
);

const secret = "PRIVATE-USERS-TEST-SECRET";
const stamp = "2026-11-07T08:30:00.1234567Z";
const branch = () => ({ id: 1, code: "B1", name: "Branch One" });
const department = () => ({ id: 2, code: "D1", name: "Department One" });
const account = () => ({
  id: 42,
  employeeCode: "E42",
  username: "staff",
  fullName: "Staff Member",
  email: "staff@example.test",
  phone: null,
  accountStatus: "ACTIVE",
  primaryBranch: branch(),
  department: department(),
});
const directory = () => {
  const { phone: _phone, ...item } = account();
  return {
    items: [
      {
        ...item,
        roles: [{ id: 3, code: "R1", name: "Role One", branch: branch() }],
      },
    ],
    page: 1,
    pageSize: 20,
    totalCount: 1,
  };
};
const review = () => ({
  account: account(),
  evaluatedAtUtc: stamp,
  reviewerScope: { isGlobal: false, branchIds: [1] },
  assignedRoles: [
    {
      assignmentId: 10,
      roleId: 3,
      code: "R1",
      name: "Role One",
      branchId: 1,
      assignmentActive: true,
      roleActive: true,
      assignedAtUtc: stamp,
      expiresAtUtc: null,
    },
  ],
  branchMemberships: [
    {
      branchId: 1,
      code: "B1",
      name: "Branch One",
      accessLevel: "STANDARD",
      branchActive: true,
      grantedAtUtc: stamp,
      revokedAtUtc: null,
    },
  ],
  configuredGrants: [
    {
      permissionCode: "USER_VIEW",
      permissionName: "View users",
      feature: "ADMIN",
      configuredScope: "BRANCH",
      assignmentId: 10,
      roleCode: "R1",
      branchId: 1,
      isEffective: true,
    },
  ],
  effectivePermissions: [
    {
      code: "USER_VIEW",
      name: "View users",
      feature: "ADMIN",
      sources: [
        {
          assignmentId: 10,
          roleCode: "R1",
          scope: "BRANCH",
          branchId: 1,
          requiresResourceOwnership: false,
        },
      ],
    },
  ],
});
const endpoints = [
  {
    name: "directory",
    path: "/api/users",
    fixture: directory,
    read: (api, signal) => api.listUsers({}, signal),
  },
  {
    name: "detail",
    path: "/api/users/42",
    fixture: account,
    read: (api, signal) => api.getUser(42, signal),
  },
  {
    name: "review",
    path: "/api/users/42/effective-access",
    fixture: review,
    read: (api, signal) => api.getEffectiveAccess(42, {}, signal),
  },
];
const json = (value, status = 200, contentType = "application/json") =>
  new Response(JSON.stringify(value), {
    status,
    headers: { "Content-Type": contentType },
  });
const invalidResponse = { ok: false, error: { kind: "invalid-response" } };
const invalidRequest = { ok: false, error: { kind: "invalid-request" } };
const cancelled = { ok: false, error: { kind: "cancelled" } };
function harness(...replies) {
  const calls = [];
  const api = createUsersApi(async (url, init) => {
    calls.push({ url, ...init });
    assert.ok(replies.length, "Unexpected fetch/retry");
    const reply = replies.shift();
    return typeof reply === "function" ? reply(init) : reply;
  });
  return { api, calls };
}
function request(call, expectedUrl, signal) {
  assert.equal(call.url, expectedUrl);
  assert.equal(call.method, "GET");
  assert.equal(call.credentials, "include");
  assert.equal(call.cache, "no-store");
  assert.equal(call.redirect, "error");
  assert.deepEqual([...call.headers], [["accept", "application/json"]]);
  assert.equal(call.body, undefined);
  assert.equal(call.signal, signal);
}
function set(value, path, replacement) {
  const keys = path.split(".");
  const last = keys.pop();
  let target = value;
  for (const key of keys) target = target[key];
  target[last] = replacement;
}
function get(value, path) {
  return path.split(".").reduce((v, key) => v[key], value);
}

for (const endpoint of endpoints) {
  test(`${endpoint.name}: exact GET contract, AbortSignal and validated response`, async () => {
    const data = endpoint.fixture();
    const signal = new AbortController().signal;
    const { api, calls } = harness(json(data));
    assert.deepEqual(await endpoint.read(api, signal), {
      ok: true,
      status: 200,
      data,
    });
    assert.equal(calls.length, 1);
    request(calls[0], endpoint.path, signal);
  });
  test(`${endpoint.name}: arbitrary response extensions are not exposed`, async () => {
    const data = endpoint.fixture();
    const decorate = (value) => {
      if (Array.isArray(value)) value.forEach(decorate);
      else if (value !== null && typeof value === "object") {
        Object.values(value).forEach(decorate);
        Object.assign(value, {
          password: secret,
          sessionToken: secret,
          csrfToken: secret,
          unexpected: { detail: secret },
        });
      }
    };
    decorate(data);
    const { api } = harness(json(data));
    const result = await endpoint.read(api);
    assert.deepEqual(result, {
      ok: true,
      status: 200,
      data: endpoint.fixture(),
    });
    assert.ok(!JSON.stringify(result).includes(secret));
  });
}

test("list whitelists and encodes every query parameter without changing the path", async () => {
  const { api, calls } = harness(json(directory()));
  await api.listUsers({
    search: "staff & role=admin/#?",
    branchId: 1,
    departmentId: 2,
    roleId: 3,
    accountStatus: "INACTIVE",
    page: 2,
    pageSize: 10,
  });
  assert.equal(calls.length, 1);
  request(
    calls[0],
    "/api/users?search=staff+%26+role%3Dadmin%2F%23%3F&branchId=1&departmentId=2&roleId=3&accountStatus=INACTIVE&page=2&pageSize=10",
  );
});
test("review query is separate from list and cannot inject extra parameters", async () => {
  const { api, calls } = harness(json(review()));
  await api.getEffectiveAccess(42, {
    feature: "ADMIN&branchId=2",
    permissionCode: "USER_VIEW/+?#",
  });
  request(
    calls[0],
    "/api/users/42/effective-access?feature=ADMIN%26branchId%3D2&permissionCode=USER_VIEW%2F%2B%3F%23",
  );
});
test("defaults omit query and undefined values; strings are not silently coerced or trimmed", async () => {
  const { api, calls } = harness(
    json(directory()),
    json(directory()),
    json(review()),
  );
  await api.listUsers();
  await api.listUsers({ search: "  Staff  ", branchId: undefined });
  await api.getEffectiveAccess(42);
  assert.deepEqual(
    calls.map((call) => call.url),
    [
      "/api/users",
      "/api/users?search=++Staff++",
      "/api/users/42/effective-access",
    ],
  );
});
test("empty directory and pages beyond last page are valid responses", async () => {
  const data = { items: [], page: 9, pageSize: 100, totalCount: 2 };
  const { api } = harness(json(data));
  assert.deepEqual(await api.listUsers({ page: 9, pageSize: 100 }), {
    ok: true,
    status: 200,
    data,
  });
});
test("nullable/redacted detail and review fields are preserved without inferring missing memberships", async () => {
  const detail = {
    ...account(),
    phone: null,
    primaryBranch: null,
    department: null,
  };
  const report = { ...review(), account: detail };
  report.assignedRoles[0].branchId = null;
  report.configuredGrants[0].branchId = null;
  report.configuredGrants[0].feature = null;
  report.effectivePermissions[0].feature = null;
  report.effectivePermissions[0].sources[0].branchId = null;
  const { api } = harness(json(detail), json(report));
  assert.deepEqual((await api.getUser(42)).data, detail);
  assert.deepEqual((await api.getEffectiveAccess(42)).data, report);
});
test("branch-only report stays clipped and is not expanded using list or detail data", async () => {
  const data = review();
  data.account.primaryBranch = null;
  data.account.department = null;
  const { api } = harness(json(data));
  assert.deepEqual((await api.getEffectiveAccess(42)).data, data);
});
test("inactive assignments/configured grants are not turned into effective permissions", async () => {
  const data = review();
  data.account.accountStatus = "INACTIVE";
  data.assignedRoles[0].assignmentActive = false;
  data.assignedRoles[0].expiresAtUtc = "2025-01-01T00:00:00Z";
  data.branchMemberships[0].revokedAtUtc = stamp;
  data.configuredGrants[0].isEffective = false;
  data.effectivePermissions = [];
  const { api } = harness(json(data));
  assert.deepEqual((await api.getEffectiveAccess(42)).data, data);
});
test("OWN provenance is preserved and never normalized to BRANCH or CanExecute", async () => {
  const data = review();
  data.configuredGrants[0].configuredScope = "OWN";
  Object.assign(data.effectivePermissions[0].sources[0], {
    scope: "OWN",
    requiresResourceOwnership: true,
  });
  const { api } = harness(json(data));
  assert.deepEqual((await api.getEffectiveAccess(42)).data, data);
});
test("global report with empty permissions remains a report, not a permission grant", async () => {
  const data = {
    ...review(),
    reviewerScope: { isGlobal: true, branchIds: [] },
    assignedRoles: [],
    branchMemberships: [],
    configuredGrants: [],
    effectivePermissions: [],
  };
  const { api } = harness(json(data));
  assert.deepEqual((await api.getEffectiveAccess(42)).data, data);
});

const idPaths = [
  [0, "items.0.id"],
  [0, "items.0.primaryBranch.id"],
  [0, "items.0.department.id"],
  [0, "items.0.roles.0.id"],
  [0, "items.0.roles.0.branch.id"],
  [1, "id"],
  [1, "primaryBranch.id"],
  [1, "department.id"],
  [2, "account.id"],
  [2, "account.primaryBranch.id"],
  [2, "account.department.id"],
  [2, "reviewerScope.branchIds.0"],
  [2, "assignedRoles.0.assignmentId"],
  [2, "assignedRoles.0.roleId"],
  [2, "assignedRoles.0.branchId", true],
  [2, "branchMemberships.0.branchId"],
  [2, "configuredGrants.0.assignmentId"],
  [2, "configuredGrants.0.branchId", true],
  [2, "effectivePermissions.0.sources.0.assignmentId"],
  [2, "effectivePermissions.0.sources.0.branchId", true],
];
for (const [index, path, allowsNull] of idPaths) {
  const endpoint = endpoints[index];
  test(`${endpoint.name}: positive safe integer policy at ${path}`, async () => {
    for (const bad of [
      Number.MAX_SAFE_INTEGER + 1,
      9223372036854775807,
      0,
      -1,
      1.5,
      "42",
      true,
      {},
      ...(allowsNull ? [] : [null]),
    ]) {
      const data = endpoint.fixture();
      set(data, path, bad);
      const { api, calls } = harness(json(data));
      assert.deepEqual(
        await endpoint.read(api),
        invalidResponse,
        `${path}: ${JSON.stringify(bad)}`,
      );
      assert.equal(calls.length, 1);
    }
    for (const accepted of [
      1,
      Number.MAX_SAFE_INTEGER,
      ...(allowsNull ? [null] : []),
    ]) {
      const data = endpoint.fixture();
      set(data, path, accepted);
      const { api } = harness(json(data));
      const result = await endpoint.read(api);
      assert.equal(result.ok, true);
      assert.equal(get(result.data, path), accepted);
    }
  });
}
test("raw unsafe Int64 JSON literal is rejected instead of rounded into a user target", async () => {
  const body = JSON.stringify(account()).replace(
    '"id":42',
    '"id":9007199254740993',
  );
  const { api } = harness(
    new Response(body, { headers: { "Content-Type": "application/json" } }),
  );
  assert.deepEqual(await api.getUser(42), invalidResponse);
});

const malformed = [
  [0, "items", {}],
  [0, "items.0.fullName", null],
  [0, "items.0.employeeCode", 1],
  [0, "items.0.roles", null],
  [0, "items.0.roles.0.branch", undefined],
  [0, "items.0.accountStatus", false],
  [0, "items.0.primaryBranch.name", 1],
  [0, "page", 0],
  [0, "page", 1.5],
  [0, "pageSize", 101],
  [0, "totalCount", -1],
  [1, "phone", 123],
  [1, "username", undefined],
  [1, "primaryBranch", []],
  [2, "account", null],
  [2, "reviewerScope.isGlobal", "false"],
  [2, "reviewerScope.branchIds", null],
  [2, "assignedRoles", {}],
  [2, "assignedRoles.0.assignmentActive", 1],
  [2, "assignedRoles.0.roleActive", "true"],
  [2, "branchMemberships.0.branchActive", null],
  [2, "configuredGrants.0.isEffective", "true"],
  [2, "configuredGrants.0.feature", 12],
  [2, "configuredGrants.0.configuredScope", null],
  [2, "effectivePermissions.0.sources", {}],
  [2, "effectivePermissions.0.sources.0.requiresResourceOwnership", "false"],
  [2, "effectivePermissions.0.feature", undefined],
  [2, "evaluatedAtUtc", "2026-02-30T00:00:00Z"],
  [2, "assignedRoles.0.assignedAtUtc", "2026-11-07T08:00:00"],
  [2, "assignedRoles.0.expiresAtUtc", "invalid"],
  [2, "branchMemberships.0.grantedAtUtc", 123],
  [2, "branchMemberships.0.revokedAtUtc", "2026-11-07T25:00:00Z"],
];
test("malformed/missing DTO fields fail closed across all response sections", async () => {
  for (const [index, path, replacement] of malformed) {
    const endpoint = endpoints[index],
      data = endpoint.fixture();
    set(data, path, replacement);
    const { api } = harness(json(data));
    assert.deepEqual(
      await endpoint.read(api),
      invalidResponse,
      `${endpoint.name}:${path}`,
    );
  }
});

const badListQueries = [
  null,
  [],
  "search",
  { search: "a".repeat(201) },
  { search: 42 },
  { search: null },
  { branchId: "1" },
  { branchId: 0 },
  { branchId: 1.5 },
  { branchId: Number.MAX_SAFE_INTEGER + 1 },
  { departmentId: -1 },
  { departmentId: "2" },
  { roleId: true },
  { roleId: Infinity },
  { accountStatus: "Active" },
  { accountStatus: "LOCKED" },
  { accountStatus: null },
  { page: 0 },
  { page: 1.5 },
  { page: "1" },
  { page: 2147483648 },
  { page: NaN },
  { pageSize: 0 },
  { pageSize: 101 },
  { pageSize: "20" },
  { pageSize: 1.5 },
  { page: 21474838, pageSize: 100 },
  { page: 107374184 },
  { sort: "fullName" },
  { includeAccess: true },
  { actorId: 42 },
  { url: "https://example.test" },
];
test("invalid list query/filter is a sanitized local invalid-request with no fetch", async () => {
  for (const query of badListQueries) {
    const { api, calls } = harness();
    assert.deepEqual(await api.listUsers(query), invalidRequest);
    assert.equal(calls.length, 0);
  }
});
test("review query rejects invalid types, excessive lengths and non-whitelisted keys", async () => {
  for (const query of [
    null,
    [],
    { feature: 42 },
    { feature: "a".repeat(61) },
    { permissionCode: "a".repeat(101) },
    { permissionCode: null },
    { branchId: 1 },
    { includeAccess: true },
  ]) {
    const { api, calls } = harness();
    assert.deepEqual(await api.getEffectiveAccess(42, query), invalidRequest);
    assert.equal(calls.length, 0);
  }
});
test("request IDs require numeric positive safe integers, never Auth SessionDto strings", async () => {
  for (const value of [
    "42",
    "../42",
    "42?includeAccess=true",
    0,
    -1,
    1.5,
    Number.MAX_SAFE_INTEGER + 1,
    null,
    true,
    NaN,
    Infinity,
  ]) {
    const { api, calls } = harness();
    assert.deepEqual(await api.getUser(value), invalidRequest);
    assert.deepEqual(await api.getEffectiveAccess(value), invalidRequest);
    assert.equal(calls.length, 0);
  }
});
test("valid query length, page offset and safe-ID boundaries pass without coercion", async () => {
  const { api, calls } = harness(
    json(directory()),
    json(review()),
    json(account()),
  );
  await api.listUsers({
    search: "a".repeat(200),
    branchId: Number.MAX_SAFE_INTEGER,
    departmentId: 2,
    roleId: 3,
    accountStatus: "ACTIVE",
    page: 2147483647,
    pageSize: 1,
  });
  await api.getEffectiveAccess(Number.MAX_SAFE_INTEGER, {
    feature: "a".repeat(60),
    permissionCode: "b".repeat(100),
  });
  await api.getUser(Number.MAX_SAFE_INTEGER);
  assert.equal(calls.length, 3);
  assert.equal(calls[2].url, `/api/users/${Number.MAX_SAFE_INTEGER}`);
  assert.ok(calls.every((call) => call.method === "GET"));
});

for (const endpoint of endpoints) {
  for (const status of [400, 401, 403, 404, 500, 503]) {
    test(`${endpoint.name}: HTTP ${status} stays distinct, sanitized and never retried`, async () => {
      const { api, calls } = harness(
        json(
          {
            status,
            title: secret,
            detail: secret,
            traceId: secret,
            errors: { password: [secret] },
            code: secret,
          },
          status,
          "application/problem+json",
        ),
      );
      assert.deepEqual(await endpoint.read(api), {
        ok: false,
        error: { kind: "http", status },
      });
      assert.equal(calls.length, 1);
      request(calls[0], endpoint.path);
    });
  }
  test(`${endpoint.name}: network and AbortError do not leak exception text or retry`, async () => {
    for (const [error, kind] of [
      [new Error(secret), "network"],
      [new DOMException(secret, "AbortError"), "cancelled"],
    ]) {
      const { api, calls } = harness(() => {
        throw error;
      });
      assert.deepEqual(await endpoint.read(api), {
        ok: false,
        error: { kind },
      });
      assert.equal(calls.length, 1);
    }
  });
  test(`${endpoint.name}: pre-aborted read never fetches`, async () => {
    const controller = new AbortController();
    controller.abort(secret);
    const { api, calls } = harness();
    assert.deepEqual(await endpoint.read(api, controller.signal), cancelled);
    assert.equal(calls.length, 0);
  });
}
test("known ProblemDetails codes are whitelisted without exposing other fields", async () => {
  for (const code of [
    "VALIDATION_FAILED",
    "AUTHENTICATION_REQUIRED",
    "SESSION_INVALID",
    "FORBIDDEN",
    "PERMISSION_DENIED",
    "PASSWORD_CHANGE_REQUIRED",
    "AUTH_SERVICE_UNAVAILABLE",
    "INTERNAL_SERVER_ERROR",
  ]) {
    const { api } = harness(
      json(
        { status: 403, code, title: secret, detail: secret, traceId: secret },
        403,
      ),
    );
    assert.deepEqual(await api.getUser(42), {
      ok: false,
      error: { kind: "http", status: 403, code },
    });
  }
});
test("mismatched ProblemDetails status cannot attach a misleading code", async () => {
  const { api } = harness(json({ status: 401, code: "SESSION_INVALID" }, 403));
  assert.deepEqual(await api.getUser(42), {
    ok: false,
    error: { kind: "http", status: 403 },
  });
});
test("minimal detail 403 does not authorize or prevent an independent branch review", async () => {
  const data = review();
  const { api, calls } = harness(
    json({ status: 403, code: "FORBIDDEN" }, 403),
    json(data),
  );
  assert.equal((await api.getUser(42)).error.status, 403);
  assert.deepEqual(await api.getEffectiveAccess(42), {
    ok: true,
    status: 200,
    data,
  });
  assert.deepEqual(
    calls.map((call) => call.url),
    ["/api/users/42", "/api/users/42/effective-access"],
  );
});
test("401 never triggers session revalidation, redirects or implicit extra reads", async () => {
  const { api, calls } = harness(
    json({ status: 401, code: "SESSION_INVALID" }, 401),
  );
  assert.deepEqual(await api.listUsers(), {
    ok: false,
    error: { kind: "http", status: 401, code: "SESSION_INVALID" },
  });
  assert.equal(calls.length, 1);
  assert.equal(calls[0].url, "/api/users");
  assert.deepEqual(Object.keys(api).sort(), [
    "getEffectiveAccess",
    "getUser",
    "listUsers",
  ]);
});
test("concurrent reads stay independent and expose cancellation for caller-owned lifecycle", async () => {
  let resolve;
  const controller = new AbortController();
  const { api, calls } = harness(
    () =>
      new Promise((r) => {
        resolve = r;
      }),
    json(review()),
  );
  const old = api.getUser(42, controller.signal);
  assert.equal((await api.getEffectiveAccess(42)).ok, true);
  controller.abort();
  resolve(json(account())); // A fetch double ignoring abort cannot publish success after cancellation.
  assert.deepEqual(await old, cancelled);
  assert.equal(calls.length, 2);
});
test("AbortSignal is forwarded to an abort-aware fetch implementation", async () => {
  const controller = new AbortController();
  const { api, calls } = harness(
    (init) =>
      new Promise((_, reject) => {
        init.signal.addEventListener(
          "abort",
          () => reject(new DOMException(secret, "AbortError")),
          { once: true },
        );
      }),
  );
  const pending = api.listUsers({}, controller.signal);
  controller.abort();
  assert.deepEqual(await pending, cancelled);
  request(calls[0], "/api/users", controller.signal);
});

test("HTML, malformed JSON, null/array roots and wrong success status are invalid responses", async () => {
  for (const reply of [
    new Response(secret, { headers: { "Content-Type": "text/html" } }),
    new Response("{", { headers: { "Content-Type": "application/json" } }),
    json(null),
    json([]),
    json(account(), 201),
    json(account(), 200, "application/problem+json"),
    new Response(null, { status: 204 }),
  ]) {
    const { api, calls } = harness(reply);
    assert.deepEqual(await api.getUser(42), invalidResponse);
    assert.equal(calls.length, 1);
  }
});
test("non-JSON and malformed error bodies preserve HTTP status without exposing content", async () => {
  for (const reply of [
    new Response(secret, { status: 503 }),
    new Response("{", {
      status: 500,
      headers: { "Content-Type": "application/problem+json" },
    }),
  ]) {
    const expectedStatus = reply.status;
    const { api } = harness(reply);
    assert.deepEqual(await api.getUser(42), {
      ok: false,
      error: { kind: "http", status: expectedStatus },
    });
  }
});
test("body read failures distinguish network from cancellation without exposing raw exceptions", async () => {
  for (const [error, kind] of [
    [new Error(secret), "network"],
    [new DOMException(secret, "AbortError"), "cancelled"],
  ]) {
    const reply = json(account());
    reply.text = async () => {
      throw error;
    };
    const { api } = harness(reply);
    assert.deepEqual(await api.getUser(42), { ok: false, error: { kind } });
  }
});
test("valid UTC precision/calendar values are preserved and invalid calendar values rejected", async () => {
  for (const value of [
    "0001-01-01T00:00:00Z",
    "2000-02-29T23:59:59.1234567Z",
  ]) {
    const data = review();
    data.evaluatedAtUtc = value;
    const { api } = harness(json(data));
    assert.equal((await api.getEffectiveAccess(42)).data.evaluatedAtUtc, value);
  }
  for (const value of [
    "0000-01-01T00:00:00Z",
    "1900-02-29T00:00:00Z",
    "2026-13-01T00:00:00Z",
    "2026-01-01T00:00:60Z",
    "2026-01-01T00:00:00+00:00",
  ]) {
    const data = review();
    data.evaluatedAtUtc = value;
    const { api } = harness(json(data));
    assert.deepEqual(await api.getEffectiveAccess(42), invalidResponse);
  }
});
test("production import graph has no authentication state, mock services, UI or storage initializers", () => {
  const inputs = Object.keys(bundle.metafile.inputs).map((path) =>
    path.replaceAll("\\", "/"),
  );
  assert.deepEqual(inputs, ["src/users/usersApi.ts"]);
  assert.ok(
    !/localStorage|sessionStorage|console\.(log|error|warn)|Authorization|includeAccess=true/.test(
      bundle.outputFiles[0].text,
    ),
  );
});
test("all three reads work with forbidden browser storage and make no console calls", async () => {
  const keys = ["localStorage", "sessionStorage"];
  const original = keys.map((key) =>
    Object.getOwnPropertyDescriptor(globalThis, key),
  );
  const logMethods = ["log", "warn", "error"],
    originalLogs = logMethods.map((key) => console[key]);
  let storageTouches = 0,
    logTouches = 0;
  try {
    for (const key of keys)
      Object.defineProperty(globalThis, key, {
        configurable: true,
        get() {
          storageTouches++;
          throw new Error(secret);
        },
      });
    for (const key of logMethods)
      console[key] = () => {
        logTouches++;
      };
    const { api } = harness(
      json(directory()),
      json(account()),
      json(review()),
      () => {
        throw new Error(secret);
      },
    );
    await api.listUsers();
    await api.getUser(42);
    await api.getEffectiveAccess(42);
    assert.deepEqual(await api.getUser(42), {
      ok: false,
      error: { kind: "network" },
    });
    assert.equal(storageTouches, 0);
    assert.equal(logTouches, 0);
  } finally {
    keys.forEach((key, index) => {
      if (original[index])
        Object.defineProperty(globalThis, key, original[index]);
      else delete globalThis[key];
    });
    logMethods.forEach((key, index) => {
      console[key] = originalLogs[index];
    });
  }
});

for (const [name, call] of [
  ["list", (a) => a.listUsers()],
  ["detail", (a) => a.getUser(42)],
  ["access", (a) => a.getEffectiveAccess(42)],
]) {
  for (const status of [401, 403, 503])
    for (const contentType of ["application/json", "application/problem+json"])
      test(
        "092 unreadable " + name + " " + status + " " + contentType,
        async () => {
          const seen = [];
          const api = createUsersApi(async (url, init) => {
            seen.push(init.method);
            const response = new Response("", {
              status,
              headers: { "Content-Type": contentType },
            });
            response.text = async () => {
              throw Error("PRIVATE-092-ERROR");
            };
            return response;
          });
          const r = await call(api);
          assert.equal(r.ok, false);
          assert.equal(r.error.kind, "http");
          assert.equal(r.error.status, status);
          assert.deepEqual(seen, ["GET"]);
          assert.ok(!JSON.stringify(r).includes("PRIVATE-092"));
        },
      );
  for (const failure of ["network", "successful-body", "malformed-success"])
    test(
      "092 " + name + " " + failure + " has no invented status",
      async () => {
        const api = createUsersApi(async () => {
          if (failure === "network") throw Error("PRIVATE-092");
          const response = new Response("invalid", {
            headers: { "Content-Type": "application/json" },
          });
          if (failure === "successful-body")
            response.text = async () => {
              throw Error("PRIVATE-092");
            };
          return response;
        });
        const r = await call(api);
        assert.equal(r.ok, false);
        assert.equal(r.error.status, undefined);
        assert.ok(!JSON.stringify(r).includes("PRIVATE-092"));
      },
    );
}

test("092 cancellation during failed body read wins over observed status", async () => {
  const abort = new AbortController();
  const api = createUsersApi(async () => {
    const response = new Response("", {
      status: 401,
      headers: { "Content-Type": "application/json" },
    });
    response.text = async () => {
      abort.abort();
      throw Error("PRIVATE-092");
    };
    return response;
  });
  const r = await api.listUsers({}, abort.signal);
  assert.equal(r.error.kind, "cancelled");
  assert.equal(r.error.status, undefined);
});
