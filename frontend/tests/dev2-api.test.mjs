import { test } from "node:test";
import assert from "node:assert/strict";
import { register } from "node:module";
register(new URL("./inventory-ts-loader.mjs", import.meta.url));
const { operations } = await import("../src/dev2/catalog.generated.ts");
const { createDev2Api, payload, operationPath } =
  await import("../src/dev2/api.ts");
const response = (body, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
const session = (id = "42", restricted = false) => ({
  user: { id, username: "staff", name: "Staff" },
  expiresAt: "2026-11-13T20:00:00Z",
  absoluteExpiresAt: "2026-11-14T20:00:00Z",
  mustChangePassword: restricted,
  csrfToken: "private-token",
});
const op = (id) => operations.find((x) => x.id === id);

test("all accepted legacy action IDs have a real UI contract and no actor input is trusted", () => {
  const legacy = operations.filter((x) => x.decisionId);
  assert.equal(legacy.length, 61);
  assert.equal(new Set(legacy.map((x) => x.id)).size, 61);
  for (let i = 1; i <= 61; i++)
    assert.ok(op("A004A-" + String(i).padStart(3, "0")));
  for (const action of operations) assert.match(action.route, /^\/api\//);
  const body = payload(
    op("A004A-021").fields,
    {
      branchId: "3",
      reportedBy: 999,
      similarReviewToken: "test-reviewed-incidents",
      incidentTitle: "Leak",
      description: "Pipe",
      discoveredAt: "2026-11-13T10:00:00Z",
    },
    "42",
  );
  assert.equal(body.reportedBy, 42);
  assert.equal(body.branchId, 3);
});
test("actual scoped GET defaults and explicit query use existing cookie transport", async () => {
  const calls = [];
  const api = createDev2Api(async (url, init) => {
    calls.push({ url, init });
    return response({ page: 2, pageSize: 3, total: 1, items: [] });
  });
  const result = await api(
    op("A004A-034"),
    { id: 34 },
    { page: 2, pageSize: 3 },
    "42",
  );
  assert.equal(result.ok, true);
  assert.equal(
    calls[0].url,
    "/api/maintenance-works/34/material-availability?page=2&pageSize=3",
  );
  assert.equal(calls[0].init.credentials, "include");
  assert.equal(calls[0].init.redirect, "error");
  assert.equal(calls[0].init.cache, "no-store");
});
test("mutation confirms server identity, uses fresh CSRF privately and exact expected201", async () => {
  const calls = [];
  const api = createDev2Api(async (url, init) => {
    calls.push({ url, init });
    return url === "/api/auth/session"
      ? response(session())
      : response({ incidentId: 7 }, 201);
  });
  const r = await api(
    op("A004A-021"),
    {},
    {
      branchId: 3,
      incidentTitle: "Leak",
      description: "Pipe",
      discoveredAt: "2026-11-13T10:00:00Z",
      reportedBy: 999,
      similarReviewToken: "test-reviewed-incidents",
    },
    "42",
  );
  assert.equal(r.ok, true);
  assert.equal(calls.length, 2);
  assert.equal(calls[1].init.headers.get("X-CSRF-TOKEN"), "private-token");
  assert.equal(JSON.parse(calls[1].init.body).reportedBy, 42);
  assert.equal(calls[1].init.headers.has("Authorization"), false);
});
test("changed identity and restricted session prevent business dispatch", async () => {
  for (const s of [session("43"), session("42", true)]) {
    const calls = [];
    const api = createDev2Api(async (url) => {
      calls.push(url);
      return response(s);
    });
    const r = await api(op("A004A-026"), { id: 26 }, {}, "42");
    assert.equal(r.ok, false);
    assert.deepEqual(calls, ["/api/auth/session"]);
  }
});
test("403, stale versions and unknown mutation outcome remain failures; no automatic retry", async () => {
  for (const status of [400, 403, 409, 500]) {
    let writes = 0;
    const api = createDev2Api(async (url) => {
      if (url === "/api/auth/session") return response(session());
      writes++;
      return response({ status, title: "Rejected" }, status);
    });
    const r = await api(
      op("A004A-045"),
      { id: 45 },
      { expectedRowVersion: "AAAAAAAAAAA=" },
      "42",
    );
    assert.equal(r.ok, false);
    assert.equal(r.error.status, status);
    assert.equal(writes, 1);
  }
  let writes = 0;
  const api = createDev2Api(async (url) => {
    if (url === "/api/auth/session") return response(session());
    writes++;
    throw Error("lost response");
  });
  const r = await api(op("A004A-026"), { id: 26 }, {}, "42");
  assert.equal(r.error.kind, "network");
  assert.equal(writes, 1);
});
test("route escapes, unsafe IDs, invalid lines and missing required fields never dispatch", async () => {
  let calls = 0;
  const api = createDev2Api(async () => {
    calls++;
    return response({});
  });
  for (const id of ["../1", "1?branchId=2", 0, "9007199254740992"])
    assert.equal((await api(op("A004A-034"), { id }, {}, "42")).ok, false);
  assert.throws(() => operationPath(op("A004A-034"), { id: "//evil" }));
  assert.throws(() =>
    payload(
      op("A004A-047").fields,
      { expectedRowVersion: "AAAAAAAAAAA=", lines: [] },
      "42",
    ),
  );
  assert.equal(calls, 0);
});
test("nested fulfillment and receipt forms preserve independent concurrency versions and target IDs", () => {
  const f = payload(
    op("A004A-051").fields,
    {
      expectedRowVersion: "AAAAAAAAAAA=",
      mode: "Partial",
      receiverName: "Người nhận thử nghiệm",
      warehouseId: 1,
      documentDate: "2026-11-13",
      lines: [{ requestLineId: 51, quantity: 2 }],
    },
    "42",
  );
  assert.deepEqual(f.lines, [{ requestLineId: 51, quantity: 2 }]);
  assert.equal(f.actorUserId, 42);
  const b = payload(
    op("A004A-059").fields,
    {
      expectedRowVersion: "AAAAAAAAAAA=",
      receiptDocumentId: 59,
      receiptExpectedRowVersion: "AAAAAAAAAAE=",
      receiptLines: [{ lineId: 1, acceptedQty: 3 }],
      lines: [{ stockDocumentLineId: 1, replenishmentLineId: 2 }],
    },
    "42",
  );
  assert.notEqual(b.expectedRowVersion, b.receiptExpectedRowVersion);
  assert.equal(b.receiptLines[0].acceptedQty, 3);
});
