import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { role, permission, detail, page } from "./role-catalog-fixtures.mjs";
const b = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "ts",
    contents: "export * from './src/roles/roleCatalogApi';",
  },
  bundle: true,
  write: false,
  format: "esm",
});
const { createRoleCatalogApi } = await import(
  "data:text/javascript;base64," +
    Buffer.from(b.outputFiles[0].text).toString("base64")
);
for (const [name, url, body, call] of [
  ["list", "/api/roles?page=1&pageSize=25", page([role()]), (a) => a.roles()],
  ["detail", "/api/roles/1", detail(), (a) => a.detail(1)],
  [
    "grants",
    "/api/roles/1/permissions",
    { roleId: 1, grants: detail().grants },
    (a) => a.grants(1),
  ],
  [
    "catalog",
    "/api/permissions/catalog?page=1&pageSize=25",
    page([permission()]),
    (a) => a.permissions(),
  ],
])
  test("GET " + name, async () => {
    const calls = [];
    const a = createRoleCatalogApi(async (u, o) => {
      calls.push([u, o]);
      return Response.json({ ...body, secret: "SECRET" });
    });
    const r = await call(a);
    assert.equal(r.ok, true);
    assert.equal(calls[0][0], url);
    assert.equal(calls[0][1].method, "GET");
    assert.equal(calls[0][1].credentials, "include");
    assert.equal(calls[0][1].cache, "no-store");
    assert.ok(!new Headers(calls[0][1].headers).has("Authorization"));
    assert.ok(!JSON.stringify(r).includes("SECRET"));
  });
for (const status of [401, 403, 404, 503])
  test("sanitized status " + status, async () => {
    let n = 0;
    const r = await createRoleCatalogApi(async () => {
      n++;
      return Response.json(
        { title: "SECRET", detail: "SECRET", traceId: "SECRET" },
        { status },
      );
    }).roles();
    assert.equal(r.ok, false);
    assert.equal(r.status, status);
    assert.ok(!JSON.stringify(r).includes("SECRET"));
    assert.equal(n, 1);
  });
for (const [name, body] of [
  ["wrong role", detail(2)],
  [
    "invalid count",
    { ...detail(), role: { ...role(), assignedUserCount: -1 } },
  ],
  [
    "invalid scope",
    { ...detail(), grants: [{ ...detail().grants[0], dataScope: "GLOBAL" }] },
  ],
  ["missing field", { ...detail(), role: { ...role(), isActive: undefined } }],
])
  test("reject " + name, async () =>
    assert.equal(
      (await createRoleCatalogApi(async () => Response.json(body)).detail(1))
        .ok,
      false,
    ),
  );
test("safe query encoding and filter", async () => {
  let url;
  const api = createRoleCatalogApi(async (u) => {
    url = u;
    return Response.json(page([]));
  });
  assert.equal((await api.roles(1, "R&x", "false")).ok, true);
  assert.equal(
    url,
    "/api/roles?page=1&pageSize=25&search=R%26x&isActive=false",
  );
});
test("invalid query/id sends no request", async () => {
  let n = 0;
  const a = createRoleCatalogApi(async () => {
    n++;
    throw Error();
  });
  for (const r of [
    await a.roles(0),
    await a.detail(-1),
    await a.permissions(1000001),
  ])
    assert.equal(r.ok, false);
  assert.equal(n, 0);
});
test("non JSON and malformed JSON rejected", async () => {
  for (const r of [
    new Response("<html>"),
    new Response("{", { headers: { "Content-Type": "application/json" } }),
  ])
    assert.equal((await createRoleCatalogApi(async () => r).roles()).ok, false);
});
test("network has no retry", async () => {
  let n = 0;
  const r = await createRoleCatalogApi(async () => {
    n++;
    throw Error("SECRET");
  }).roles();
  assert.equal(r.kind, "network");
  assert.equal(n, 1);
});

for (const [name, call] of [
  ["list", (a) => a.roles()],
  ["detail", (a) => a.detail(1)],
  ["permissions", (a) => a.permissions()],
  ["grants", (a) => a.grants(1)],
]) {
  for (const status of [401, 403, 503])
    for (const contentType of ["application/json", "application/problem+json"])
      test(
        "092 unreadable " + name + " " + status + " " + contentType,
        async () => {
          const seen = [];
          const api = createRoleCatalogApi(async (url, init) => {
            seen.push(init.method);
            const response = new Response("", {
              status,
              headers: { "Content-Type": contentType },
            });
            response.text = async () => {
              throw Error("PRIVATE-092-ERROR");
            };
            return response;
          });
          const r = await call(api);
          assert.equal(r.ok, false);
          assert.equal(r.kind, "http");
          assert.equal(r.status, status);
          assert.deepEqual(seen, ["GET"]);
          assert.ok(!JSON.stringify(r).includes("PRIVATE-092"));
        },
      );
  for (const failure of ["network", "successful-body", "malformed-success"])
    test(
      "092 " + name + " " + failure + " has no invented status",
      async () => {
        const api = createRoleCatalogApi(async () => {
          if (failure === "network") throw Error("PRIVATE-092");
          const response = new Response("invalid", {
            headers: { "Content-Type": "application/json" },
          });
          if (failure === "successful-body")
            response.text = async () => {
              throw Error("PRIVATE-092");
            };
          return response;
        });
        const r = await call(api);
        assert.equal(r.ok, false);
        assert.equal(r.status, undefined);
        assert.ok(!JSON.stringify(r).includes("PRIVATE-092"));
      },
    );
}

test("092 cancellation during failed body read wins over observed status", async () => {
  const abort = new AbortController();
  const api = createRoleCatalogApi(async () => {
    const response = new Response("", {
      status: 401,
      headers: { "Content-Type": "application/json" },
    });
    response.text = async () => {
      abort.abort();
      throw Error("PRIVATE-092");
    };
    return response;
  });
  const r = await api.roles(1, "", "", abort.signal);
  assert.equal(r.kind, "cancelled");
  assert.equal(r.status, undefined);
});
