import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
const tag = '"AAAAAAAAAAE="',
  secret = "SYNTHETIC-CREATE-CSRF";
const branch = {
  id: 42,
  code: "B-NEW",
  name: "New branch",
  address: "New address",
  phone: null,
  email: null,
  timezone: "Asia/Ho_Chi_Minh",
  isActive: true,
};
const session = (id = "7", restricted = false) => ({
  user: { id, username: "fixture", name: "Fixture" },
  expiresAt: "2026-12-01T12:00:00Z",
  absoluteExpiresAt: "2026-12-01T20:00:00Z",
  mustChangePassword: restricted,
  csrfToken: secret,
});
const bundle = await build({
  stdin: {
    loader: "tsx",
    resolveDir: process.cwd(),
    contents: `
import React,{StrictMode}from'react';import{createRoot}from'react-dom/client';import{BrowserRouter}from'react-router-dom';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthApi}from'./src/auth/authApi';import{createAuthStore}from'./src/auth/authStore';import{HotelRouteHost}from'./src/hotel/HotelRouteHost';
const calls=[],waiting=new Map(),scenario=window.scenario;const response=r=>new Response(JSON.stringify(r.body),{status:r.status??200,headers:{'Content-Type':'application/json',ETag:r.etag??${JSON.stringify(tag)}}});
window.fetch=async(url,init)=>{const call={url,method:init.method,credentials:init.credentials,cache:init.cache,headers:Object.fromEntries(init.headers),body:init.body,aborted:false};calls.push(call);init.signal?.addEventListener('abort',()=>call.aborted=true,{once:true});let r;if(url==='/api/auth/session')r=scenario.auth??{body:${JSON.stringify(session())}};else if(url==='/api/auth/csrf')r=scenario.csrf??{body:{csrfToken:${JSON.stringify(secret)},headerName:'X-CSRF-TOKEN'}};else if(init.method==='POST')r=scenario.create??{status:201,body:${JSON.stringify(branch)}};else if(url==='/api/branches/42/edit')r={body:${JSON.stringify(branch)}};else {r={status:503,body:{}}}if(r.network)throw Error(${JSON.stringify(secret)});if(r.deferred)return new Promise(resolve=>waiting.set(r.deferred,resolve));return response(r)};
const store=createAuthStore(createAuthApi(window.fetch));const app=<BrowserRouter><HotelRouteHost/></BrowserRouter>;createRoot(document.getElementById('root')).render(<StrictMode><AuthProvider store={store} fallback={app}>{app}</AuthProvider></StrictMode>);window.fixture={calls,scenario,revalidate:()=>store.revalidate(),finish:(key,r)=>waiting.get(key)(response(r)),navigate(path){history.pushState(null,'',path);dispatchEvent(new PopStateEvent('popstate'))}};
`,
  },
  bundle: true,
  write: false,
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"development"', "import.meta.env": "{}" },
});
let browser;
before(async () => {
  const executablePath =
    process.env.HOTELOPS_TEST_BROWSER ||
    [
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
      "C:/Program Files/Google/Chrome/Application/chrome.exe",
    ].find(existsSync);
  browser = await chromium.launch({
    headless: true,
    ...(executablePath ? { executablePath } : {}),
  });
});
after(async () => browser?.close());
async function pageTest(scenario, run, path = "/branches/new") {
  const p = await browser.newPage(),
    errors = [],
    network = [],
    logs = [];
  p.setDefaultTimeout(6000);
  p.on("pageerror", (e) => errors.push(e.message));
  p.on("console", (m) => logs.push(m.text()));
  await p.route("**/*", (r) =>
    r.request().resourceType() === "document"
      ? r.fulfill({ contentType: "text/html", body: '<div id="root"></div>' })
      : (network.push(r.request().url()), r.abort()),
  );
  try {
    await p.goto("http://offline.test" + path);
    await p.evaluate((s) => {
      window.scenario = s;
      for (const k of ["localStorage", "sessionStorage"])
        Object.defineProperty(window, k, {
          get() {
            throw Error("Storage forbidden");
          },
        });
    }, scenario);
    await p.addScriptTag({ content: bundle.outputFiles[0].text });
    await run(p);
    assert.deepEqual(errors, []);
    assert.deepEqual(network, []);
    assert.ok(!logs.some((s) => s.includes(secret)));
    assert.ok(!(await p.locator("body").innerText()).includes(secret));
    for (const c of await p.evaluate(() => window.fixture.calls)) {
      assert.equal(c.credentials, "include");
      assert.equal(c.cache, "no-store");
      assert.equal(c.headers.authorization, undefined);
    }
  } finally {
    await p.close();
  }
}
const posts = (p) =>
  p.evaluate(() => window.fixture.calls.filter((c) => c.method === "POST"));
const settle = (p) =>
  p.evaluate(
    () =>
      new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))),
  );
