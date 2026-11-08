import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { structureReply, secret } from "./independent-fixtures.mjs";
const result = await build({
  stdin: {
    loader: "ts",
    resolveDir: process.cwd(),
    contents: `export {createHotelApi} from './src/hotel/hotelApi';export {boundStructure} from './src/hotel/structureView';`,
  },
  bundle: true,
  write: false,
  format: "esm",
});
const { createHotelApi, boundStructure } = await import(
  "data:text/javascript;base64," +
    Buffer.from(result.outputFiles[0].text).toString("base64")
);
for (const [nodeType, nodeId, collection] of [
  ["branch", undefined, "floors"],
  ["floor", 11, "areas"],
  ["floor", 11, "rooms"],
  ["area", 12, "rooms"],
  ["room", 15, undefined],
  ["department", 13, undefined],
])
  test(`typed API exact query ${nodeType}/${collection ?? "detail"}`, async () => {
    const calls = [];
    const api = createHotelApi(async (url, init) => {
      calls.push({ url, ...init });
      return Response.json(structureReply(url));
    });
    const filters = {
      nodeType,
      nodeId,
      collection,
      isActive: "false",
      page: 2,
      pageSize: 20,
    };
    const r = await api.read("structure", { branchId: 1 }, filters);
    assert.equal(r.ok, true);
    const c = calls[0],
      url = new URL(c.url, "https://offline.test");
    assert.equal(url.pathname, "/api/branches/1/structure");
    assert.equal(url.searchParams.get("nodeType"), nodeType);
    assert.equal(
      url.searchParams.get("nodeId"),
      nodeId === undefined ? null : String(nodeId),
    );
    assert.equal(url.searchParams.get("collection"), collection ?? null);
    assert.equal(url.searchParams.get("page"), "2");
    assert.equal(url.searchParams.get("isActive"), "false");
    assert.equal(c.method, "GET");
    assert.equal(c.credentials, "include");
    assert.equal(c.cache, "no-store");
    assert.equal(new Headers(c.headers).get("Authorization"), null);
    assert.equal(calls.length, 1);
  });
const q = {
  branchId: 1,
  nodeType: "floor",
  nodeId: 11,
  collection: "areas",
  isActive: "",
  page: 1,
  pageSize: 20,
};
const valid = () =>
  structureReply(
    "/api/branches/1/structure?nodeType=floor&nodeId=11&collection=areas&page=1&pageSize=20",
  );
test("view accepts valid and empty page", () => {
  assert.equal(boundStructure(valid(), q), true);
  const d = valid();
  d.areas.items = [];
  d.areas.totalCount = 0;
  assert.equal(boundStructure(d, q), true);
});
for (const change of [
  (d) => (d.branch.id = 2),
  (d) => (d.selectedFloor.id = 99),
  (d) => (d.areas.page = 2),
  (d) => (d.areas.pageSize = 10),
  (d) => (d.availableCollections = []),
  (d) => (d.areas.items[0].floorId = 99),
  (d) => (d.isActive = false),
])
  test("view rejects mismatched context or unavailable collection", () => {
    const d = valid();
    change(d);
    assert.equal(boundStructure(d, q), false);
  });
for (const status of [401, 403, 404, 503])
  test(`API ${status} remains error not empty`, async () => {
    const api = createHotelApi(async () =>
      Response.json({ status, title: secret, detail: secret }, { status }),
    );
    const r = await api.read(
      "structure",
      { branchId: 1 },
      { nodeType: "branch", collection: "floors", page: 1, pageSize: 20 },
    );
    assert.equal(r.ok, false);
    assert.equal(r.error.status, status);
    assert.ok(!JSON.stringify(r).includes(secret));
  });
