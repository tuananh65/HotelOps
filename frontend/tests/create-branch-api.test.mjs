import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
const b = await build({
  entryPoints: ["src/branch-maintenance/createBranchApi.ts"],
  bundle: true,
  write: false,
  format: "esm",
});
const { createBranchApi } = await import(
  "data:text/javascript;base64," +
    Buffer.from(b.outputFiles[0].text).toString("base64")
);
const tag = '"AAAAAAAAAAE="',
  secret = "SYNTHETIC-CREATE-CSRF";
const input = {
  code: " b-new ",
  name: "New branch",
  address: "New address",
  phone: null,
  email: null,
};
const branch = {
  id: 42,
  code: "B-NEW",
  name: "New branch",
  address: "New address",
  phone: null,
  email: null,
  timezone: "Asia/Ho_Chi_Minh",
  isActive: true,
};
function fixture(reply = { status: 201, body: branch, etag: tag }) {
  const calls = [];
  const create = createBranchApi(async (url, init) => {
    calls.push({ url, ...init });
    if (url === "/api/auth/csrf")
      return Response.json({ csrfToken: secret, headerName: "X-CSRF-TOKEN" });
    if (reply.error) throw reply.error;
    return new Response(reply.raw ?? JSON.stringify(reply.body), {
      status: reply.status ?? 201,
      headers: {
        "Content-Type": reply.type ?? "application/json",
        ...(reply.etag ? { ETag: reply.etag } : {}),
      },
    });
  });
  return { create, calls };
}
test("201 create uses normalized code, exact allowlist, fresh CSRF and cookie transport", async () => {
  const h = fixture();
  assert.deepEqual(
    await h.create(
      {
        ...input,
        id: 999,
        timezone: "BAD",
        isActive: false,
        reason: "NO",
        role: "NO",
      },
      () => true,
    ),
    { outcome: "CONFIRMED", data: { branch, etag: tag } },
  );
  assert.equal(h.calls.length, 2);
  assert.equal(h.calls[0].url, "/api/auth/csrf");
  const c = h.calls[1];
  assert.equal(c.url, "/api/branches");
  assert.equal(c.method, "POST");
  assert.equal(c.credentials, "include");
  assert.equal(c.cache, "no-store");
  assert.equal(c.redirect, "error");
  assert.equal(c.headers.get("X-CSRF-TOKEN"), secret);
  assert.equal(c.headers.get("If-Match"), null);
  assert.equal(c.headers.get("Authorization"), null);
  assert.deepEqual(JSON.parse(c.body), { ...input, code: "B-NEW" });
});
test("201 optional ETag omitted is valid, server fields preserved and extras stripped", async () => {
  const h = fixture({ status: 201, body: { ...branch, token: secret } });
  assert.deepEqual(await h.create(input, () => true), {
    outcome: "CONFIRMED",
    data: { branch, etag: null },
  });
});
for (const status of [400, 401, 403, 404, 409, 500, 503])
  test(`create HTTP ${status} sanitized, no second POST`, async () => {
    const h = fixture({
      status,
      body: { title: secret, traceId: secret, detail: secret },
    });
    const r = await h.create(input, () => true);
    assert.equal(r.outcome, status >= 500 ? "OUTCOME_UNKNOWN" : "REJECTED");
    assert.deepEqual(r.error, { kind: "http", status });
    assert.equal(h.calls.filter((c) => c.method === "POST").length, 1);
    assert.ok(!JSON.stringify(r).includes(secret));
  });
for (const reply of [
  { status: 200, body: branch },
  { body: { ...branch, id: 0 } },
  { body: { ...branch, id: Number.MAX_SAFE_INTEGER + 1 } },
  { body: { ...branch, code: "OTHER" } },
  { body: { ...branch, isActive: false } },
  { body: { ...branch, timezone: "OTHER" } },
  { body: { ...branch, address: null } },
  {
    body: branch,
    etag: '"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"',
  },
  { body: branch, etag: "W/" + tag },
  { body: null },
  { raw: "{" },
  { raw: "<html>", type: "text/html" },
])
  test("malformed/unexpected successful response never confirmed", async () => {
    const h = fixture(reply);
    assert.equal(
      (await h.create(input, () => true)).outcome,
      "OUTCOME_UNKNOWN",
    );
    assert.equal(h.calls.filter((c) => c.method === "POST").length, 1);
  });
for (const error of [
  new Error(secret),
  new DOMException(secret, "TimeoutError"),
  new DOMException(secret, "AbortError"),
])
  test(`post ${error.name} outcome unknown without retry`, async () => {
    const h = fixture({ error });
    assert.deepEqual(await h.create(input, () => true), {
      outcome: "OUTCOME_UNKNOWN",
      error: { kind: "network" },
    });
    assert.equal(h.calls.filter((c) => c.method === "POST").length, 1);
  });
for (const invalid of [
  { ...input, code: "" },
  { ...input, code: "bad space" },
  { ...input, code: "a".repeat(31) },
  { ...input, code: "ß" },
  { ...input, name: "" },
  { ...input, address: "" },
  { ...input, email: "bad" },
  { ...input, phone: "letters" },
])
  test("invalid fields do not dispatch", async () => {
    const h = fixture();
    assert.equal(
      (await h.create(invalid, () => true)).outcome,
      "NOT_DISPATCHED",
    );
    assert.equal(h.calls.length, 0);
  });
test("unknown identity and pre-aborted caller do not dispatch", async () => {
  const h = fixture();
  assert.equal((await h.create(input, () => false)).outcome, "NOT_DISPATCHED");
  const c = new AbortController();
  c.abort();
  assert.equal(
    (await h.create(input, () => true, c.signal)).outcome,
    "NOT_DISPATCHED",
  );
  assert.equal(h.calls.length, 0);
});
test("identity replacement during CSRF prevents POST; reviewed request detached", async () => {
  let resolve,
    active = true;
  const calls = [];
  const create = createBranchApi(async (url, init) => {
    calls.push({ url, ...init });
    return new Promise((r) => (resolve = r));
  });
  const p = create(input, () => active);
  active = false;
  resolve(Response.json({ csrfToken: secret, headerName: "X-CSRF-TOKEN" }));
  assert.equal((await p).outcome, "NOT_DISPATCHED");
  assert.equal(calls.length, 1);
});
test("identity replacement after POST does not publish confirmed identity data", async () => {
  let resolve,
    active = true;
  const create = createBranchApi(async (url) =>
    url === "/api/auth/csrf"
      ? Response.json({ csrfToken: secret, headerName: "X-CSRF-TOKEN" })
      : new Promise((r) => (resolve = r)),
  );
  const p = create(input, () => active);
  await new Promise((r) => setTimeout(r, 0));
  active = false;
  resolve(Response.json(branch, { status: 201, headers: { ETag: tag } }));
  assert.equal((await p).outcome, "OUTCOME_UNKNOWN");
});
test("exact reviewed body detached before awaiting bootstrap", async () => {
  const h = fixture();
  const submitted = { ...input };
  const p = h.create(submitted, () => true);
  submitted.name = "Changed later";
  await p;
  assert.equal(JSON.parse(h.calls[1].body).name, "New branch");
});
