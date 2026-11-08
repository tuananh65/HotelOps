import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { shift, day, secret } from "./workday-fixtures.mjs";
const bundle = await build({
  entryPoints: ["src/workday/workdayApi.ts"],
  bundle: true,
  write: false,
  format: "esm",
  platform: "browser",
});
const { createWorkdayApi } = await import(
  `data:text/javascript;base64,${Buffer.from(bundle.outputFiles[0].text).toString("base64")}`
);
function harness(reply) {
  const calls = [];
  const api = createWorkdayApi(async (url, init) => {
    calls.push({ url, ...init });
    if (reply.error) throw reply.error;
    return new Response(
      reply.status === 204 ? null : (reply.raw ?? JSON.stringify(reply.body)),
      {
        status: reply.status ?? 200,
        headers: { "Content-Type": reply.type ?? "application/json" },
      },
    );
  });
  return { api, calls };
}
for (const [op, body, expected] of [
  ["ownShifts", [shift()], [shift()]],
  ["current", day(), day()],
]) {
  test(`${op} valid DTO, scoped GET and transport options`, async () => {
    const h = harness({ body });
    const signal = new AbortController().signal;
    assert.deepEqual(await h.api[op](1, signal), { ok: true, data: expected });
    assert.equal(h.calls.length, 1);
    const c = h.calls[0];
    assert.equal(
      c.url,
      `/api/branches/1/workdays/${op === "current" ? "current" : "own-shifts"}`,
    );
    assert.equal(c.method, "GET");
    assert.equal(c.credentials, "include");
    assert.equal(c.cache, "no-store");
    assert.equal(c.redirect, "error");
    assert.equal(c.signal, signal);
    assert.equal(c.body, undefined);
    assert.deepEqual(Object.fromEntries(c.headers), {
      accept: "application/json",
    });
  });
}
test("explicit 204 is empty current; array empty is empty own-shifts", async () => {
  assert.deepEqual(await harness({ status: 204 }).api.current(1), {
    ok: true,
    data: null,
  });
  assert.deepEqual(await harness({ body: [] }).api.ownShifts(1), {
    ok: true,
    data: [],
  });
  assert.deepEqual(await harness({ status: 204 }).api.ownShifts(1), {
    ok: false,
    error: "invalid-response",
  });
});
for (const [status, error] of [
  [401, "unauthorized"],
  [403, "forbidden"],
  [404, "unavailable"],
  [405, "unavailable"],
  [503, "unavailable"],
  [500, "http"],
]) {
  for (const op of ["current", "ownShifts"])
    test(`${op} ${status} is sanitized ${error}, never empty`, async () => {
      const h = harness({
        status,
        body: { status, title: secret, detail: secret, traceId: secret },
      });
      assert.deepEqual(await h.api[op](1), { ok: false, error });
      assert.equal(h.calls.length, 1);
    });
}
for (const body of [
  null,
  {},
  { ...day(), branchId: 2 },
  { ...day(), workdayId: Number.MAX_SAFE_INTEGER + 1 },
  { ...day(), workDate: "2026-02-30" },
  { ...day(), startedAtUtc: "invalid" },
  { ...day(), rowVersion: "weak" },
  { ...day(), openInterval: {} },
]) {
  test(`malformed current ${JSON.stringify(body)} rejected`, async () => {
    assert.deepEqual(await harness({ body }).api.current(1), {
      ok: false,
      error: "invalid-response",
    });
  });
}
for (const body of [
  { items: [] },
  [shift(2)],
  [{ ...shift(), shiftId: "11" }],
  [{ ...shift(), scheduledEndAtUtc: "2026-11-07T00:00:00Z" }],
  [shift(), shift()],
]) {
  test(`malformed shifts ${JSON.stringify(body)} rejected`, async () => {
    assert.deepEqual(await harness({ body }).api.ownShifts(1), {
      ok: false,
      error: "invalid-response",
    });
  });
}
test("valid open interval and extra fields are detached/allowlisted", async () => {
  const expected = {
    ...day(),
    openInterval: {
      intervalId: 30,
      workItemId: 31,
      workItemKind: "TASK",
      openedAtUtc: "2026-11-08T02:00:00.1234567+00:00",
      rowVersion: "AAAAAAAAAAE=",
    },
  };
  const h = harness({
    body: {
      ...expected,
      userId: 999,
      token: secret,
      openInterval: { ...expected.openInterval, secret },
    },
  });
  assert.deepEqual(await h.api.current(1), { ok: true, data: expected });
});
for (const reply of [{ raw: "{" }, { raw: "<html>", type: "text/html" }])
  test("malformed/non-JSON response", async () => {
    assert.deepEqual(await harness(reply).api.current(1), {
      ok: false,
      error: "invalid-response",
    });
  });
for (const [error, kind] of [
  [new Error(secret), "network"],
  [new DOMException(secret, "AbortError"), "cancelled"],
])
  test(`transport ${kind} no retry`, async () => {
    const h = harness({ error });
    assert.deepEqual(await h.api.current(1), { ok: false, error: kind });
    assert.equal(h.calls.length, 1);
  });
test("unsafe branch rejected without dispatch", async () => {
  const h = harness({ body: [] });
  for (const id of [0, -1, NaN, 1.5, Number.MAX_SAFE_INTEGER + 1])
    assert.deepEqual(await h.api.ownShifts(id), {
      ok: false,
      error: "invalid-request",
    });
  assert.equal(h.calls.length, 0);
});
test("concurrent current reads cannot mix empty flags", async () => {
  const resolvers = [];
  const api = createWorkdayApi(() => new Promise((r) => resolvers.push(r)));
  const a = api.current(1),
    b = api.current(2);
  resolvers[1](
    new Response(JSON.stringify(day(2)), {
      headers: { "Content-Type": "application/json" },
    }),
  );
  resolvers[0](new Response(null, { status: 204 }));
  assert.deepEqual(await a, { ok: true, data: null });
  assert.deepEqual(await b, { ok: true, data: day(2) });
});
