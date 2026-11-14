import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
const bundle = await build({
  entryPoints: ["src/branch-maintenance/branchMaintenanceApi.ts"],
  bundle: true,
  write: false,
  format: "esm",
});
const { createBranchMaintenanceApi } = await import(
  "data:text/javascript;base64," +
    Buffer.from(bundle.outputFiles[0].text).toString("base64")
);
const tag =
    '"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"',
  next = '"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"',
  secret = "SYNTHETIC-BRANCH-PRIVATE";
const branch = (id = 1) => ({
  id,
  code: "B" + id,
  name: "Branch " + id,
  address: "Address",
  phone: null,
  email: null,
  timezone: "Asia/Ho_Chi_Minh",
  isActive: true,
});
const update = {
  name: "Updated",
  address: "New address",
  phone: null,
  email: null,
  reason: "Correct contact information",
};
function setup(reply = { status: 200, body: branch(), etag: next }) {
  const calls = [];
  const api = createBranchMaintenanceApi(async (url, init) => {
    calls.push({ url, ...init });
    if (url === "/api/auth/csrf")
      return Response.json({ csrfToken: secret, headerName: "X-CSRF-TOKEN" });
    if (reply.network) throw Error(secret);
    return new Response(reply.raw ?? JSON.stringify(reply.body), {
      status: reply.status ?? 200,
      headers: {
        "Content-Type": reply.type ?? "application/json",
        ...(reply.etag ? { ETag: reply.etag } : {}),
      },
    });
  });
  return { api, calls };
}
test("GET exact scoped route, credentials, no-store and detached snapshot", async () => {
  const h = setup({ body: { ...branch(), secret }, etag: tag });
  assert.deepEqual(await h.api.read(1), {
    ok: true,
    data: { branch: branch(), etag: tag },
  });
  const c = h.calls[0];
  assert.equal(c.url, "/api/branches/1/edit");
  assert.equal(c.method, "GET");
  assert.equal(c.credentials, "include");
  assert.equal(c.cache, "no-store");
  assert.equal(c.redirect, "error");
  assert.equal(c.headers.get("Authorization"), null);
});
test("update fresh CSRF plus exact PUT If-Match and request whitelist", async () => {
  const h = setup();
  const result = await h.api.update(
    1,
    tag,
    { ...update, isActive: false, code: "NO" },
    () => true,
  );
  assert.equal(result.outcome, "CONFIRMED");
  assert.equal(h.calls.length, 2);
  const c = h.calls[1];
  assert.equal(c.url, "/api/branches/1");
  assert.equal(c.method, "PUT");
  assert.equal(c.headers.get("If-Match"), tag);
  assert.equal(c.headers.get("X-CSRF-TOKEN"), secret);
  assert.equal(c.credentials, "include");
  assert.equal(c.cache, "no-store");
  assert.equal(c.redirect, "error");
  assert.equal(c.headers.get("Authorization"), null);
  assert.deepEqual(JSON.parse(c.body), update);
});
for (const status of [400, 401, 403, 404, 409, 412, 428, 500, 503])
  test(`HTTP ${status}: sanitized, one mutation, no replay`, async () => {
    const h = setup({
      status,
      body: { title: secret, detail: secret, traceId: secret },
    });
    const r = await h.api.update(1, tag, update, () => true);
    assert.equal(r.outcome, status >= 500 ? "OUTCOME_UNKNOWN" : "REJECTED");
    assert.deepEqual(r.error, { kind: "http", status });
    assert.equal(h.calls.filter((c) => c.method === "PUT").length, 1);
    assert.ok(!JSON.stringify(r).includes(secret));
  });
for (const reply of [
  { body: branch(2), etag: next },
  { body: branch(), etag: "W/" + next },
  { body: branch() },
  { raw: "{", etag: next },
  { body: branch(), type: "text/html", etag: next },
])
  test("invalid/missing bound success contract is unknown after PUT", async () => {
    const h = setup(reply);
    assert.equal(
      (await h.api.update(1, tag, update, () => true)).outcome,
      "OUTCOME_UNKNOWN",
    );
    assert.equal((await h.api.read(1)).ok, false);
  });
test("network loss after PUT is unknown with no retry", async () => {
  const h = setup({ network: true });
  assert.deepEqual(await h.api.update(1, tag, update, () => true), {
    outcome: "OUTCOME_UNKNOWN",
    error: { kind: "network" },
  });
  assert.equal(h.calls.filter((c) => c.method === "PUT").length, 1);
});
test("invalid IDs, weak tags, fields or unknown identity cannot dispatch", async () => {
  const h = setup();
  for (const args of [
    [0, tag, update, () => true],
    [1, "*", update, () => true],
    [1, tag, { ...update, reason: "short" }, () => true],
    [1, tag, { ...update, name: "" }, () => true],
    [1, tag, update, () => false],
  ])
    assert.equal((await h.api.update(...args)).outcome, "NOT_DISPATCHED");
  assert.equal(h.calls.length, 0);
});
test("identity changes during CSRF prevent PUT", async () => {
  let resolve;
  let current = true;
  const calls = [];
  const api = createBranchMaintenanceApi(async (url, init) => {
    calls.push(url);
    return new Promise((r) => (resolve = r));
  });
  const p = api.update(1, tag, update, () => current);
  current = false;
  resolve(Response.json({ csrfToken: secret, headerName: "X-CSRF-TOKEN" }));
  assert.equal((await p).outcome, "NOT_DISPATCHED");
  assert.deepEqual(calls, ["/api/auth/csrf"]);
});
test("request body frozen before bootstrap await", async () => {
  const h = setup();
  const input = { ...update };
  const p = h.api.update(1, tag, input, () => true);
  input.name = "tampered";
  await p;
  assert.equal(JSON.parse(h.calls[1].body).name, "Updated");
});
test("404 read remains unavailable error, never empty success", async () => {
  const h = setup({ status: 404, body: { detail: secret } });
  assert.deepEqual(await h.api.read(1), {
    ok: false,
    error: { kind: "http", status: 404 },
  });
});
test("active UC012 ETag contract rejects legacy base64 rowversion", async () => {
  const h = setup({ body: branch(), etag: '"AAAAAAAAAAE="' });
  assert.equal((await h.api.read(1)).ok, false);
  assert.equal(
    (await h.api.update(1, '"AAAAAAAAAAE="', update, () => true)).outcome,
    "NOT_DISPATCHED",
  );
  assert.equal(h.calls.filter((c) => c.method === "PUT").length, 0);
});
