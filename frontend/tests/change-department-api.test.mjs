import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
const b = await build({
  entryPoints: ["src/users/changeDepartmentApi.ts"],
  bundle: true,
  write: false,
  platform: "node",
  format: "esm",
});
const { createChangeDepartmentApi } = await import(
  "data:text/javascript;base64," +
    Buffer.from(b.outputFiles[0].text).toString("base64")
);
const old = '"0011223344556677"',
  next = '"0011223344556678"',
  secret = "PRIVATE-DEPARTMENT-TOKEN";
const review = { userId: 42, branchId: 1, departmentId: 2 };
const json = (body, status = 200, etag = next) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json", ETag: etag },
  });
const csrf = () => json({ csrfToken: secret, headerName: "X-CSRF-TOKEN" });
const success = () =>
  json({ userId: 42, branchId: 1, previousDepartmentId: 2, departmentId: 3 });
function harness(reply = success, bootstrap = csrf, enabled = true) {
  const calls = [];
  const api = createChangeDepartmentApi(async (url, options) => {
    calls.push({ url, ...options });
    return url === "/api/auth/csrf" ? bootstrap() : reply();
  }, enabled);
  return {
    api,
    calls,
    run: (current = () => true, signal) =>
      api.change(review, old, 3, current, signal),
  };
}
test("exact PUT contract, whitelisted detached success, cookie/CSRF and If-Match", async () => {
  const h = harness();
  const r = await h.run();
  assert.deepEqual(r, {
    ok: true,
    data: { userId: 42, branchId: 1, departmentId: 3 },
    etag: next,
  });
  assert.equal(h.calls.length, 2);
  const put = h.calls[1];
  assert.equal(put.method, "PUT");
  assert.equal(put.url, "/api/users/42/department");
  assert.deepEqual(JSON.parse(put.body), { departmentId: 3 });
  assert.equal(put.headers["If-Match"], old);
  assert.equal(put.headers["X-CSRF-TOKEN"], secret);
  for (const c of h.calls) {
    assert.equal(c.credentials, "include");
    assert.equal(c.cache, "no-store");
  }
  assert.equal(JSON.stringify(r).includes(secret), false);
});
test("default OFF has no requests", async () => {
  const h = harness(success, csrf, false);
  assert.equal((await h.run()).outcome, "NOT_DISPATCHED");
  assert.equal(h.calls.length, 0);
});
for (const status of [400, 401, 403, 404, 412, 428, 500, 503])
  test("mutation status " + status, async () => {
    const h = harness(() => json({ detail: secret }, status));
    const r = await h.run();
    assert.equal(r.outcome, status >= 500 ? "OUTCOME_UNKNOWN" : "REJECTED");
    assert.equal(r.status, status);
    assert.equal(h.calls.filter((c) => c.method === "PUT").length, 1);
    assert.equal(JSON.stringify(r).includes(secret), false);
  });
for (const code of [
  "ROLE_SCOPE_CONFLICT",
  "DEPARTMENT_UNCHANGED",
  "INVALID_DEPARTMENT_SCOPE",
  "OPEN_RESPONSIBILITIES",
  "LAST_DURABLE_ADMIN",
])
  test("verified conflict " + code, async () => {
    const h = harness(() =>
      json({ status: 409, code, title: code, detail: secret }, 409),
    );
    assert.equal((await h.run()).outcome, "REJECTED");
    assert.equal(h.calls.length, 2);
  });
test("contradictory conflict is unknown", async () => {
  const h = harness(() =>
    json(
      {
        status: 409,
        code: "ROLE_SCOPE_CONFLICT",
        title: "OPEN_RESPONSIBILITIES",
      },
      409,
    ),
  );
  assert.equal((await h.run()).outcome, "OUTCOME_UNKNOWN");
  assert.equal(h.calls.length, 2);
});
for (const status of [401, 403, 503])
  test("CSRF status " + status + " has no PUT", async () => {
    const h = harness(success, () => json({ detail: secret }, status));
    const r = await h.run();
    assert.equal(r.outcome, "NOT_DISPATCHED");
    assert.equal(r.status, status);
    assert.equal(h.calls.length, 1);
  });
for (const changed of [
  { branchId: 9 },
  { userId: 9 },
  { previousDepartmentId: 9 },
  { departmentId: 9 },
])
  test("mismatched success " + JSON.stringify(changed), async () => {
    const h = harness(() =>
      json({
        userId: 42,
        branchId: 1,
        previousDepartmentId: 2,
        departmentId: 3,
        ...changed,
      }),
    );
    assert.equal((await h.run()).outcome, "OUTCOME_UNKNOWN");
  });
