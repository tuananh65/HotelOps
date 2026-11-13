import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
import { role, permission, detail, page } from "./role-catalog-fixtures.mjs";
import { session } from "./workday-fixtures.mjs";
const compiled = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `import React from'react';import{createRoot}from'react-dom/client';import{BrowserRouter}from'react-router-dom';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthStore}from'./src/auth/authStore';import{createAuthApi}from'./src/auth/authApi';import{AuthRouteHost}from'./src/auth/AuthRouteHost';const s=window.scenario,calls=[],waiters=[];let old=s.late,lateGrants=s.lateGrants;window.fetch=async(u,o)=>{calls.push({url:u,method:o.method,cache:o.cache,credentials:o.credentials});if(u==='/api/auth/session')return s.anonymous?Response.json({},{status:401}):Response.json({...${JSON.stringify(session())},mustChangePassword:s.restricted??false});const body=u.includes('/permissions/catalog')?${JSON.stringify(page([permission(), permission(2)]))}:u.endsWith('/permissions')?${JSON.stringify({ roleId: 1, grants: detail().grants })}:u.startsWith('/api/roles?')?${JSON.stringify(page([role()]))}:${JSON.stringify(detail())};if(lateGrants&&u.endsWith('/permissions')){lateGrants=false;return new Promise(resolve=>waiters.push(()=>resolve(Response.json({...body,grants:body.grants.map(g=>({...g,permission:{...g.permission,permissionName:'OLD PRIVATE'}}))}))));}if(s.empty&&u.startsWith('/api/roles?'))return Response.json({items:[],page:1,pageSize:25,total:0});if(s.status){const r=Response.json({detail:'SECRET'},{status:s.status});if(s.unreadable)r.text=async()=>{throw Error('SECRET');};return r;}if(old){old=false;return new Promise(resolve=>waiters.push(()=>resolve(Response.json({...body,items:[{...${JSON.stringify(role())},roleName:'OLD PRIVATE'}]}))));}return Response.json(body);};const store=createAuthStore(createAuthApi(window.fetch));createRoot(document.getElementById('root')).render(<BrowserRouter><AuthProvider store={store} fallback={<AuthRouteHost/>}><AuthRouteHost/></AuthProvider></BrowserRouter>);window.fixture={calls,finish:()=>waiters.splice(0).forEach(f=>f()),replace:()=>store.revalidate()};`,
  },
  bundle: true,
  write: false,
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"production"' },
});
let browser;
before(async () => {
  const executablePath = [
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    "C:/Program Files/Google/Chrome/Application/chrome.exe",
  ].find(existsSync);
  browser = await chromium.launch({
    headless: true,
    ...(executablePath ? { executablePath } : {}),
  });
});
after(async () => browser?.close());
async function run(s, fn, path = "/roles") {
  const p = await browser.newPage();
  p.setDefaultTimeout(5000);
  const errors = [];
  p.on("pageerror", (e) => errors.push(e.message));
  await p.route("**/*", (r) =>
    r.request().resourceType() === "document"
      ? r.fulfill({ contentType: "text/html", body: '<div id="root"></div>' })
      : r.abort(),
  );
  try {
    await p.goto("https://offline.test" + path);
    await p.evaluate((s) => {
      window.scenario = s;
      for (const k of ["localStorage", "sessionStorage"])
        Object.defineProperty(window, k, {
          get() {
            throw Error("storage forbidden");
          },
        });
    }, s);
    await p.addScriptTag({ content: compiled.outputFiles[0].text });
    await fn(p);
    assert.deepEqual(errors, []);
    assert.ok(!(await p.locator("body").innerText()).includes("SECRET"));
    for (const c of await p.evaluate(() => window.fixture.calls)) {
      assert.equal(c.method, "GET");
      assert.equal(c.credentials, "include");
      assert.equal(c.cache, "no-store");
    }
  } finally {
    await p.close();
  }
}
test("real list then detail then permission matrix", () =>
  run({}, async (p) => {
    await p.getByRole("link", { name: "ROLE_1 — Role 1" }).click();
    await p.getByRole("heading", { name: "ROLE_1 — Role 1" }).waitFor();
    await p.getByText("ROOM_VIEW_1: BRANCH", { exact: true }).waitFor();
    await p
      .getByRole("link", { name: "Permission matrix", exact: true })
      .click();
    await p
      .getByText("ROOM_VIEW_1 — View 1: Granted (BRANCH)", { exact: true })
      .waitFor();
    await p
      .getByText("ROOM_VIEW_2 — View 2: Not Granted", { exact: true })
      .waitFor();
    assert.equal(
      await p.getByRole("button", { name: /Save|Edit|Deactivate/ }).count(),
      0,
    );
  }));
