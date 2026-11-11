import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { mkdir } from "node:fs/promises";
await mkdir(".test-output/reset", { recursive: true });
await build({
  entryPoints: ["src/users/credentialResetApi.ts"],
  outfile: ".test-output/reset/api.mjs",
  bundle: true,
  platform: "node",
  format: "esm",
});
const { createCredentialResetApi } =
  await import("../.test-output/reset/api.mjs");
const tag = '"0123456789ABCDEF"',
  next = '"0123456789ABCDE0"',
  token = "SYNTHETIC-CSRF",
  secret = "SYNTHETIC-ONE-TIME-PASSWORD";
const expiry = "2030-01-01T00:00:00.1234567Z";
const response = (body, status = 200, headers = {}) =>
  Response.json(body, { status, headers });
function harness(
  reply = () =>
    response(
      { temporaryCredential: { password: secret, expiresAtUtc: expiry } },
      200,
      { ETag: next },
    ),
  preflight = () => response({ headerName: "X-CSRF-TOKEN", csrfToken: token }),
) {
  const calls = [];
  const api = createCredentialResetApi(async (url, options) => {
    calls.push({ url, ...options });
    return url === "/api/auth/csrf" ? preflight() : reply();
  });
  return {
    api,
    calls,
    reset: () =>
      api.reset(42, tag, true, () => true, new AbortController().signal),
  };
}
function safe(result) {
  const json = JSON.stringify(result);
  for (const bad of [token, "PRIVATE-DETAIL", "PRIVATE-TRACE"])
    assert.ok(!json.includes(bad));
}
test("reset sends only identity attestation, current version, cookies and per-attempt CSRF", async () => {
  const h = harness(),
    r = await h.reset();
  assert.equal(r.outcome, "CONFIRMED");
  assert.equal(r.etag, next);
  assert.equal(r.data.password, secret);
  assert.equal(h.calls.length, 2);
  const c = h.calls[1];
  assert.equal(c.url, "/api/users/42/reset-credential");
  assert.equal(c.method, "POST");
  assert.deepEqual(JSON.parse(c.body), { identityVerificationConfirmed: true });
  assert.equal(c.headers.get("If-Match"), tag);
  assert.equal(c.headers.get("X-CSRF-TOKEN"), token);
  for (const c of h.calls) {
    assert.equal(c.credentials, "include");
    assert.equal(c.cache, "no-store");
    assert.equal(c.redirect, "error");
  }
  safe(r);
});
for (const status of [400, 401, 403, 404, 412, 413, 415, 423, 428])
  test("reset rejection sanitized " + status, async () => {
    const h = harness(() =>
      response(
        { status, title: "PRIVATE-DETAIL", traceId: "PRIVATE-TRACE" },
        status,
      ),
    );
    const r = await h.reset();
    assert.equal(r.outcome, "REJECTED");
    assert.equal(r.status, status);
    safe(r);
    assert.equal(h.calls.length, 2);
  });
for (const status of [500, 503])
  test("unknown server outcome never replayed " + status, async () => {
    const h = harness(() => response({ detail: "PRIVATE-DETAIL" }, status));
    const r = await h.reset();
    assert.equal(r.outcome, "OUTCOME_UNKNOWN");
    assert.equal(r.status, status);
    safe(r);
    assert.equal(h.calls.length, 2);
  });
for (const status of [401, 403, 503])
  test("CSRF failure never dispatches reset " + status, async () => {
    const h = harness(undefined, () =>
      response({ detail: "PRIVATE-DETAIL" }, status),
    );
    const r = await h.reset();
    assert.equal(r.outcome, "NOT_DISPATCHED");
    assert.equal(r.status, status);
    assert.equal(h.calls.length, 1);
    safe(r);
  });
for (const code of ["TARGET_NOT_ACTIVE", "LAST_DURABLE_ADMIN"])
  test("verified conflict " + code, async () => {
    const h = harness(() => response({ status: 409, code, title: code }, 409));
    assert.equal((await h.reset()).outcome, "REJECTED");
  });
for (const body of [
  { code: "TARGET_NOT_ACTIVE", title: "LAST_DURABLE_ADMIN" },
  { code: null, title: "TARGET_NOT_ACTIVE" },
  { code: "UNKNOWN" },
  { status: 400, title: "TARGET_NOT_ACTIVE" },
])
  test("ambiguous conflict " + JSON.stringify(body), async () => {
    const h = harness(() => response(body, 409));
    const r = await h.reset();
    assert.equal(r.outcome, "OUTCOME_UNKNOWN");
    safe(r);
    assert.equal(h.calls.length, 2);
  });
