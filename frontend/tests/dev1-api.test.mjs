import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: ["src/api/dev1/rooms.ts", "src/api/dev1/client.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outdir: ".test-output/dev1",
  define: { "import.meta.env": "{}" },
});
const { listRooms, positiveId } = await import("../.test-output/dev1/rooms.js");
const { getJson, apiBase, errorMessage, ApiError } =
  await import("../.test-output/dev1/client.js");
const room = {
  id: 12,
  branchId: 7,
  roomNumber: "101",
  floor: { id: 3, code: "F1", name: "Floor 1", number: 1 },
  area: null,
  roomType: { id: 8, code: "STD", name: "Standard" },
  operationalStatusCode: "DIRTY",
  isActive: true,
};
const response = (items = [room]) => ({
  items,
  page: 1,
  pageSize: 20,
  totalCount: items.length,
});

test("DEV1 scoped read sends cookie credentials and filters without prototype identity", async () => {
  const abort = new AbortController();
  globalThis.fetch = async (url, options) => {
    const query = new URL(url, "https://localhost").searchParams;
    assert.equal(query.get("branchId"), "7");
    assert.equal(query.get("operationalStatusCode"), "DIRTY");
    assert.equal(query.get("search"), "101 & 102");
    assert.equal(query.has("allBranches"), false);
    assert.equal(options.credentials, "include");
    assert.equal(options.cache, "no-store");
    assert.equal(options.redirect, "error");
    assert.equal(options.signal, abort.signal);
    assert.deepEqual(options.headers, { Accept: "application/json" });
    return Response.json(response());
  };
  assert.equal(
    (await listRooms("7", 1, "DIRTY", "101 & 102", abort.signal)).items[0]
      .operationalStatusCode,
    "DIRTY",
  );
});
test("DEV1 rejects invalid scope and prototype lifecycle filters before request", async () => {
  globalThis.fetch = () => {
    throw new Error("must not fetch");
  };
  for (const id of ["0", "-1", "abc", "9007199254740993", "1e2"])
    assert.throws(() => positiveId(id));
  await assert.rejects(
    listRooms("7", 1, "Pending Cleaning", ""),
    /Invalid filter/,
  );
  await assert.rejects(listRooms("7", NaN, "", ""), /Invalid filter/);
});
test("DEV1 accepts empty data; rejects wrong branch, unsafe ID and malformed DTO", async () => {
  globalThis.fetch = async () => Response.json(response([]));
  assert.deepEqual((await listRooms("7", 1, "", "")).items, []);
  for (const invalid of [
    { ...room, branchId: 8 },
    { ...room, id: 9007199254740992 },
    { ...room, floor: null },
  ]) {
    globalThis.fetch = async () => Response.json(response([invalid]));
    await assert.rejects(
      listRooms("7", 1, "", ""),
      (error) => error.code === "INVALID_RESPONSE",
    );
  }
});
test("DEV1 distinguishes 401/403/503 ProblemDetails; no automatic retry", async () => {
  for (const status of [401, 403, 503]) {
    let calls = 0;
    globalThis.fetch = async () => {
      calls++;
      return Response.json({ title: "DENIED" }, { status });
    };
    await assert.rejects(
      getJson("/rooms"),
      (error) => error.status === status && error.code === "DENIED",
    );
    assert.equal(calls, 1);
  }
  assert.notEqual(
    errorMessage(new ApiError(401, "DENIED")),
    errorMessage(new ApiError(403, "DENIED")),
  );
});
test("DEV1 handles non-JSON and network failure without mock fallback", async () => {
  globalThis.fetch = async () => new Response("<html>proxy error</html>");
  await assert.rejects(
    getJson("/rooms"),
    (error) => error.code === "INVALID_RESPONSE",
  );
  globalThis.fetch = async () => {
    throw new TypeError("Failed to fetch");
  };
  await assert.rejects(getJson("/rooms"), (error) => error.status === 0);
});
test("DEV1 propagates request cancellation", async () => {
  const controller = new AbortController();
  controller.abort();
  globalThis.fetch = async (_, options) => {
    options.signal.throwIfAborted();
  };
  await assert.rejects(
    getJson("/rooms", controller.signal),
    (error) => error.name === "AbortError",
  );
});
test("DEV1 rejects cross-origin API base", () => {
  assert.equal(apiBase("/api/"), "/api");
  for (const base of [
    "https://example.com/api",
    "//example.com/api",
    "/\\example.com",
    "/api?token=x",
  ])
    assert.throws(() => apiBase(base));
});
