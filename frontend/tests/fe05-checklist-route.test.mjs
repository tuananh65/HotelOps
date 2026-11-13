import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import React from "react";
import { renderToString } from "react-dom/server";
import { MemoryRouter } from "react-router-dom";

await build({
  entryPoints: [
    "src/features/inspectionChecklists/ChecklistRoute.tsx",
    "src/features/inspectionChecklists/api.ts",
    "src/features/inspectionChecklists/model.ts",
  ],
  outdir: ".test-output/ui-a",
  bundle: true,
  platform: "node",
  format: "esm",
  external: ["react", "react-dom", "react-router-dom"],
  loader: { ".css": "empty" },
  outExtension: { ".js": ".mjs" },
});
const { ChecklistRoute, checklistRouteScope } =
  await import("../.test-output/ui-a/ChecklistRoute.mjs");
const { createChecklistApi } = await import("../.test-output/ui-a/api.mjs");
const { roomTypeIds } = await import("../.test-output/ui-a/model.mjs");
const host = {
  authorization: "granted",
  sessionKey: "session-1",
  principalId: "user-1",
  branchId: 7,
  checklistManage: true,
};

for (const [name, context] of [
  ["missing host", undefined],
  ["unresolved", { ...host, authorization: "unresolved" }],
  ["denied", { ...host, authorization: "denied" }],
  ["no permission", { ...host, checklistManage: false }],
  ["missing session", { ...host, sessionKey: "" }],
  ["missing principal", { ...host, principalId: "" }],
  ...["", 0, -1, "1.5", "1e2", "abc", "9007199254740992"].map((branchId) => [
    `branch ${branchId}`,
    { ...host, branchId },
  ]),
])
  test(`direct route fails closed: ${name}`, () => {
    assert.equal(checklistRouteScope(context), null);
    assert.match(
      renderToString(
        React.createElement(ChecklistRoute, { context, initialId: "42" }),
      ),
      /role="alert"/,
    );
  });

test("valid host fixes branch; session, principal and branch changes isolate subtree identity", () => {
  const scope = checklistRouteScope(host);
  assert.equal(scope.branch, 7);
  for (const change of [
    { branchId: 8 },
    { principalId: "user-2" },
    { sessionKey: "session-2" },
  ])
    assert.notEqual(checklistRouteScope({ ...host, ...change }).key, scope.key);
  const html = renderToString(
    React.createElement(
      MemoryRouter,
      { initialEntries: ["/?branchId=999"] },
      React.createElement(ChecklistRoute, { context: host }),
    ),
  );
  assert.ok(!html.includes("999"));
});

test("manual IDs accept safe positive values including multiple applicable types", () => {
  assert.deepEqual(roomTypeIds("11, 12,9007199254740991"), [
    11,
    12,
    Number.MAX_SAFE_INTEGER,
  ]);
});
for (const value of [
  "",
  "0",
  "-1",
  "1.2",
  "1e2",
  "0x10",
  "NaN",
  "11,",
  "11,11",
  "9007199254740992",
])
  test(`manual IDs reject ${JSON.stringify(value)}`, () =>
    assert.throws(() => roomTypeIds(value)));

test("aborted scope sends no request", async () => {
  const scope = new AbortController();
  scope.abort();
  let calls = 0;
  const api = createChecklistApi(async () => {
    calls++;
    throw Error("unexpected");
  }, scope.signal);
  await assert.rejects(api.read(7, 42), { name: "AbortError" });
  assert.equal(calls, 0);
});

test("late CSRF response cannot dispatch a mutation after scope cancellation", async () => {
  const scope = new AbortController();
  const calls = [];
  const api = createChecklistApi(async (path) => {
    calls.push(path);
    scope.abort();
    return new Response(
      JSON.stringify({ csrfToken: "test-only", headerName: "X-CSRF-TOKEN" }),
    );
  }, scope.signal);
  await assert.rejects(api.create(7, {}), { name: "AbortError" });
  assert.deepEqual(calls, ["/api/auth/csrf"]);
});
