import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { mkdir } from "node:fs/promises";
await mkdir(".test-output/initial-grants", { recursive: true });
await build({
  entryPoints: ["src/roles/initialRolePermissionsApi.ts"],
  bundle: true,
  format: "esm",
  platform: "node",
  outdir: ".test-output/initial-grants",
});
const { createInitialRolePermissionsApi } =
  await import("../.test-output/initial-grants/initialRolePermissionsApi.js");
const grant = { permissionId: 10, dataScope: "BRANCH" };
const token = { headerName: "X-CSRF-TOKEN", csrfToken: "SYNTHETIC-TOKEN" };
const success = { contractVersion: 1, roleId: 7, grants: [grant] };
const reply = (x, status = 200) => Response.json(x, { status });
test("OFF sends no GET or POST", async () => {
  const calls = [];
  const api = createInitialRolePermissionsApi(async (...a) => {
    calls.push(a);
    return reply({});
  });
  assert.equal((await api.save(7, [grant], () => true)).reason, "DISABLED");
  assert.equal(calls.length, 0);
});
test("strict cookie CSRF versioned POST and sanitized 201", async () => {
  const calls = [];
  const api = createInitialRolePermissionsApi(async (u, o) => {
    calls.push({ u, o });
    return u.endsWith("/csrf")
      ? reply(token)
      : reply({ ...success, traceId: "SECRET", token: "SECRET" }, 201);
  }, true);
  const r = await api.save(7, [{ ...grant, userId: 99 }], () => true);
  assert.deepEqual(r, { outcome: "CONFIRMED", roleId: 7, grants: [grant] });
  assert.equal(calls.length, 2);
  assert.equal(calls[1].u, "/api/roles/7/permissions/initial-v1");
  assert.equal(calls[1].o.credentials, "include");
  assert.equal(calls[1].o.cache, "no-store");
  assert.equal(
    new Headers(calls[1].o.headers).get("X-CSRF-TOKEN"),
    token.csrfToken,
  );
  assert.equal(new Headers(calls[1].o.headers).has("If-Match"), false);
  assert.deepEqual(JSON.parse(calls[1].o.body), {
    contractVersion: 1,
    grants: [grant],
  });
  assert.ok(!JSON.stringify(r).includes("SECRET"));
});
for (const status of [400, 401, 403, 404, 409, 503])
  test("HTTP " + status + " no retry or raw problem", async () => {
    let posts = 0;
    const api = createInitialRolePermissionsApi(
      async (u) =>
        u.endsWith("/csrf")
          ? reply(token)
          : (posts++,
            reply(
              { status, code: "SECRET", detail: "SECRET", traceId: "SECRET" },
              status,
            )),
      true,
    );
    const r = await api.save(7, [grant], () => true);
    assert.equal(r.outcome, status === 503 ? "OUTCOME_UNKNOWN" : "REJECTED");
    assert.equal(r.status, status);
    assert.equal(posts, 1);
    assert.ok(!JSON.stringify(r).includes("SECRET"));
  });
for (const status of [401, 403, 503])
  test("CSRF " + status + " preserves status without POST", async () => {
    let n = 0;
    const api = createInitialRolePermissionsApi(async () => {
      n++;
      return reply({ detail: "SECRET" }, status);
    }, true);
    const r = await api.save(7, [grant], () => true);
    assert.equal(r.outcome, "NOT_DISPATCHED");
    assert.equal(r.status, status);
    assert.equal(n, 1);
  });
for (const body of [
  {},
  { ...success, roleId: 8 },
  { ...success, contractVersion: 2 },
  { ...success, grants: [{ ...grant, dataScope: "ALL" }] },
  { ...success, grants: [grant, grant] },
])
  test("malformed success is unknown", async () => {
    let posts = 0;
    const api = createInitialRolePermissionsApi(
      async (u) =>
        u.endsWith("/csrf") ? reply(token) : (posts++, reply(body, 201)),
      true,
    );
    assert.equal(
      (await api.save(7, [grant], () => true)).outcome,
      "OUTCOME_UNKNOWN",
    );
    assert.equal(posts, 1);
  });
test("synchronous exclusion does not queue or replay", async () => {
  let finish;
  let posts = 0;
  const api = createInitialRolePermissionsApi(
    async (u) =>
      u.endsWith("/csrf")
        ? new Promise((r) => (finish = () => r(reply(token))))
        : (posts++, reply(success, 201)),
    true,
  );
  const first = api.save(7, [grant], () => true);
  assert.equal((await api.save(7, [grant], () => true)).reason, "BUSY");
  finish();
  assert.equal((await first).outcome, "CONFIRMED");
  assert.equal(posts, 1);
});
test("identity changed during CSRF never dispatches", async () => {
  let finish,
    current = true,
    n = 0;
  const api = createInitialRolePermissionsApi(async () => {
    n++;
    return new Promise((r) => (finish = () => r(reply(token))));
  }, true);
  const first = api.save(7, [grant], () => current);
  current = false;
  finish();
  assert.equal((await first).reason, "STALE");
  assert.equal(n, 1);
});
test("network failure after dispatch unknown, not rejection", async () => {
  let posts = 0;
  const api = createInitialRolePermissionsApi(async (u) => {
    if (u.endsWith("/csrf")) return reply(token);
    posts++;
    throw Error("SECRET");
  }, true);
  const r = await api.save(7, [grant], () => true);
  assert.equal(r.outcome, "OUTCOME_UNKNOWN");
  assert.equal(posts, 1);
  assert.ok(!JSON.stringify(r).includes("SECRET"));
});
test("invalid scope, empty, duplicate and unsafe ID rejected before transport", async () => {
  let n = 0;
  const api = createInitialRolePermissionsApi(async () => {
    n++;
    return reply({});
  }, true);
  for (const grants of [
    [],
    [grant, grant],
    [{ ...grant, dataScope: "ALL" }],
    [{ ...grant, permissionId: 0 }],
  ])
    assert.equal(
      (await api.save(7, grants, () => true)).outcome,
      "NOT_DISPATCHED",
    );
  assert.equal(
    (await api.save(Number.MAX_SAFE_INTEGER + 1, [grant], () => true)).reason,
    "INVALID_REQUEST",
  );
  assert.equal(n, 0);
});

test("CSRF network failure has no invented HTTP status or POST", async () => {
  let n = 0;
  const api = createInitialRolePermissionsApi(async () => {
    n++;
    throw Error("SECRET");
  }, true);
  const r = await api.save(7, [grant], () => true);
  assert.equal(r.outcome, "NOT_DISPATCHED");
  assert.equal(r.status, undefined);
  assert.equal(n, 1);
});
test("cancel after POST is unknown without replay or invented status", async () => {
  let posts = 0;
  const abort = new AbortController();
  const api = createInitialRolePermissionsApi(async (u) => {
    if (u.endsWith("/csrf")) return reply(token);
    posts++;
    abort.abort();
    throw new DOMException("SECRET", "AbortError");
  }, true);
  const r = await api.save(7, [grant], () => true, abort.signal);
  assert.equal(r.outcome, "OUTCOME_UNKNOWN");
  assert.equal(r.status, undefined);
  assert.equal(posts, 1);
  assert.ok(!JSON.stringify(r).includes("SECRET"));
});