async function review(p) {
  for (const [name, value] of [
    ["code", " b-new "],
    ["name", "New branch"],
    ["address", "New address"],
  ])
    await p.getByRole("textbox", { name, exact: true }).fill(value);
  await p.getByRole("button", { name: "Review create", exact: true }).click();
  await p.getByRole("region", { name: "Review create branch" }).waitFor();
}
test("required validation, normalized review, edit invalidation and double confirm one POST", () =>
  pageTest({}, async (p) => {
    await p.getByRole("button", { name: "Review create" }).click();
    await p.getByRole("alert").waitFor();
    assert.equal((await posts(p)).length, 0);
    await review(p);
    assert.equal((await posts(p)).length, 0);
    assert.ok(
      (
        await p
          .getByRole("region", { name: "Review create branch" })
          .innerText()
      ).includes("B-NEW"),
    );
    await p.getByRole("textbox", { name: "name", exact: true }).fill("changed");
    assert.equal(
      await p.getByRole("button", { name: "Confirm create" }).count(),
      0,
    );
    await review(p);
    await p.getByRole("button", { name: "Confirm create" }).evaluate((b) => {
      b.click();
      b.click();
    });
    await p.getByText("Branch ID: 42", { exact: true }).waitFor();
    assert.equal((await posts(p)).length, 1);
    assert.deepEqual(Object.keys(JSON.parse((await posts(p))[0].body)).sort(), [
      "address",
      "code",
      "email",
      "name",
      "phone",
    ]);
    assert.ok(
      (
        await p.getByRole("region", { name: "Created branch" }).innerText()
      ).includes("Asia/Ho_Chi_Minh"),
    );
  }));
for (const [status, message] of [
  [409, "Branch code already exists."],
  [400, "Server rejected the branch fields."],
  [401, "Session invalid."],
  [403, "Server denied Create Branch."],
])
  test(`server ${status} sanitized, no false confirmation`, () =>
    pageTest(
      { create: { status, body: { title: secret, detail: secret } } },
      async (p) => {
        await review(p);
        await p.getByRole("button", { name: "Confirm create" }).click();
        await p.getByText(message, { exact: false }).waitFor();
        assert.equal(
          await p.getByRole("region", { name: "Created branch" }).count(),
          0,
        );
        assert.equal((await posts(p)).length, 1);
        if (status === 401 || status === 403)
          assert.equal(
            await p
              .getByRole("textbox", { name: "name", exact: true })
              .inputValue(),
            "",
          );
      },
    ));
for (const create of [
  { network: true },
  { status: 503, body: {} },
  { status: 500, body: {} },
  { status: 201, body: { ...branch, id: 0 } },
])
  test("unknown outcome locks replay including after explicit session revalidation", () =>
    pageTest({ create }, async (p) => {
      await review(p);
      await p.getByRole("button", { name: "Confirm create" }).click();
      await p.getByText("Create outcome unknown.", { exact: false }).waitFor();
      assert.equal(
        await p.getByRole("button", { name: "Review create" }).isDisabled(),
        true,
      );
      await p
        .getByRole("button", { name: "Check session", exact: true })
        .click();
      await p.getByRole("button", { name: "Review create" }).waitFor();
      assert.equal(
        await p.getByRole("button", { name: "Review create" }).isDisabled(),
        true,
      );
      assert.equal((await posts(p)).length, 1);
      assert.equal(
        await p.getByRole("region", { name: "Created branch" }).count(),
        0,
      );
    }));
for (const auth of [{ status: 401, body: {} }, { body: session("7", true) }])
  test("anonymous/restricted never bootstrap create or POST", () =>
    pageTest({ auth }, async (p) => {
      await p
        .getByText("A confirmed unrestricted session is required.")
        .waitFor();
      assert.equal((await posts(p)).length, 0);
      assert.equal(
        (await p.evaluate(() => window.fixture.calls)).filter(
          (c) => c.url === "/api/auth/csrf",
        ).length,
        0,
      );
    }));
test("session replacement during POST hides prior identity result", () =>
  pageTest({ create: { deferred: "old" } }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm create" }).click();
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.method === "POST"),
    );
    await p.evaluate((s) => {
      window.fixture.scenario.auth = { body: s };
      return window.fixture.revalidate();
    }, session("8"));
    await p.getByRole("button", { name: "Review create" }).waitFor();
    await p.evaluate(
      (b) => window.fixture.finish("old", { status: 201, body: b }),
      branch,
    );
    await settle(p);
    assert.equal(
      await p.getByRole("region", { name: "Created branch" }).count(),
      0,
    );
    assert.equal(
      await p.getByRole("textbox", { name: "name", exact: true }).inputValue(),
      "",
    );
    assert.equal((await posts(p)).length, 1);
  }));
test("identity replacement during CSRF never dispatches POST", () =>
  pageTest({ csrf: { deferred: "csrf" } }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm create" }).click();
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.url === "/api/auth/csrf"),
    );
    await p.evaluate((s) => {
      window.fixture.scenario.auth = { body: s };
      return window.fixture.revalidate();
    }, session("8"));
    await p.evaluate(
      (token) =>
        window.fixture.finish("csrf", {
          body: { csrfToken: token, headerName: "X-CSRF-TOKEN" },
        }),
      secret,
    );
    await settle(p);
    assert.equal((await posts(p)).length, 0);
  }));
test("Hotel Structure navigation reaches Create; Update and FE12 OFF routes preserved", () =>
  pageTest(
    {},
    async (p) => {
      await p.getByRole("link", { name: "Create Branch", exact: true }).click();
      await p.waitForURL("**/branches/new");
      await review(p);
      await p.getByRole("button", { name: "Confirm create" }).click();
      await p
        .getByRole("link", { name: "View branch information", exact: true })
        .click();
      await p.waitForURL("**/branches/42/edit");
      await p.getByRole("textbox", { name: "name", exact: true }).waitFor();
      assert.equal(
        await p
          .getByRole("textbox", { name: "name", exact: true })
          .inputValue(),
        "New branch",
      );
      await p.evaluate(() => window.fixture.navigate("/workday"));
      await p.getByRole("heading", { name: "HotelOps workspace" }).waitFor();
      assert.equal(
        (
          await p.evaluate(() =>
            window.fixture.calls.filter((c) => c.url.includes("/workdays/")),
          )
        ).length,
        0,
      );
      assert.equal((await posts(p)).length, 1);
    },
    "/hotel",
  ));
