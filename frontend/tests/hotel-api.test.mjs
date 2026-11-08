import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import {
  tag,
  nextTag,
  secret,
  structure,
  editable,
  area,
  typeItem,
  roomTypes,
  room,
  rooms,
  roomDetail,
  department,
  mutationCases,
  requests,
} from "./hotel-fixtures.mjs";
const bundle = await build({
  entryPoints: ["src/hotel/hotelApi.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  write: false,
});
const { createHotelApi } = await import(
  `data:text/javascript;base64,${Buffer.from(bundle.outputFiles[0].text).toString("base64")}`
);
const response = (body, status = 200, etag = tag) =>
  new Response(JSON.stringify(body), {
    status,
    headers: {
      "Content-Type": "application/json",
      ...(etag == null ? {} : { ETag: etag }),
    },
  });
function harness(
  reply,
  csrf = () => response({ headerName: "X-CSRF-TOKEN", csrfToken: secret }),
) {
  const calls = [];
  const api = createHotelApi(async (url, options) => {
    calls.push({
      url,
      ...options,
      headers: Object.fromEntries(new Headers(options.headers)),
    });
    return url === "/api/auth/csrf" ? csrf() : reply(url, options);
  });
  return { api, calls };
}
const readCases = [
  ["structure", undefined, "/api/branches/1/structure", structure()],
  ["floor", 11, "/api/branches/1/floors/11", editable],
  [
    "area",
    12,
    "/api/branches/1/areas/12",
    { ...area, kind: "area", floorNumber: null },
  ],
  ["roomType", 14, "/api/room-types/14?branchId=1", typeItem],
  ["roomTypes", undefined, "/api/room-types?branchId=1", roomTypes],
  ["roomEdit", 15, "/api/rooms/15/edit?branchId=1", room],
  ["rooms", undefined, "/api/rooms?branchId=1", rooms],
  ["roomDetail", 15, "/api/rooms/15?branchId=1", roomDetail],
  ["department", 13, "/api/branches/1/departments/13", department],
];
for (const [kind, id, url, dto] of readCases)
  test(`real ${kind} read contract and DTO allowlist`, async () => {
    const h = harness(() =>
      response({ ...dto, csrfToken: secret, detail: secret }),
    );
    const result = await h.api.read(kind, { branchId: 1, id });
    assert.equal(result.ok, true);
    assert.deepEqual(result.data, dto);
    assert.equal(h.calls.length, 1);
    assert.equal(h.calls[0].url, url);
    assert.equal(h.calls[0].method, "GET");
    assert.equal(h.calls[0].credentials, "include");
    assert.equal(h.calls[0].cache, "no-store");
    assert.equal(h.calls[0].redirect, "error");
    assert.equal(h.calls[0].headers.authorization, undefined);
    assert.equal(h.calls[0].headers["x-csrf-token"], undefined);
    assert.equal(JSON.stringify(result).includes(secret), false);
  });
for (const [kind, id, url, dto] of mutationCases)
  test(`${kind} sends exact method, URL, payload, CSRF and version once`, async () => {
    const create = kind.startsWith("create");
    const h = harness(() =>
      response({ ...dto, traceId: secret }, create ? 201 : 200, nextTag),
    );
    const result = await h.api.mutate(
      kind,
      { branchId: 1, id },
      requests[kind],
      create ? null : tag,
    );
    assert.equal(result.outcome, "CONFIRMED");
    assert.deepEqual(result.data, dto);
    assert.equal(h.calls.length, 2);
    const c = h.calls[1];
    assert.equal(h.calls[0].url, "/api/auth/csrf");
    assert.equal(h.calls[0].method, "GET");
    assert.equal(c.url, url);
    assert.equal(c.method, kind.startsWith("update") ? "PUT" : "POST");
    assert.deepEqual(JSON.parse(c.body), requests[kind]);
    assert.equal(c.headers["x-csrf-token"], secret);
    assert.equal(c.headers["if-match"], create ? undefined : tag);
    assert.equal(c.headers.authorization, undefined);
    for (const call of h.calls) {
      assert.equal(call.credentials, "include");
      assert.equal(call.cache, "no-store");
      assert.equal(call.redirect, "error");
    }
    assert.equal(JSON.stringify(result).includes(secret), false);
  });
for (const value of [
  0,
  -1,
  1.5,
  "1",
  NaN,
  Infinity,
  Number.MAX_SAFE_INTEGER + 1,
])
  test(`invalid branch ${String(value)} does not fetch`, async () => {
    const h = harness(() => response(structure()));
    assert.equal(
      (await h.api.read("structure", { branchId: value })).ok,
      false,
    );
    assert.equal(
      (
        await h.api.mutate(
          "createFloor",
          { branchId: value },
          requests.createFloor,
        )
      ).outcome,
      "NOT_DISPATCHED",
    );
    assert.equal(h.calls.length, 0);
  });
for (const bad of [
  null,
  "W/" + tag,
  '"ABCDEF0123456789"',
  '"' + "a".repeat(64) + '"',
  tag + "\n",
])
  test(`invalid update ETag ${JSON.stringify(bad)} does not dispatch`, async () => {
    const h = harness(() => response(editable));
    assert.equal(
      (
        await h.api.mutate(
          "updateFloor",
          { branchId: 1, id: 11 },
          requests.updateFloor,
          bad,
        )
      ).outcome,
      "NOT_DISPATCHED",
    );
    assert.equal(h.calls.length, 0);
  });
for (const dto of [
  { ...editable, id: 19 },
  { ...editable, branchId: 2 },
  { ...editable, kind: "area" },
  { ...editable, id: Number.MAX_SAFE_INTEGER + 1 },
])
  test(`invalid read identity ${JSON.stringify(dto)} is rejected`, async () => {
    const h = harness(() => response(dto));
    assert.equal(
      (await h.api.read("floor", { branchId: 1, id: 11 })).ok,
      false,
    );
  });
for (const etag of [null, "W/" + tag, '"ABCDEF0123456789"'])
  test(`management GET rejects missing/invalid ETag ${etag}`, async () => {
    const h = harness(() => response(editable, 200, etag));
    assert.equal(
      (await h.api.read("floor", { branchId: 1, id: 11 })).ok,
      false,
    );
  });
test("nested branch leakage is rejected before UI", async () => {
  const data = structure();
  data.floors.items[0] = { ...data.floors.items[0], branchId: 2 };
  const h = harness(() => response(data));
  assert.equal((await h.api.read("structure", { branchId: 1 })).ok, false);
});
test("literal filters are URL-encoded, not path injected", async () => {
  const h = harness(() => response(rooms));
  await h.api.read(
    "rooms",
    { branchId: 1 },
    {
      search: "A&B /%",
      page: 2,
      pageSize: 20,
      floorId: 11,
      operationalStatusCode: "DIRTY",
    },
  );
  const url = new URL(h.calls[0].url, "https://test.local");
  assert.equal(url.pathname, "/api/rooms");
  assert.equal(url.searchParams.get("search"), "A&B /%");
  assert.equal(url.searchParams.get("floorId"), "11");
});
for (const [status, title, expected] of [
  [400, "VALIDATION_FAILED", "REJECTED"],
  [401, "SESSION_INVALID", "REJECTED"],
  [403, "PERMISSION_DENIED", "REJECTED"],
  [404, "NOTFOUND", "REJECTED"],
  [409, "STRUCTURE_CODE_EXISTS", "REJECTED"],
  [412, "STALE", "REJECTED"],
  [428, "IF_MATCH_REQUIRED", "REJECTED"],
  [500, "INTERNAL_SERVER_ERROR", "OUTCOME_UNKNOWN"],
  [503, "UNAVAILABLE", "OUTCOME_UNKNOWN"],
  [409, "UNKNOWN", "OUTCOME_UNKNOWN"],
])
  test(`mutation ${status}/${title} preserves failure outcome without retry`, async () => {
    const h = harness(() =>
      response(
        { status, title, detail: secret, traceId: secret, csrfToken: secret },
        status,
      ),
    );
    const result = await h.api.mutate(
      "createFloor",
      { branchId: 1 },
      requests.createFloor,
    );
    assert.equal(result.outcome, expected);
    assert.equal(result.error.status, status);
    assert.equal(h.calls.length, 2);
    assert.equal(JSON.stringify(result).includes(secret), false);
    assert.equal("data" in result, false);
  });
for (const fields of [
  { code: "STRUCTURE_CODE_EXISTS", title: "DUPLICATE" },
  { code: "STRUCTURE_CODE_EXISTS", title: null },
  { status: 400, title: "DUPLICATE" },
])
  test(`contradictory/malformed 409 remains unknown ${JSON.stringify(fields)}`, async () => {
    const h = harness(() => response(fields, 409));
    assert.equal(
      (await h.api.mutate("createFloor", { branchId: 1 }, requests.createFloor))
        .outcome,
      "OUTCOME_UNKNOWN",
    );
    assert.equal(h.calls.length, 2);
  });
for (const kind of ["network", "timeout", "cancelled"])
  test(`post-dispatch ${kind} is unknown, never retried`, async () => {
    const h = harness(() => {
      throw kind === "network"
        ? new Error(secret)
        : new DOMException(
            secret,
            kind === "timeout" ? "TimeoutError" : "AbortError",
          );
    });
    const result = await h.api.mutate(
      "createFloor",
      { branchId: 1 },
      requests.createFloor,
    );
    assert.equal(result.outcome, "OUTCOME_UNKNOWN");
    assert.equal(result.error.kind, kind);
    assert.equal(h.calls.length, 2);
    assert.equal(JSON.stringify(result).includes(secret), false);
  });
for (const make of [
  () =>
    new Response("{", {
      status: 201,
      headers: { "Content-Type": "application/json" },
    }),
  () =>
    new Response("<html>" + secret, {
      status: 201,
      headers: { "Content-Type": "text/html" },
    }),
  () => response(null, 201),
  () => response({ id: 1 }, 201),
])
  test("malformed success never claims committed confirmation", async () => {
    const h = harness(make);
    assert.equal(
      (await h.api.mutate("createFloor", { branchId: 1 }, requests.createFloor))
        .outcome,
      "OUTCOME_UNKNOWN",
    );
    assert.equal(h.calls.length, 2);
  });
test("failed CSRF bootstrap does not dispatch mutation", async () => {
  const h = harness(
    () => response(editable),
    () => response({}, 400),
  );
  assert.equal(
    (await h.api.mutate("createFloor", { branchId: 1 }, requests.createFloor))
      .outcome,
    "NOT_DISPATCHED",
  );
  assert.equal(h.calls.length, 1);
});
test("cancellation during bootstrap prevents mutation", async () => {
  const abort = new AbortController();
  const h = harness(
    () => response(editable),
    () => {
      abort.abort();
      return response({ csrfToken: secret, headerName: "X-CSRF-TOKEN" });
    },
  );
  assert.equal(
    (
      await h.api.mutate(
        "createFloor",
        { branchId: 1 },
        requests.createFloor,
        null,
        abort.signal,
      )
    ).outcome,
    "NOT_DISPATCHED",
  );
  assert.equal(h.calls.length, 1);
});
test("caller changes during CSRF await cannot replace target or payload", async () => {
  let release;
  const c = { branchId: 1 };
  const body = { ...requests.createFloor };
  const h = harness(
    () => response(editable, 201),
    () =>
      new Promise((r) => {
        release = r;
      }),
  );
  const pending = h.api.mutate("createFloor", c, body);
  c.branchId = 9;
  body.name = "changed";
  release(response({ csrfToken: secret, headerName: "X-CSRF-TOKEN" }));
  assert.equal((await pending).outcome, "CONFIRMED");
  assert.equal(h.calls[1].url, "/api/branches/1/floors");
  assert.equal(JSON.parse(h.calls[1].body).name, "Server Floor");
});
test("overposted fields/accessors are rejected before fetch", async () => {
  let invoked = false;
  const h = harness(() => response(editable));
  const body = { ...requests.createFloor, permission: "ADMIN" };
  assert.equal(
    (await h.api.mutate("createFloor", { branchId: 1 }, body)).outcome,
    "NOT_DISPATCHED",
  );
  const getter = { ...requests.createFloor };
  Object.defineProperty(getter, "name", {
    enumerable: true,
    get() {
      invoked = true;
      return secret;
    },
  });
  assert.equal(
    (await h.api.mutate("createFloor", { branchId: 1 }, getter)).outcome,
    "NOT_DISPATCHED",
  );
  assert.equal(invoked, false);
  assert.equal(h.calls.length, 0);
});

for (const kind of ["rooms", "roomTypes"])
  test(`${kind} all-branches mode never guesses branch membership`, async () => {
    const data = kind === "rooms" ? rooms : { ...roomTypes, branchId: null };
    const h = harness(() => response(data));
    assert.equal(
      (await h.api.read(kind, { branchId: 0 }, { allBranches: true })).ok,
      true,
    );
    const url = new URL(h.calls[0].url, "http://test.local");
    assert.equal(url.searchParams.has("branchId"), false);
    assert.equal(
      url.searchParams.get("allBranches"),
      kind === "rooms" ? "true" : null,
    );
    const invalid = harness(() => response(data));
    assert.equal(
      (await invalid.api.read(kind, { branchId: 1 }, { allBranches: true })).ok,
      false,
    );
    assert.equal(invalid.calls.length, 0);
  });
test("selected structure node must match requested identity", async () => {
  const data = {
    ...structure("areas"),
    nodeType: "floor",
    selectedFloor: { ...editable, id: 11 },
  };
  const h = harness(() => response(data));
  assert.equal(
    (
      await h.api.read(
        "structure",
        { branchId: 1 },
        { nodeType: "floor", nodeId: 11, collection: "areas" },
      )
    ).ok,
    true,
  );
  assert.equal(
    (
      await h.api.read(
        "structure",
        { branchId: 1 },
        { nodeType: "floor", nodeId: 999, collection: "areas" },
      )
    ).ok,
    false,
  );
});

test("UC018 optional code is sent only when explicitly supplied", async () => {
  const item = mutationCases.find((c) => c[0] === "updateType")[3];
  const h = harness(() => response({ ...item, code: "DLX2" }, 200, nextTag));
  const result = await h.api.mutate(
    "updateType",
    { branchId: 1, id: 14 },
    { ...requests.updateType, code: "DLX2" },
    tag,
  );
  assert.equal(result.outcome, "CONFIRMED");
  assert.equal(result.data.code, "DLX2");
  assert.equal(JSON.parse(h.calls[1].body).code, "DLX2");
});
test("UC019 exposes only allowlisted server dependency references", async () => {
  const h = harness(() =>
    response(
      {
        status: 409,
        title: "ACTIVE_ROOMS_EXIST",
        rooms: [
          { id: 15, roomNumber: "101", detail: secret, csrfToken: secret },
        ],
        traceId: secret,
      },
      409,
    ),
  );
  const result = await h.api.mutate(
    "deactivateType",
    { branchId: 1, id: 14 },
    requests.deactivateType,
    tag,
  );
  assert.equal(result.outcome, "REJECTED");
  assert.deepEqual(result.error.activeRooms, [{ id: 15, roomNumber: "101" }]);
  assert.equal(JSON.stringify(result).includes(secret), false);
  assert.equal(h.calls.length, 2);
});
