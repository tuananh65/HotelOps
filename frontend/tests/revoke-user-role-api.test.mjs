import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
const b = await build({
  entryPoints: ["src/users/revokeUserRoleApi.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  write: false,
});
const { createRevokeUserRoleApi } = await import(
  "data:text/javascript;base64," +
    Buffer.from(b.outputFiles[0].text).toString("base64")
);
const old = '"0011223344556677"',
  next = '"0011223344556678"',
  secret = "SYNTHETIC-PRIVATE-REVOKE";
const csrf = () =>
  new Response(
    JSON.stringify({ headerName: "X-CSRF-TOKEN", csrfToken: secret }),
    { headers: { "Content-Type": "application/json" } },
  );
function harness(
  reply = () => new Response(null, { status: 204, headers: { ETag: next } }),
  enabled = true,
  bootstrap = csrf,
) {
  const calls = [];
  return {
    calls,
    api: createRevokeUserRoleApi(async (url, init) => {
      calls.push({ url, ...init });
      return url === "/api/auth/csrf" ? bootstrap() : reply();
    }, enabled),
  };
}
test("default dispatch OFF sends zero requests", async () => {
  let n = 0;
  const api = createRevokeUserRoleApi(async () => {
    n++;
    return csrf();
  });
  assert.equal((await api.revoke(42, 18, old, () => true)).reason, "DISABLED");
  assert.equal(n, 0);
});
test("confirmed 204 requires fresh User ETag and exact bodyless DELETE", async () => {
  const { api, calls } = harness();
  assert.deepEqual(await api.revoke(42, 18, old, () => true), {
    outcome: "CONFIRMED",
    etag: next,
  });
  assert.deepEqual(
    calls.map((c) => [c.url, c.method]),
    [
      ["/api/auth/csrf", "GET"],
      ["/api/users/42/roles/18", "DELETE"],
    ],
  );
  const c = calls[1];
  assert.equal(c.body, undefined);
  assert.equal(new Headers(c.headers).get("If-Match"), old);
  assert.equal(new Headers(c.headers).get("X-CSRF-TOKEN"), secret);
  assert.equal(new Headers(c.headers).has("Authorization"), false);
  for (const r of calls) {
    assert.equal(r.credentials, "include");
    assert.equal(r.cache, "no-store");
  }
});
for (const status of [400, 401, 403, 404, 412, 428, 503, 500])
  test("safe HTTP " + status, async () => {
    const { api, calls } = harness(
      () =>
        new Response(JSON.stringify({ detail: secret, traceId: secret }), {
          status,
          headers: { "Content-Type": "application/problem+json" },
        }),
    );
    const r = await api.revoke(42, 18, old, () => true);
    assert.equal(r.outcome, status >= 500 ? "OUTCOME_UNKNOWN" : "REJECTED");
    assert.equal(r.status, status);
    assert.equal(calls.length, 2);
    assert.ok(!JSON.stringify(r).includes(secret));
  });
for (const status of [401, 403, 503])
  test("CSRF " + status + " preserves status without DELETE", async () => {
    const { api, calls } = harness(
      undefined,
      true,
      () => new Response(null, { status }),
    );
    const r = await api.revoke(42, 18, old, () => true);
    assert.equal(r.outcome, "NOT_DISPATCHED");
    assert.equal(r.status, status);
    assert.equal(calls.length, 1);
  });
for (const etag of [undefined, old, "W/" + next, '"BAD"'])
  test("204 invalid/unchanged ETag " + etag, async () => {
    const { api, calls } = harness(
      () =>
        new Response(null, {
          status: 204,
          headers: etag ? { ETag: etag } : {},
        }),
    );
    assert.equal(
      (await api.revoke(42, 18, old, () => true)).outcome,
      "OUTCOME_UNKNOWN",
    );
    assert.equal(calls.length, 2);
  });
for (const args of [
  [0, 18, old],
  [42, 0, old],
  [42, 18, "*"],
])
  test("invalid selectors " + args.join(), async () => {
    const { api, calls } = harness();
    assert.equal(
      (await api.revoke(...args, () => true)).reason,
      "INVALID_REQUEST",
    );
    assert.equal(calls.length, 0);
  });
test("stale after CSRF never deletes", async () => {
  let current = true;
  const { api, calls } = harness(undefined, true, () => {
    current = false;
    return csrf();
  });
  assert.equal((await api.revoke(42, 18, old, () => current)).reason, "STALE");
  assert.equal(calls.length, 1);
});
test("concurrent confirm excluded synchronously without queue/retry", async () => {
  let release;
  const { api, calls } = harness(
    undefined,
    true,
    () => new Promise((r) => (release = r)),
  );
  const first = api.revoke(42, 18, old, () => true);
  assert.equal((await api.revoke(42, 18, old, () => true)).reason, "BUSY");
  release(csrf());
  assert.equal((await first).outcome, "CONFIRMED");
  assert.equal(calls.filter((c) => c.method === "DELETE").length, 1);
});
test("network outcome unknown never retries or exposes error", async () => {
  const { api, calls } = harness(() => {
    throw Error(secret);
  });
  const r = await api.revoke(42, 18, old, () => true);
  assert.equal(r.outcome, "OUTCOME_UNKNOWN");
  assert.ok(!JSON.stringify(r).includes(secret));
  assert.equal(calls.length, 2);
});
for (const code of [
  "ROLE_ASSIGNMENT_CONFLICT",
  "LAST_DURABLE_ADMIN",
  "unknown",
])
  test("409 " + code, async () => {
    const { api } = harness(
      () =>
        new Response(
          JSON.stringify({ status: 409, code, title: code, detail: secret }),
          {
            status: 409,
            headers: { "Content-Type": "application/problem+json" },
          },
        ),
    );
    assert.equal(
      (await api.revoke(42, 18, old, () => true)).outcome,
      code === "unknown" ? "OUTCOME_UNKNOWN" : "REJECTED",
    );
  });
test("identity change after DELETE makes success indeterminate", async () => {
  let current = true;
  const { api } = harness(() => {
    current = false;
    return new Response(null, { status: 204, headers: { ETag: next } });
  });
  assert.equal(
    (await api.revoke(42, 18, old, () => current)).outcome,
    "OUTCOME_UNKNOWN",
  );
});
test("contradictory409 stays unknown without raw disclosure", async () => {
  const { api } = harness(
    () =>
      new Response(
        JSON.stringify({
          status: 409,
          code: "ROLE_ASSIGNMENT_CONFLICT",
          title: "LAST_DURABLE_ADMIN",
          detail: secret,
        }),
        {
          status: 409,
          headers: { "Content-Type": "application/problem+json" },
        },
      ),
  );
  const r = await api.revoke(42, 18, old, () => true);
  assert.equal(r.outcome, "OUTCOME_UNKNOWN");
  assert.ok(!JSON.stringify(r).includes(secret));
});
test("malformed bootstrap never deletes or invents status", async () => {
  const { api, calls } = harness(
    undefined,
    true,
    () =>
      new Response("{", { headers: { "Content-Type": "application/json" } }),
  );
  const r = await api.revoke(42, 18, old, () => true);
  assert.equal(r.outcome, "NOT_DISPATCHED");
  assert.equal(r.status, undefined);
  assert.equal(calls.length, 1);
});
test("network bootstrap never deletes or invents status", async () => {
  const { api, calls } = harness(undefined, true, () => {
    throw Error(secret);
  });
  const r = await api.revoke(42, 18, old, () => true);
  assert.equal(r.outcome, "NOT_DISPATCHED");
  assert.equal(r.status, undefined);
  assert.equal(calls.length, 1);
});
test("cancelled before bootstrap sends nothing", async () => {
  const { api, calls } = harness();
  const controller = new AbortController();
  controller.abort();
  assert.equal(
    (await api.revoke(42, 18, old, () => true, controller.signal)).outcome,
    "NOT_DISPATCHED",
  );
  assert.equal(calls.length, 0);
});
test("production application graph never imports revoke candidate", async () => {
  const app = await build({
    entryPoints: ["src/hotel/main.tsx"],
    bundle: true,
    write: false,
    metafile: true,
    loader: { ".css": "empty" },
    define: { "import.meta.env": "{}" },
    jsx: "automatic",
  });
  assert.ok(
    !Object.keys(app.metafile.inputs).some((p) =>
      /RevokeUserRoleCandidate|revokeUserRoleApi/.test(p),
    ),
  );
});
