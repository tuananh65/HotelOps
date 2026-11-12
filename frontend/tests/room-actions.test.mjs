import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
const bundle = await build({
  entryPoints: ["src/api/dev1/roomActions.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  write: false,
  define: { "import.meta.env": "{}" },
});
const loadBundle = (output) =>
  import(
    `data:text/javascript;base64,${Buffer.from(output).toString("base64")}`
  );
const { cleaningActionHref } = await loadBundle(bundle.outputFiles[0].text);
const boardBundle = await build({
  entryPoints: ["src/screens/rooms/RoomStatusBoardScreen.tsx"],
  bundle: true,
  platform: "node",
  format: "esm",
  packages: "external",
  write: false,
  define: { "import.meta.env": "{}" },
});
// Resolve external packages before loading the in-memory module.
import { createRequire } from "node:module";
import { pathToFileURL } from "node:url";
const require = createRequire(import.meta.url);
const boardSource = boardBundle.outputFiles[0].text.replace(
  /from "(react(?:\/jsx-runtime)?|react-router-dom)"/g,
  (_, name) => `from "${pathToFileURL(require.resolve(name)).href}"`,
);
const { parseRoomContext } = await loadBundle(boardSource);

test("optional context stays missing; canonical scoped IDs are preserved", () => {
  for (const [query, expected] of [
    ["", { branch: "", room: "" }],
    ["branchId=7", { branch: "7", room: "" }],
    ["branchId=7&roomId=11", { branch: "7", room: "11" }],
    ["branchId=9007199254740991", { branch: "9007199254740991", room: "" }],
  ])
    assert.deepEqual(parseRoomContext(new URLSearchParams(query)), expected);
});

test("explicit invalid or duplicate context fails closed", () => {
  const invalid = [
    "",
    "0",
    "-1",
    "01",
    "+1",
    "1.0",
    "1e2",
    "NaN",
    "Infinity",
    " 1",
    "1\n",
    "9007199254740992",
    "1&roomId=99",
  ];
  for (const name of ["branchId", "roomId"]) {
    for (const value of invalid) {
      const params = new URLSearchParams({ branchId: "7", roomId: "11" });
      params.set(name, value);
      assert.throws(
        () => parseRoomContext(params),
        `${name}=${JSON.stringify(value)}`,
      );
    }
    for (const value of [name === "branchId" ? "7" : "11", "99", ""]) {
      const params = new URLSearchParams({ branchId: "7", roomId: "11" });
      params.append(name, value);
      assert.throws(() => parseRoomContext(params));
    }
  }
  assert.throws(() => parseRoomContext(new URLSearchParams("roomId=11")));
});
const options = {
  roomId: 11,
  branchId: 7,
  roomNumber: "A101",
  cleaningTypes: [],
  priorities: [],
  cleaningItemCount: 0,
};
test("authorized navigation uses scoped GET with cookies, never mutation", async () => {
  const abort = new AbortController();
  globalThis.fetch = async (url, init) => {
    assert.equal(url, "/api/branches/7/cleaning-task-options?roomId=11");
    assert.equal(init.method, "GET");
    assert.equal(init.credentials, "include");
    assert.equal(init.signal, abort.signal);
    return Response.json(options);
  };
  assert.equal(
    await cleaningActionHref("7", "11", abort.signal),
    "/cleaning/tasks/new?branchId=7&roomId=11",
  );
});
test("invalid context fails before network", async () => {
  let calls = 0;
  globalThis.fetch = async () => {
    calls++;
    throw Error("unexpected");
  };
  for (const [b, r] of [
    ["0", "11"],
    ["7", "-1"],
    ["7", "1&branchId=8"],
    ["9007199254740992", "11"],
  ])
    await assert.rejects(cleaningActionHref(b, r));
  assert.equal(calls, 0);
});
test("cross-branch or wrong-room options never produce a link", async () => {
  for (const bad of [
    { ...options, branchId: 8 },
    { ...options, roomId: 12 },
    {},
  ]) {
    globalThis.fetch = async () => Response.json(bad);
    await assert.rejects(cleaningActionHref("7", "11"));
  }
});
test("denied, expired or unavailable destinations fail closed", async () => {
  for (const status of [401, 403, 404, 409, 503]) {
    globalThis.fetch = async () =>
      Response.json({ title: "DENIED" }, { status });
    await assert.rejects(cleaningActionHref("7", "11"));
  }
});
test("canceled scope cannot yield navigation", async () => {
  const abort = new AbortController();
  globalThis.fetch = async () => {
    abort.abort();
    return Response.json(options);
  };
  await assert.rejects(cleaningActionHref("7", "11", abort.signal));
});