for (const etag of [old, "W/" + next, "*", "bad"])
  test("invalid or unchanged success ETag " + etag, async () => {
    const h = harness(() =>
      json(
        { userId: 42, branchId: 1, previousDepartmentId: 2, departmentId: 3 },
        200,
        etag,
      ),
    );
    assert.equal((await h.run()).outcome, "OUTCOME_UNKNOWN");
  });
test("network after dispatch is unknown and not retried", async () => {
  const h = harness(() => {
    throw new Error(secret);
  });
  assert.equal((await h.run()).outcome, "OUTCOME_UNKNOWN");
  assert.equal(h.calls.length, 2);
});
test("malformed success is unknown", async () => {
  const h = harness(
    () =>
      new Response("{", {
        status: 200,
        headers: { "Content-Type": "application/json", ETag: next },
      }),
  );
  assert.equal((await h.run()).outcome, "OUTCOME_UNKNOWN");
});
test("review read pairs DTO and ETag from a single response", async () => {
  const h = harness(() => json({ ...review, raw: secret }, 200, old));
  assert.deepEqual(await h.api.read(42), { ok: true, data: review, etag: old });
  assert.equal(h.calls.length, 1);
  assert.equal(h.calls[0].method, "GET");
});
for (const value of [
  { ...review, userId: 9 },
  { ...review, branchId: 0 },
  { ...review, departmentId: "2" },
  null,
])
  test("reject malformed review " + JSON.stringify(value), async () => {
    const h = harness(() => json(value, 200, old));
    assert.equal((await h.api.read(42)).ok, false);
  });
test("identity changes during preflight prevent PUT", async () => {
  let current = true;
  const h = harness(success, () => {
    current = false;
    return csrf();
  });
  assert.equal((await h.run(() => current)).outcome, "NOT_DISPATCHED");
  assert.equal(h.calls.length, 1);
});
test("identity changes after dispatch cannot confirm", async () => {
  let current = true;
  const h = harness(() => {
    current = false;
    return success();
  });
  assert.equal((await h.run(() => current)).outcome, "OUTCOME_UNKNOWN");
});
test("simultaneous confirmations cannot dispatch two PUTs", async () => {
  let release;
  const waiting = new Promise((r) => (release = r));
  const h = harness(async () => {
    await waiting;
    return success();
  });
  const first = h.run();
  assert.equal((await h.run()).outcome, "NOT_DISPATCHED");
  release();
  assert.equal((await first).ok, true);
  assert.equal(h.calls.filter((c) => c.method === "PUT").length, 1);
});
test("freeze target and branch through asynchronous bootstrap", async () => {
  const input = { ...review };
  let done;
  const pending = new Promise((r) => (done = r));
  const h = harness(success, async () => {
    await pending;
    return csrf();
  });
  const r = h.api.change(input, old, 3, () => true);
  input.userId = 99;
  input.branchId = 99;
  input.departmentId = 99;
  done();
  assert.equal((await r).ok, true);
  assert.equal(h.calls[1].url, "/api/users/42/department");
});
test("pre-cancelled operation dispatches nothing", async () => {
  const h = harness();
  const c = new AbortController();
  c.abort();
  assert.equal((await h.run(() => true, c.signal)).outcome, "NOT_DISPATCHED");
  assert.equal(h.calls.length, 0);
});

for (const status of [401, 403, 404, 503])
  test(
    "read error " + status + " never masquerades as empty or success",
    async () => {
      const h = harness(() => json({ detail: secret }, status, old));
      const r = await h.api.read(42);
      assert.equal(r.ok, false);
      assert.equal(r.status, status);
      assert.equal(JSON.stringify(r).includes(secret), false);
    },
  );
for (const etag of ["*", "W/" + old, "bad"])
  test("read rejects unsafe version " + etag, async () => {
    const h = harness(() => json(review, 200, etag));
    assert.equal((await h.api.read(42)).ok, false);
  });
test("CSRF network failure has no invented HTTP status or PUT", async () => {
  const h = harness(success, () => {
    throw new Error(secret);
  });
  const r = await h.run();
  assert.deepEqual(r, { ok: false, outcome: "NOT_DISPATCHED" });
  assert.equal(h.calls.length, 1);
});

for (const status of [401, 403, 503])
  test("unreadable CSRF error retains observed status " + status, async () => {
    const h = harness(success, () => {
      const response = json({ detail: secret }, status);
      response.text = async () => {
        throw new Error(secret);
      };
      return response;
    });
    assert.deepEqual(await h.run(), {
      ok: false,
      outcome: "NOT_DISPATCHED",
      status,
    });
    assert.equal(h.calls.length, 1);
  });

test("unreadable successful CSRF has no failure HTTP status", async () => {
  const h = harness(success, () => {
    const response = csrf();
    response.text = async () => {
      throw new Error(secret);
    };
    return response;
  });
  assert.deepEqual(await h.run(), { ok: false, outcome: "NOT_DISPATCHED" });
  assert.equal(h.calls.length, 1);
});
