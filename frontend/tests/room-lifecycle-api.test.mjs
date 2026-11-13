import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
const bundle = await build({
  entryPoints: ["src/hotel/roomLifecycleApi.ts"],
  bundle: true,
  write: false,
  platform: "node",
  format: "esm",
});
const { createRoomLifecycleApi } = await import(
  "data:text/javascript;base64," +
    Buffer.from(bundle.outputFiles[0].text).toString("base64")
);
const tag = '"' + "A".repeat(64) + '"',
  next = '"' + "B".repeat(64) + '"',
  secret = "SYNTHETIC-ROOM-SECRET";
const room = (active) => ({
  id: 10,
  branchId: 1,
  floorId: 2,
  areaId: null,
  roomTypeId: 3,
  roomNumber: "101",
  notes: null,
  operationalStatus: active ? "DIRTY" : "OUT_OF_SERVICE",
  isActive: active,
});
const response = (body, status = 200, headers = {}) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json", ...headers },
  });
const csrf = () => response({ headerName: "X-CSRF-TOKEN", csrfToken: secret });
function harness(
  reply = () => response(room(false), 200, { ETag: next }),
  enabled = true,
  bootstrap = csrf,
) {
  const calls = [];
  return {
    calls,
    api: createRoomLifecycleApi(async (url, init) => {
      calls.push({ url, ...init });
      return url === "/api/auth/csrf" ? bootstrap() : reply();
    }, enabled),
  };
}
const change = (api, activate = false, current = () => true) =>
  api.change(1, 10, activate, tag, "Valid room reason", current);
test("default OFF never requests", async () => {
  let n = 0;
  const api = createRoomLifecycleApi(async () => {
    n++;
    return csrf();
  });
  assert.equal((await change(api)).reason, "DISABLED");
  assert.equal(n, 0);
});
for (const active of [false, true])
  test("synthetic confirmed scoped " + active, async () => {
    const { api, calls } = harness(() =>
      response(room(active), 200, { ETag: next }),
    );
    assert.equal((await change(api, active)).outcome, "CONFIRMED");
    assert.equal(
      calls[1].url,
      `/api/rooms/10/${active ? "reactivate" : "deactivate"}?branchId=1`,
    );
    assert.equal(calls[1].method, "POST");
    assert.deepEqual(JSON.parse(calls[1].body), {
      reason: "Valid room reason",
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
  test("HTTP " + status, async () => {
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
  test("CSRF " + status, async () => {
    const { api, calls } = harness(undefined, true, () => response({}, status));
    const r = await change(api);
    assert.equal(r.outcome, "NOT_DISPATCHED");
    assert.equal(r.status, status);
    assert.equal(calls.length, 1);
  });
for (const patch of [
  { id: 11 },
  { branchId: 2 },
  { isActive: true },
  { floorId: 0 },
])
  test("wrong/malformed success " + JSON.stringify(patch), async () => {
    const { api } = harness(() =>
      response({ ...room(false), ...patch }, 200, { ETag: next }),
    );
    assert.equal((await change(api)).outcome, "OUTCOME_UNKNOWN");
  });
for (const bad of ['"0011223344556677"', "*", "W/" + tag])
  test("reject non-Room ETag " + bad, async () => {
    const { api, calls } = harness();
    assert.equal(
      (await api.change(1, 10, false, bad, "Valid room reason", () => true))
        .reason,
      "INVALID_REQUEST",
    );
    assert.equal(calls.length, 0);
  });
for (const reason of ["short", "x".repeat(1001), "invalid\nreason"])
  test("invalid reason " + reason.length, async () => {
    const { api, calls } = harness();
    assert.equal(
      (await api.change(1, 10, false, tag, reason, () => true)).reason,
      "INVALID_REQUEST",
    );
    assert.equal(calls.length, 0);
  });
test("concurrent operations BUSY no queue", async () => {
  let finish;
  const { api, calls } = harness(
    undefined,
    true,
    () => new Promise((r) => (finish = r)),
  );
  const first = change(api);
  assert.equal((await change(api, true)).reason, "BUSY");
  finish(csrf());
  assert.equal((await first).outcome, "CONFIRMED");
  assert.equal(calls.filter((c) => c.method === "POST").length, 1);
});
test("network outcome unknown no retry", async () => {
  const { api, calls } = harness(() => {
    throw Error(secret);
  });
  const r = await change(api);
  assert.equal(r.outcome, "OUTCOME_UNKNOWN");
  assert.ok(!JSON.stringify(r).includes(secret));
  assert.equal(calls.length, 2);
});
test("identity changes during body read cannot confirm", async () => {
  let current = true;
  const { api } = harness(() => {
    const r = response(room(false), 200, { ETag: next });
    r.json = async () => {
      current = false;
      return room(false);
    };
    return r;
  });
  assert.equal(
    (await change(api, false, () => current)).outcome,
    "OUTCOME_UNKNOWN",
  );
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
      /RoomLifecycleCandidate|roomLifecycleApi/.test(p),
    ),
  );
});
for (const code of [
  "ROOM_DEPENDENCIES_EXIST",
  "ROOM_PARENT_INELIGIBLE",
  "ROOM_STATE_CONFLICT",
])
  test("known conflict " + code, async () => {
    const { api } = harness(() =>
      response({ status: 409, code, title: code, detail: secret }, 409),
    );
    assert.equal((await change(api)).outcome, "REJECTED");
  });
test("stale identity after bootstrap never posts", async () => {
  let current = true;
  const { api, calls } = harness(undefined, true, () => {
    current = false;
    return csrf();
  });
  assert.equal((await change(api, false, () => current)).reason, "STALE");
  assert.equal(calls.length, 1);
});
test("reactivation cannot confirm a Ready response", async () => {
  const { api } = harness(() =>
    response({ ...room(true), operationalStatus: "READY" }, 200, {
      ETag: next,
    }),
  );
  assert.equal((await change(api, true)).outcome, "OUTCOME_UNKNOWN");
});

for (const status of [401, 403, 503])
  test(
    "phaseA unreadable CSRF " + status + " preserves admission failure",
    async () => {
      const h = harness(undefined, true, () => {
        const r = response({ detail: secret }, status);
        r.text = async () => {
          throw Error(secret);
        };
        return r;
      });
      const result = await change(h.api);
      assert.equal(result.outcome, "NOT_DISPATCHED");
      assert.equal(result.status, status);
      assert.equal(h.calls.length, 1);
      assert.ok(!JSON.stringify(result).includes(secret));
    },
  );

for (const profile of ["network", "malformed", "unreadable-success"])
  test("phaseA " + profile + " supplies no invented status", async () => {
    const h = harness(undefined, true, () => {
      if (profile === "network") throw Error(secret);
      const r = response({});
      if (profile === "unreadable-success")
        r.text = async () => {
          throw Error(secret);
        };
      return r;
    });
    const r = await change(h.api);
    assert.equal(r.outcome, "NOT_DISPATCHED");
    assert.equal(r.status, undefined);
    assert.equal(h.calls.length, 1);
  });
test("phaseA failed bootstrap cannot poison next request", async () => {
  let i = 0;
  const h = harness(undefined, true, () => {
    if (i++) throw Error(secret);
    const r = response({}, 401);
    r.text = async () => {
      throw Error(secret);
    };
    return r;
  });
  assert.equal((await change(h.api)).status, 401);
  assert.equal((await change(h.api)).status, undefined);
  assert.equal(h.calls.length, 2);
});
