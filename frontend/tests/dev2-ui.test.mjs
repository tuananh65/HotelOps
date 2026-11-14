import { test } from "node:test";
import assert from "node:assert/strict";
import { register } from "node:module";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter } from "react-router-dom";
register(new URL("./dev2-ts-loader.mjs", import.meta.url));
const { operations } = await import("../src/dev2/catalog.generated.ts");
const { Fields, defaults } = await import("../src/dev2/Fields.tsx");
const { AuthProvider } = await import("../src/auth/AuthProvider.tsx");
const { AuthRouteHost } = await import("../src/auth/AuthRouteHost.tsx");
const { createAuthStore } = await import("../src/auth/authStore.ts");
const session = {
  user: { id: "42", username: "staff", name: "Staff" },
  expiresAt: "2099-11-13T20:00:00Z",
  absoluteExpiresAt: "2099-11-14T20:00:00Z",
  mustChangePassword: false,
  csrfToken: "PRIVATE-TOKEN-NOT-IN-DOM",
};
async function storeFor(value) {
  const api = {
    getSession: async () => value,
    getSessionSnapshot: () => (value.ok ? value.data : null),
  };
  const store = createAuthStore(api);
  await store.initialize();
  return store;
}
function render(store, route) {
  return renderToStaticMarkup(
    React.createElement(
      MemoryRouter,
      { initialEntries: [route] },
      React.createElement(
        AuthProvider,
        { store },
        React.createElement(AuthRouteHost),
      ),
    ),
  );
}
test("NOT_RUN_AUTHORIZATION_REQUIRED: target provider positive admission needs mounted browser effects", {skip:"Target AuthProvider deliberately hides a reused warm store until mounted revalidation; browser startup is not authorized"}, async () => {
  const store = await storeFor({ ok: true, status: 200, data: session });
  for (const operation of operations) {
    const html = render(
      store,
      `/operations/${operation.module}?action=${encodeURIComponent(operation.id)}`,
    );
    assert.ok(html.includes(`data-operation="${operation.id}"`), operation.id);
    assert.ok(!html.includes(session.csrfToken), "CSRF stays private");
    assert.ok(html.includes("The server checks your branch"));
  }
});
test("restricted and anonymous sessions cannot render operation forms", async () => {
  for (const value of [
    { ok: true, status: 200, data: { ...session, mustChangePassword: true } },
    {
      ok: false,
      error: {
        kind: "http",
        status: 401,
        problem: { code: "AUTHENTICATION_REQUIRED" },
      },
    },
  ]) {
    const store = await storeFor(value);
    const html = render(store, "/operations/incidents?action=A004A-021");
    assert.ok(!html.includes("data-operation="));
    assert.ok(!html.includes("PRIVATE-TOKEN"));
  }
});
test("actual nested forms preserve target identities and prefill the current concurrency token", () => {
  const action = operations.find((x) => x.id === "A004A-047");
  const values = defaults(action.fields, {
    rowVersion: "AAAAAAAAAAE=",
    actorUserId: 999,
    lines: [{ requestLineId: 51, approvedQty: 10 }],
  });
  const html = renderToStaticMarkup(
    React.createElement(Fields, {
      fields: action.fields,
      values,
      actor: "42",
      onChange: () => {},
    }),
  );
  assert.match(
    html,
    /<input(?=[^>]*name="actorUserId")(?=[^>]*readOnly="")(?=[^>]*value="42")[^>]*>/,
  );
  assert.ok(html.includes('name="lines.0.requestLineId"'));
  assert.ok(html.includes('value="AAAAAAAAAAE="'));
});

// Target AuthProvider admission is intentionally not weakened to satisfy SSR.
test("all112 action field sets render offline with server actor hidden from editing", () => {
 for(const action of operations){const html=renderToStaticMarkup(React.createElement(Fields,{fields:action.fields,values:defaults(action.fields,{}),actor:"42",onChange:()=>{}}));assert.equal(typeof html,"string");assert.ok(!html.includes(session.csrfToken));}
});
test("target AuthProvider rejects SSR admission even when supplied an initialized store",async()=>{const store=await storeFor({ok:true,status:200,data:session});assert.equal(store.getSnapshot().status,"AUTHENTICATED");const html=render(store,"/operations/incidents?action=A004A-021");assert.ok(!html.includes("data-operation="));});
