import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
const bundled = await build({
  entryPoints: ["src/hotel/departmentDeactivationApi.ts"],
  bundle: true,
  write: false,
  platform: "node",
  format: "esm",
});
const { createDepartmentDeactivationApi } = await import(
  "data:text/javascript;base64," +
    Buffer.from(bundled.outputFiles[0].text).toString("base64")
);
const tag = '"' + "A".repeat(64) + '"',
  next = '"' + "B".repeat(64) + '"',
  secret = "SYNTHETIC-DEPARTMENT-SECRET";
const department = () => ({
  id: 2,
  branchId: 1,
  code: "HK",
  name: "Housekeeping",
  description: null,
  managerUserId: null,
  isActive: false,
});
const response = (body, status = 200, headers = {}) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json", ...headers },
  });
const csrf = () => response({ headerName: "X-CSRF-TOKEN", csrfToken: secret });
function harness(
  reply = () => response(department(), 200, { ETag: next }),
  enabled = true,
  bootstrap = csrf,
) {
  const calls = [];
  return {
    calls,
    api: createDepartmentDeactivationApi(async (url, init) => {
      calls.push({ url, ...init });
      return url === "/api/auth/csrf" ? bootstrap() : reply();
    }, enabled),
  };
}
const change = (api, current = () => true, signal) =>
  api.change(1, 2, tag, "  Valid department reason  ", current, signal);
test("default OFF does not bootstrap or POST", async () => {
  let n = 0;
  const api = createDepartmentDeactivationApi(async () => {
    n++;
    return csrf();
  });
  assert.equal((await change(api)).reason, "DISABLED");
  assert.equal(n, 0);
});
test("synthetic confirmed request binds department/branch/ETag and exact payload", async () => {
  const { api, calls } = harness();
  assert.equal((await change(api)).outcome, "CONFIRMED");
  assert.deepEqual(
    calls.map((c) => [c.url, c.method]),
    [
      ["/api/auth/csrf", "GET"],
      ["/api/branches/1/departments/2/deactivate", "POST"],
    ],
  );
  assert.deepEqual(JSON.parse(calls[1].body), {
    reason: "Valid department reason",
    confirmation: true,
  });
  const h = new Headers(calls[1].headers);
  assert.equal(h.get("If-Match"), tag);
  assert.equal(h.get("X-CSRF-TOKEN"), secret);
  assert.equal(h.has("Authorization"), false);
  for (const c of calls) {
    assert.equal(c.credentials, "include");
    assert.equal(c.cache, "no-store");
  }
});
for (const status of [400, 401, 403, 404, 412, 428, 409, 500, 503])
  test("safe HTTP " + status, async () => {
    const { api, calls } = harness(() =>
      response({ detail: secret, traceId: secret }, status),
    );
    const r = await change(api);
    assert.equal(
      r.outcome,
      [409, 500, 503].includes(status) ? "OUTCOME_UNKNOWN" : "REJECTED",
    );
    assert.equal(r.status, status);
    assert.ok(!JSON.stringify(r).includes(secret));
    assert.equal(calls.length, 2);
  });
for (const status of [401, 403, 503])
  test("CSRF " + status + " never POSTs", async () => {
    const { api, calls } = harness(undefined, true, () => response({}, status));
    const r = await change(api);
    assert.equal(r.outcome, "NOT_DISPATCHED");
    assert.equal(r.status, status);
    assert.equal(calls.length, 1);
  });
for (const patch of [
  { id: 3 },
  { branchId: 2 },
  { isActive: true },
  { managerUserId: 0 },
])
  test("wrong/malformed success " + JSON.stringify(patch), async () => {
    const { api } = harness(() =>
      response({ ...department(), ...patch }, 200, { ETag: next }),
    );
    assert.equal((await change(api)).outcome, "OUTCOME_UNKNOWN");
  });
for (const etag of [tag, '"0011223344556677"', "*"])
  test("unchanged/wrong response ETag " + etag, async () => {
    const { api } = harness(() => response(department(), 200, { ETag: etag }));
    assert.equal((await change(api)).outcome, "OUTCOME_UNKNOWN");
  });
for (const reason of ["short", "x".repeat(1001), "invalid\nreason"])
  test("invalid reason " + reason.length, async () => {
    const { api, calls } = harness();
    assert.equal(
      (await api.change(1, 2, tag, reason, () => true)).reason,
      "INVALID_REQUEST",
    );
    assert.equal(calls.length, 0);
  });
test("synchronous double attempt BUSY without queue", async () => {
  let finish;
  const { api, calls } = harness(
    undefined,
    true,
    () => new Promise((r) => (finish = r)),
  );
  const first = change(api);
  assert.equal((await change(api)).reason, "BUSY");
  finish(csrf());
  assert.equal((await first).outcome, "CONFIRMED");
  assert.equal(calls.filter((c) => c.method === "POST").length, 1);
});
test("stale preflight never POSTs", async () => {
  let current = true;
  const { api, calls } = harness(undefined, true, () => {
    current = false;
    return csrf();
  });
  assert.equal((await change(api, () => current)).reason, "STALE");
  assert.equal(calls.length, 1);
});
test("late response body cannot confirm old identity", async () => {
  let current = true;
  const { api } = harness(() => {
    const r = response(department(), 200, { ETag: next });
    r.json = async () => {
      current = false;
      return department();
    };
    return r;
  });
  assert.equal((await change(api, () => current)).outcome, "OUTCOME_UNKNOWN");
});
test("network POST unknown without retry", async () => {
  const { api, calls } = harness(() => {
    throw Error(secret);
  });
  const r = await change(api);
  assert.equal(r.outcome, "OUTCOME_UNKNOWN");
  assert.ok(!JSON.stringify(r).includes(secret));
  assert.equal(calls.length, 2);
});
for (const code of [
  "DEPARTMENT_DEPENDENCIES_EXIST",
  "DEPARTMENT_STATE_CONFLICT",
])
  test("known conflict " + code, async () => {
    const { api } = harness(() =>
      response({ status: 409, code, title: code }, 409),
    );
    assert.equal((await change(api)).outcome, "REJECTED");
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
      /DepartmentDeactivationCandidate|departmentDeactivationApi/.test(p),
    ),
  );
});