for (const headers of [
  {},
  { ETag: tag },
  { ETag: "W/" + next },
  { ETag: '"abcdefghabcdefgh"' },
])
  test(
    "malformed/unchanged reset version " + JSON.stringify(headers),
    async () => {
      const h = harness(() =>
        response(
          { temporaryCredential: { password: secret, expiresAtUtc: expiry } },
          200,
          headers,
        ),
      );
      const r = await h.reset();
      assert.equal(r.outcome, "OUTCOME_UNKNOWN");
      assert.ok(!JSON.stringify(r).includes(secret));
    },
  );
for (const credential of [
  { password: secret, expiresAtUtc: "2030-02-30T00:00:00Z" },
  { password: "", expiresAtUtc: expiry },
  { password: secret, expiresAtUtc: "bad" },
  null,
])
  test("malformed secret handoff " + JSON.stringify(credential), async () => {
    const h = harness(() =>
      response({ temporaryCredential: credential }, 200, { ETag: next }),
    );
    const r = await h.reset();
    assert.equal(r.outcome, "OUTCOME_UNKNOWN");
    assert.ok(!JSON.stringify(r).includes(secret));
  });
test("synchronous exclusion covers proof and reset and never queues", async () => {
  let finish;
  const h = harness(
    undefined,
    () =>
      new Promise(
        (r) =>
          (finish = () =>
            r(response({ headerName: "X-CSRF-TOKEN", csrfToken: token }))),
      ),
  );
  const a = h.reset();
  assert.equal((await h.reset()).outcome, "NOT_DISPATCHED");
  assert.equal(
    (
      await h.api.reauthenticate(
        "password",
        () => true,
        new AbortController().signal,
      )
    ).outcome,
    "NOT_DISPATCHED",
  );
  finish();
  assert.equal((await a).outcome, "CONFIRMED");
  assert.equal(h.calls.length, 2);
});
test("binding replacement during CSRF prevents POST", async () => {
  let current = true;
  const h = harness(undefined, () => {
    current = false;
    return response({ headerName: "X-CSRF-TOKEN", csrfToken: token });
  });
  const r = await h.api.reset(
    42,
    tag,
    true,
    () => current,
    new AbortController().signal,
  );
  assert.equal(r.outcome, "NOT_DISPATCHED");
  assert.equal(h.calls.length, 1);
});
test("network before and after dispatch are distinct without fabricated status", async () => {
  for (const bootstrap of [true, false]) {
    const fail = () => {
      throw Error("PRIVATE-DETAIL");
    };
    const h = bootstrap ? harness(undefined, fail) : harness(fail);
    const r = await h.reset();
    assert.equal(r.outcome, bootstrap ? "NOT_DISPATCHED" : "OUTCOME_UNKNOWN");
    assert.equal(r.status, undefined);
    safe(r);
    assert.equal(h.calls.length, bootstrap ? 1 : 2);
  }
});
test("invalid selectors, missing attestation and cancellation cause no calls", async () => {
  const h = harness();
  for (const args of [
    [0, tag, true],
    [42, "W/" + tag, true],
    [42, tag, false],
    [Number.MAX_SAFE_INTEGER + 1, tag, true],
  ])
    assert.equal(
      (await h.api.reset(...args, () => true, new AbortController().signal))
        .outcome,
      "NOT_DISPATCHED",
    );
  const a = new AbortController();
  a.abort();
  assert.equal(
    (await h.api.reset(42, tag, true, () => true, a.signal)).outcome,
    "NOT_DISPATCHED",
  );
  assert.equal(h.calls.length, 0);
});
test("reauthentication uses only currentPassword; no client proof token", async () => {
  const h = harness(() =>
    response({ expiresAtUtc: expiry, proofToken: "PRIVATE-DETAIL" }),
  );
  const r = await h.api.reauthenticate(
    " password ",
    () => true,
    new AbortController().signal,
  );
  assert.equal(r.outcome, "CONFIRMED");
  assert.deepEqual(r.data, { expiresAtUtc: expiry });
  assert.deepEqual(JSON.parse(h.calls[1].body), {
    currentPassword: " password ",
  });
  assert.equal(h.calls[1].url, "/api/auth/reauthenticate");
  safe(r);
});

test("verified reauthentication INVALID_CREDENTIALS remains operation-level, not a session assertion", async () => {
  const h = harness(() =>
    response(
      {
        status: 401,
        code: "INVALID_CREDENTIALS",
        title: "INVALID_CREDENTIALS",
        detail: "PRIVATE-DETAIL",
      },
      401,
    ),
  );
  const r = await h.api.reauthenticate(
    "wrong",
    () => true,
    new AbortController().signal,
  );
  assert.equal(r.outcome, "REJECTED");
  assert.equal(r.code, "INVALID_CREDENTIALS");
  safe(r);
});
