import { test } from "node:test";
import assert from "node:assert/strict";
import { register } from "node:module";
register(new URL("./dev2-ts-loader.mjs", import.meta.url));
const { createDev2Api } = await import("../src/dev2/api.ts");
const { operations } = await import("../src/dev2/catalog.generated.ts");
const operation = operations.find(action => action.id === "A004A-021");
const values = { branchId: 3, incidentTitle: "Leaking tap", description: "Leak detected", roomId: 8,
  discoveredAt: "2026-11-16T01:00:00Z", internalRequestId: 17,
  requestLink: { version: "AAAAAAAAAAE=", mandatory: false }, similarReviewToken: "TEST-REVIEW" };
const response = (body, status = 200) => new Response(JSON.stringify(body), {
  status, headers: { "Content-Type": "application/json" },
});
const session = () => response({ user: { id: "42", username: "staff", name: "Staff" },
  expiresAt: "2099-11-16T20:00:00Z", absoluteExpiresAt: "2099-11-17T20:00:00Z",
  mustChangePassword: false, csrfToken: "TEST-CSRF" });

test("linked incident preserves source version and explicit optional choice with session and CSRF", async () => {
  let posted;
  const api = createDev2Api(async (url, init) => {
    if (url === "/api/auth/session") return session();
    assert.equal(url, "/api/incidents");
    assert.equal(new Headers(init.headers).get("X-CSRF-TOKEN"), "TEST-CSRF");
    posted = JSON.parse(init.body);
    return response({ incidentId: 19 }, 201);
  });
  assert.equal((await api(operation, {}, values, "42")).ok, true);
  assert.equal(posted.reportedBy, 42);
  assert.equal(posted.internalRequestId, 17);
  assert.equal(posted.similarReviewToken, "TEST-REVIEW");
  assert.deepEqual(posted.requestLink, values.requestLink);
});

test("incomplete linked source never falls back to standalone incident creation", async () => {
  for (const invalid of [
    { ...values, internalRequestId: undefined },
    { ...values, similarReviewToken: undefined },
    { ...values, internalRequestId: 0 },
    { ...values, requestLink: undefined },
    { ...values, requestLink: { version: "invalid", mandatory: false } },
    { ...values, requestLink: { version: values.requestLink.version } },
  ]) {
    let calls = 0;
    const result = await createDev2Api(async () => { calls++; return session(); })(operation, {}, invalid, "42");
    assert.equal(result.ok, false);
    assert.equal(calls, 0);
  }
});

test("stale linked creation is not retried without its source binding", async () => {
  let writes = 0;
  const result = await createDev2Api(async url => {
    if (url === "/api/auth/session") return session();
    writes++;
    return response({ status: 412, title: "IR_VERSION_CHANGED" }, 412);
  })(operation, {}, values, "42");
  assert.equal(result.ok, false);
  assert.equal(writes, 1);
});