test("empty directory", () =>
  run({ empty: true }, async (p) => {
    await p.getByText("No roles found.").waitFor();
  }));
for (const status of [401, 403, 404, 503])
  test("safe error " + status, () =>
    run({ status }, async (p) => {
      await p.getByRole("alert").waitFor();
      assert.equal(
        await p.getByRole("link", { name: "ROLE_1 — Role 1" }).count(),
        0,
      );
      assert.match(
        await p.getByRole("alert").innerText(),
        status === 401
          ? /Session invalid/
          : status === 403
            ? /Access denied/
            : status === 404
              ? /not found/
              : /unavailable/,
      );
    }),
  );
for (const restricted of [false, true])
  test("anonymous/restricted no catalog " + restricted, () =>
    run(restricted ? { restricted: true } : { anonymous: true }, async (p) => {
      await p.waitForURL(restricted ? "**/change-password" : "**/login");
      assert.equal(
        (await p.evaluate(() => window.fixture.calls)).filter((c) =>
          c.url.includes("/api/roles"),
        ).length,
        0,
      );
    }),
  );
test("session replacement ignores late catalog", () =>
  run({ late: true }, async (p) => {
    await p.getByText("Loading catalog...").waitFor();
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.url.includes("/api/roles")),
    );
    await p.evaluate(() => window.fixture.replace());
    await p.getByRole("link", { name: "ROLE_1 — Role 1" }).waitFor();
    await p.evaluate(() => window.fixture.finish());
    assert.equal(await p.getByText(/OLD PRIVATE/).count(), 0);
  }));
test("permission catalog independent of role detail", () =>
  run(
    {},
    async (p) => {
      await p
        .getByText("ROOM_VIEW_1 — View 1; room/view", { exact: true })
        .waitFor();
      assert.equal(
        (await p.evaluate(() => window.fixture.calls)).filter((c) =>
          c.url.startsWith("/api/roles/"),
        ).length,
        0,
      );
    },
    "/permissions",
  ));
test("filter replacement hides stale role results", () =>
  run({ late: true }, async (p) => {
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.url.startsWith("/api/roles?")),
    );
    await p.getByLabel("Search roles").fill("Role");
    await p.getByRole("button", { name: "Search", exact: true }).click();
    await p.getByRole("link", { name: "ROLE_1 — Role 1" }).waitFor();
    await p.evaluate(() => window.fixture.finish());
    assert.equal(await p.getByText(/OLD PRIVATE/).count(), 0);
  }));

for (const path of [
  "/roles",
  "/roles/1",
  "/permissions",
  "/roles/1/permissions",
])
  for (const status of [401, 403, 503])
    test("092 unreadable " + status + " " + path, () =>
      run(
        { status, unreadable: true },
        async (p) => {
          const message =
            status === 401
              ? "Session invalid. Revalidate before continuing."
              : status === 403
                ? "Access denied by server."
                : "Catalog unavailable.";
          await p.getByText(message, { exact: true }).first().waitFor();
          assert.equal(
            (await p.evaluate(() => window.fixture.calls)).filter(
              (c) => c.url === "/api/auth/session",
            ).length,
            1,
          );
          assert.equal(
            (await p
              .getByRole("button", { name: "Revalidate session" })
              .count()) > 0,
            status === 401,
          );
          if (status === 401) {
            await p
              .getByRole("button", { name: "Revalidate session" })
              .first()
              .click();
            await p.waitForFunction(
              () =>
                window.fixture.calls.filter(
                  (c) => c.url === "/api/auth/session",
                ).length === 2,
            );
          }
        },
        path,
      ),
    );

test("092 session replacement never restores old grant matrix", () =>
  run(
    { lateGrants: true },
    async (p) => {
      await p.waitForFunction(() =>
        window.fixture.calls.some((c) => c.url === "/api/roles/1/permissions"),
      );
      await p.evaluate(() => window.fixture.replace());
      await p.waitForFunction(
        () =>
          window.fixture.calls.filter(
            (c) => c.url === "/api/roles/1/permissions",
          ).length === 2,
      );
      await p.evaluate(() => window.fixture.finish());
      await p.evaluate(
        () =>
          new Promise((r) =>
            requestAnimationFrame(() => requestAnimationFrame(r)),
          ),
      );
      assert.equal(await p.getByText(/OLD PRIVATE/).count(), 0);
    },
    "/roles/1/permissions",
  ));
