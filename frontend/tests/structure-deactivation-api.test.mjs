import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
const output = await build({
  stdin: {
    contents:
      "export * from './src/structure-deactivation/structureDeactivationApi';export * from './src/structure-deactivation/contracts';",
    resolveDir: process.cwd(),
    loader: "ts",
  },
  bundle: true,
  write: false,
  format: "esm",
});
const { createStructureDeactivationApi, validReason } = await import(
  "data:text/javascript;base64," +
    Buffer.from(output.outputFiles[0].text).toString("base64")
);
export const tag = '"' + "A".repeat(64) + '"';
export const record = (kind = "floor") => ({
  kind,
  id: 11,
  branchId: 42,
  code: "F11",
  name: "Verified record",
  description: null,
  floorId: null,
  floorNumber: kind === "floor" ? 2 : null,
  isActive: true,
});
const input = { reason: "Retire unused location", confirmation: true };
const make = (body, status = 200, etag = tag) =>
  Response.json(body, { status, headers: { ETag: etag } });
for (const kind of ["floor", "area"])
  test("GET exact binding " + kind, async () => {
    const calls = [];
    const api = createStructureDeactivationApi(async (u, o) => {
      calls.push([u, o]);
      return make(record(kind));
    });
    const r = await api.read(kind, 42, 11);
    assert.equal(r.ok, true);
    assert.equal(calls[0][0], `/api/branches/42/${kind}s/11`);
    assert.equal(calls[0][1].method, "GET");
    assert.equal(calls[0][1].credentials, "include");
    assert.equal(calls[0][1].cache, "no-store");
  });
test("default gate prevents CSRF and POST", async () => {
  let n = 0;
  const r = await createStructureDeactivationApi(async () => {
    n++;
    throw Error();
  }).deactivate("floor", 42, 11, tag, input, () => true);
  assert.equal(r.outcome, "NOT_DISPATCHED");
  assert.equal(r.error.reason, "DISABLED");
  assert.equal(n, 0);
});
for (const [label, body, etag] of [
  ["wrong branch", { ...record(), branchId: 43 }, tag],
  ["wrong target", { ...record(), id: 12 }, tag],
  ["wrong kind", record("area"), tag],
  ["bad active", { ...record(), isActive: "yes" }, tag],
  ["weak tag", record(), "W/" + tag],
  ["base64 tag", record(), '"AQIDBA=="'],
])
  test("reject " + label, async () => {
    const r = await createStructureDeactivationApi(async () =>
      make(body, 200, etag),
    ).read("floor", 42, 11);
    assert.equal(r.ok, false);
  });
for (const [status, code] of [
  [400, "VALIDATION_FAILED"],
  [401, "SESSION_INVALID"],
  [403, "PERMISSION_DENIED"],
  [409, "STRUCTURE_DEPENDENCIES_EXIST"],
  [412, "STALE_STRUCTURE_VERSION"],
  [503, "STRUCTURE_WRITER_COORDINATION_REQUIRED"],
  [500, "SECRET"],
])
  test("POST response " + status, async () => {
    const calls = [];
    const api = createStructureDeactivationApi(async (u, o) => {
      calls.push([u, o]);
      return u.endsWith("/csrf")
        ? make({ headerName: "X-CSRF-TOKEN", csrfToken: "synthetic-token" })
        : make(
            {
              status,
              code,
              title: code,
              detail: "SECRET",
              traceId: "SECRET",
              dependencies: [{ kind: "ACTIVE_ROOM", count: 2, ids: [1, 2] }],
            },
            status,
          );
    }, true);
    const r = await api.deactivate(
      "area",
      42,
      11,
      tag,
      { ...input, extra: "must-not-send" },
      () => true,
    );
    assert.equal(r.outcome, status >= 500 ? "OUTCOME_UNKNOWN" : "REJECTED");
    assert.equal(calls.length, 2);
    const [u, o] = calls[1];
    assert.equal(u, "/api/branches/42/areas/11/deactivate");
    assert.equal(o.method, "POST");
    assert.equal(o.credentials, "include");
    assert.equal(new Headers(o.headers).get("If-Match"), tag);
    assert.equal(new Headers(o.headers).get("X-CSRF-TOKEN"), "synthetic-token");
    assert.deepEqual(JSON.parse(o.body), input);
    assert.ok(!JSON.stringify(r).includes("SECRET"));
    if (status === 409)
      assert.deepEqual(r.error.dependencies, [
        { kind: "ACTIVE_ROOM", count: 2 },
      ]);
  });
