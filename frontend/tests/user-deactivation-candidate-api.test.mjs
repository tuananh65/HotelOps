import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
const bundle = await build({
  entryPoints: ["src/users/userDeactivationCandidateApi.ts"],
  bundle: true,
  write: false,
  platform: "node",
  format: "esm",
});
const { createUserDeactivationCandidateApi } = await import(
  "data:text/javascript;base64," +
    Buffer.from(bundle.outputFiles[0].text).toString("base64")
);
const tag = '"0011223344556677"',
  next = '"0011223344556678"',
  secret = "SYNTHETIC-DEACTIVATION-SECRET";
const response = (body, status = 200, headers = {}) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json", ...headers },
  });
const csrf = () => response({ headerName: "X-CSRF-TOKEN", csrfToken: secret });
const success = () =>
  response({ userId: 42, accountStatus: "INACTIVE" }, 200, { ETag: next });
function harness(reply = success, enabled = true, bootstrap = csrf) {
  const calls = [];
  return {
    calls,
    api: createUserDeactivationCandidateApi(async (url, init) => {
      calls.push({ url, ...init });
      return url === "/api/auth/csrf" ? bootstrap() : reply();
    }, enabled),
  };
}
const send = (api, current = () => true, signal) =>
  api.deactivate(42, tag, "  Valid departure reason  ", current, signal);
test("default OFF no request", async () => {
  let n = 0;
  const api = createUserDeactivationCandidateApi(async () => {
    n++;
    return csrf();
  });
  assert.equal((await send(api)).reason, "DISABLED");
  assert.equal(n, 0);
});
test("confirmed synthetic success binds target status fresh ETag and exact request", async () => {
  const { api, calls } = harness();
  assert.deepEqual(await send(api), {
    outcome: "CONFIRMED",
    userId: 42,
    accountStatus: "INACTIVE",
    etag: next,
  });
  assert.deepEqual(
    calls.map((c) => [c.url, c.method]),
    [
      ["/api/auth/csrf", "GET"],
      ["/api/users/42/deactivate", "PATCH"],
    ],
  );
  assert.deepEqual(JSON.parse(calls[1].body), {
    reason: "Valid departure reason",
  });
  const h = new Headers(calls[1].headers);
  assert.equal(h.get("If-Match"), tag);
  assert.equal(h.get("X-CSRF-TOKEN"), secret);
  assert.ok(!h.has("Authorization"));
  for (const c of calls) {
    assert.equal(c.credentials, "include");
    assert.equal(c.cache, "no-store");
  }
});
for (const status of [400, 401, 403, 404, 412, 413, 415, 428, 500, 503])
  test("HTTP " + status, async () => {
    const { api, calls } = harness(() =>
      response({ detail: secret, traceId: secret }, status),
    );
    const r = await send(api);
    assert.equal(r.outcome, status >= 500 ? "OUTCOME_UNKNOWN" : "REJECTED");
    assert.equal(r.status, status);
    assert.ok(!JSON.stringify(r).includes(secret));
    assert.equal(calls.length, 2);
  });
for (const reason of ["short", "          ", "x".repeat(17000)])
  test("invalid/oversize reason " + reason.length, async () => {
    const { api, calls } = harness();
    assert.equal(
      (await api.deactivate(42, tag, reason, () => true)).reason,
      "INVALID_REQUEST",
    );
    assert.equal(calls.length, 0);
  });
for (const body of [
  { userId: 43, accountStatus: "INACTIVE" },
  { userId: 42, accountStatus: "ACTIVE" },
  {},
])
  test("malformed/wrong success " + JSON.stringify(body), async () => {
    const { api } = harness(() => response(body, 200, { ETag: next }));
    assert.equal((await send(api)).outcome, "OUTCOME_UNKNOWN");
  });
for (const etag of [tag, "W/" + next, "*"])
  test("invalid/unchanged ETag " + etag, async () => {
    const { api } = harness(() =>
      response({ userId: 42, accountStatus: "INACTIVE" }, 200, { ETag: etag }),
    );
    assert.equal((await send(api)).outcome, "OUTCOME_UNKNOWN");
  });
for (const status of [401, 403, 503])
  test("CSRF status " + status, async () => {
    const { api, calls } = harness(undefined, true, () => response({}, status));
    const r = await send(api);
    assert.equal(r.outcome, "NOT_DISPATCHED");
    assert.equal(r.status, status);
    assert.equal(calls.length, 1);
  });
test("network mutation unknown no retry", async () => {
  const { api, calls } = harness(() => {
    throw Error(secret);
  });
  const r = await send(api);
  assert.equal(r.outcome, "OUTCOME_UNKNOWN");
  assert.ok(!JSON.stringify(r).includes(secret));
  assert.equal(calls.length, 2);
});
test("overlap BUSY never queues", async () => {
  let finish;
  const { api, calls } = harness(
    undefined,
    true,
    () => new Promise((r) => (finish = r)),
  );
  const first = send(api);
  assert.equal((await send(api)).reason, "BUSY");
  finish(csrf());
  assert.equal((await first).outcome, "CONFIRMED");
  assert.equal(calls.filter((c) => c.method === "PATCH").length, 1);
});
test("identity changes during response body read are unknown", async () => {
  let current = true;
  const { api } = harness(() => {
    const r = success();
    r.json = async () => {
      current = false;
      return { userId: 42, accountStatus: "INACTIVE" };
    };
    return r;
  });
  assert.equal((await send(api, () => current)).outcome, "OUTCOME_UNKNOWN");
});
test("production graph excludes candidate", async () => {
  const app = await build({
    entryPoints: ["src/hotel/main.tsx"],
    bundle: true,
    write: false,
    metafile: true,
    jsx: "automatic",
    loader: { ".css": "empty" },
    define: { "import.meta.env": "{}" },
  });
  assert.ok(
    !Object.keys(app.metafile.inputs).some((p) =>
      /UserDeactivationCandidate|userDeactivationCandidateApi/.test(p),
    ),
  );
});