test("single POST network uncertainty", async () => {
  let posts = 0;
  const api = createStructureDeactivationApi(async (u) => {
    if (u.endsWith("/csrf"))
      return make({ headerName: "X-CSRF-TOKEN", csrfToken: "token" });
    posts++;
    throw Error("SECRET");
  }, true);
  const r = await api.deactivate("floor", 42, 11, tag, input, () => true);
  assert.equal(r.outcome, "OUTCOME_UNKNOWN");
  assert.equal(posts, 1);
  assert.ok(!JSON.stringify(r).includes("SECRET"));
});
for (const good of [false, true])
  test("synthetic 200 requires inactive DTO " + good, async () => {
    const api = createStructureDeactivationApi(
      async (u) =>
        u.endsWith("/csrf")
          ? make({ headerName: "X-CSRF-TOKEN", csrfToken: "token" })
          : make({ ...record(), isActive: !good }),
      true,
    );
    assert.equal(
      (await api.deactivate("floor", 42, 11, tag, input, () => true)).outcome,
      good ? "CONFIRMED" : "OUTCOME_UNKNOWN",
    );
  });
test("identity changes during bootstrap prevent dispatch", async () => {
  let valid = true,
    n = 0;
  const api = createStructureDeactivationApi(async () => {
    n++;
    valid = false;
    return make({ headerName: "X-CSRF-TOKEN", csrfToken: "token" });
  }, true);
  assert.equal(
    (await api.deactivate("floor", 42, 11, tag, input, () => valid)).outcome,
    "NOT_DISPATCHED",
  );
  assert.equal(n, 1);
});
test("reason boundaries and invalid inputs send nothing", async () => {
  assert.equal(validReason("a".repeat(10)), true);
  assert.equal(validReason("a".repeat(1000)), true);
  for (const reason of ["short", "a".repeat(1001), "valid reason\n"]) {
    assert.equal(validReason(reason), false);
    let n = 0;
    const api = createStructureDeactivationApi(async () => {
      n++;
      throw Error();
    }, true);
    assert.equal(
      (
        await api.deactivate(
          "floor",
          42,
          11,
          tag,
          { reason, confirmation: true },
          () => true,
        )
      ).outcome,
      "NOT_DISPATCHED",
    );
    assert.equal(n, 0);
  }
});
test("non JSON read rejected", async () =>
  assert.equal(
    (
      await createStructureDeactivationApi(
        async () => new Response("<html>SECRET</html>"),
      ).read("floor", 42, 11)
    ).ok,
    false,
  ));
for (const kind of ["floor", "area"])
  for (const unreadable of [false, true])
    for (const status of [401, 403, 503])
      test(`phaseA ${kind} CSRF ${status} unreadable=${unreadable} preserves status`, async () => {
        let n = 0;
        const api = createStructureDeactivationApi(async () => {
          n++;
          const r = make({ detail: "PRIVATE" }, status);
          if (unreadable)
            r.text = async () => {
              throw Error("PRIVATE");
            };
          return r;
        }, true);
        const r = await api.deactivate(kind, 42, 11, tag, input, () => true);
        assert.equal(r.outcome, "NOT_DISPATCHED");
        assert.equal(r.error.status, status);
        assert.equal(n, 1);
        assert.ok(!JSON.stringify(r).includes("PRIVATE"));
      });

for (const profile of ["network", "malformed", "unreadable-success"])
  test("phaseA " + profile + " does not invent HTTP status", async () => {
    let n = 0;
    const api = createStructureDeactivationApi(async () => {
      n++;
      if (profile === "network") throw Error("PRIVATE");
      const r = make({});
      if (profile === "unreadable-success")
        r.text = async () => {
          throw Error("PRIVATE");
        };
      return r;
    }, true);
    const r = await api.deactivate("floor", 42, 11, tag, input, () => true);
    assert.equal(r.outcome, "NOT_DISPATCHED");
    assert.equal(r.error.status, undefined);
    assert.equal(n, 1);
  });
