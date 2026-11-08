import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
const tag =
    '"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"',
  next = '"BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"',
  secret = "SYNTHETIC-BRANCH-PRIVATE";
const branch = (id) => ({
  id,
  code: "B" + id,
  name: "Branch " + id,
  address: "Address " + id,
  phone: null,
  email: null,
  timezone: "Asia/Ho_Chi_Minh",
  isActive: true,
});
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
import React,{StrictMode,useState} from 'react';import{createRoot}from'react-dom/client';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthApi}from'./src/auth/authApi';import{createAuthStore}from'./src/auth/authStore';import{BrowserRouter}from'react-router-dom';import{HotelRouteHost}from'./src/hotel/HotelRouteHost';
const calls=[],waiting=new Map(),scenario=window.scenario;const make=r=>new Response(r.status===204?null:JSON.stringify(r.body),{status:r.status??200,headers:{'Content-Type':'application/json',ETag:r.etag??${JSON.stringify(tag)}}});
window.fetch=async(url,init)=>{const c={url,method:init.method,credentials:init.credentials,cache:init.cache,body:init.body,headers:Object.fromEntries(init.headers),aborted:false};calls.push(c);init.signal?.addEventListener('abort',()=>c.aborted=true,{once:true});let r;if(url==='/api/auth/session')r=scenario.auth??{body:${JSON.stringify(session())}};else if(url==='/api/auth/csrf')r={body:{csrfToken:${JSON.stringify(secret)},headerName:'X-CSRF-TOKEN'}};else if(init.method==='GET'){const id=Number(url.split('/')[3]);r=scenario.read??{body:{...${JSON.stringify(branch(1))},id,code:'B'+id,name:'Branch '+id,address:'Address '+id}}}else r=scenario.write??{body:{...${JSON.stringify(branch(1))},...JSON.parse(init.body)},etag:${JSON.stringify(next)}};if(r.network)throw Error(${JSON.stringify(secret)});if(r.deferred)return new Promise(resolve=>waiting.set(r.deferred,resolve));return make(r)};
const store=createAuthStore(createAuthApi(window.fetch));function App(){window.selectBranch=id=>{history.pushState(null,'','/branches/'+id+'/edit');dispatchEvent(new PopStateEvent('popstate'))};return <HotelRouteHost/>};const app=<BrowserRouter><App/></BrowserRouter>;createRoot(document.getElementById('root')).render(<StrictMode><AuthProvider store={store} fallback={app}>{app}</AuthProvider></StrictMode>);window.fixture={calls,resolve:(name,r)=>waiting.get(name)(make(r)),scenario,revalidate:()=>store.revalidate()};
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
async function pageTest(scenario, run) {
  const p = await browser.newPage(),
    errors = [],
    network = [];
  p.setDefaultTimeout(6000);
  p.on("pageerror", (e) => errors.push(e.message));
  await p.route("**/*", (r) =>
    r.request().resourceType() === "document"
      ? r.fulfill({ contentType: "text/html", body: '<div id="root"></div>' })
      : (network.push(r.request().url()), r.abort()),
  );
  try {
    await p.goto("http://offline.test/branches/1/edit");
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
async function review(p) {
  await p.getByRole("textbox", { name: "name", exact: true }).fill("Updated");
  await p
    .getByRole("textbox", { name: "reason", exact: true })
    .fill("Correct contact information");
  await p.getByRole("button", { name: "Review update", exact: true }).click();
  await p.getByRole("region", { name: "Review branch update" }).waitFor();
}
const writes = (p) =>
  p.evaluate(() => window.fixture.calls.filter((c) => c.method === "PUT"));
const settle = (p) =>
  p.evaluate(
    () =>
      new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))),
  );
test("render, required validation, review-only, edit invalidation, double confirm once", () =>
  pageTest({}, async (p) => {
    await p.getByRole("textbox", { name: "name", exact: true }).waitFor();
    await p.getByRole("button", { name: "Review update" }).click();
    await p.getByRole("alert").waitFor();
    assert.equal((await writes(p)).length, 0);
    await review(p);
    assert.equal((await writes(p)).length, 0);
    await p
      .getByRole("textbox", { name: "address", exact: true })
      .fill("New address");
    assert.equal(
      await p.getByRole("button", { name: "Confirm update" }).count(),
      0,
    );
    await review(p);
    await p.getByRole("button", { name: "Confirm update" }).evaluate((b) => {
      b.click();
      b.click();
    });
    await p.getByText("Branch information updated.").waitFor();
    assert.equal((await writes(p)).length, 1);
    assert.equal((await writes(p))[0].headers["if-match"], tag);
  }));
test("412 requires explicit fresh GET and new review, no replay", () =>
  pageTest({ write: { status: 412, body: { detail: secret } } }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm update" }).click();
    await p
      .getByText("Version changed or unavailable.", { exact: false })
      .waitFor();
    assert.equal(
      await p.getByRole("textbox", { name: "name", exact: true }).count(),
      0,
    );
    assert.equal((await writes(p)).length, 1);
    await p.getByRole("button", { name: "Read branch" }).click();
    await p.getByRole("textbox", { name: "name", exact: true }).waitFor();
    assert.equal(
      await p.getByRole("button", { name: "Confirm update" }).count(),
      0,
    );
    assert.equal((await writes(p)).length, 1);
  }));
test("unknown result remains locked after successful read", () =>
  pageTest({ write: { network: true } }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm update" }).click();
    await p.getByText("Update outcome unknown.", { exact: false }).waitFor();
    await p.getByRole("button", { name: "Read branch" }).click();
    await p.getByRole("textbox", { name: "name", exact: true }).waitFor();
    assert.equal(
      await p.getByRole("button", { name: "Review update" }).isDisabled(),
      true,
    );
    assert.equal((await writes(p)).length, 1);
  }));
for (const status of [401, 403, 404, 503])
  test(`read ${status} no fake form`, () =>
    pageTest({ read: { status, body: { detail: secret } } }, async (p) => {
      await p.getByRole("alert").waitFor();
      assert.equal(
        await p.getByRole("textbox", { name: "name", exact: true }).count(),
        0,
      );
      assert.equal((await writes(p)).length, 0);
    }));
for (const auth of [{ status: 401, body: {} }, { body: session("7", true) }])
  test("anonymous/restricted cannot load branch", () =>
    pageTest({ auth }, async (p) => {
      await p
        .getByText("A confirmed unrestricted session is required.")
        .waitFor();
      assert.equal(
        (
          await p.evaluate(() =>
            window.fixture.calls.filter((c) =>
              c.url.startsWith("/api/branches"),
            ),
          )
        ).length,
        0,
      );
    }));
test("branch change during write prevents stale success from publishing", () =>
  pageTest({ write: { deferred: "old" } }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm update" }).click();
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.method === "PUT"),
    );
    await p.evaluate(() => window.selectBranch(2));
    await p.getByRole("textbox", { name: "name", exact: true }).waitFor();
    await p.waitForFunction(
      () =>
        document.querySelector("input[aria-label=name]")?.value === "Branch 2",
    );
    await p.evaluate((r) => window.fixture.resolve("old", r), {
      body: { ...branch(1), name: "Old success" },
      etag: next,
    });
    await settle(p);
    assert.equal(
      await p.getByRole("textbox", { name: "name", exact: true }).inputValue(),
      "Branch 2",
    );
    assert.equal(await p.getByText("Branch information updated.").count(), 0);
    assert.equal(
      await p.getByRole("button", { name: "Review update" }).isDisabled(),
      true,
    );
  }));
test("session replacement clears review and late read from former identity", () =>
  pageTest({ read: { deferred: "oldRead" } }, async (p) => {
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.url.includes("/edit")),
    );
    await p.evaluate(
      ({ s, b }) => {
        window.fixture.scenario.auth = { body: s };
        window.fixture.scenario.read = { body: b };
        return window.fixture.revalidate();
      },
      { s: session("8"), b: { ...branch(1), name: "New identity branch" } },
    );
    await p.waitForFunction(
      () =>
        document.querySelector("input[aria-label=name]")?.value ===
        "New identity branch",
    );
    await p.evaluate((r) => window.fixture.resolve("oldRead", r), {
      body: branch(1),
    });
    await settle(p);
    assert.equal(
      await p.getByRole("textbox", { name: "name", exact: true }).inputValue(),
      "New identity branch",
    );
    assert.equal((await writes(p)).length, 0);
  }));
for (const status of [401, 403])
  test(`update ${status} clears protected branch fields`, () =>
    pageTest({ write: { status, body: { detail: secret } } }, async (p) => {
      await review(p);
      await p.getByRole("button", { name: "Confirm update" }).click();
      await p
        .getByText("Branch data cleared after server rejection.", {
          exact: false,
        })
        .waitFor();
      assert.equal(
        await p.getByRole("textbox", { name: "name", exact: true }).count(),
        0,
      );
      assert.equal(
        await p.getByRole("button", { name: "Confirm update" }).count(),
        0,
      );
      assert.equal((await writes(p)).length, 1);
    }));
test("invalid branch URL does not request branch data", () =>
  pageTest({}, async (p) => {
    await p.getByRole("textbox", { name: "name", exact: true }).waitFor();
    const before = await p.evaluate(() => window.fixture.calls.length);
    await p.evaluate(() => window.selectBranch("not-a-number"));
    await p.getByText("Select a positive safe branch ID.").waitFor();
    assert.equal(await p.evaluate(() => window.fixture.calls.length), before);
  }));
test("FE02 structure navigation reaches editor; FE01 Profile remains available", () =>
  pageTest({}, async (p) => {
    await p.getByRole("textbox", { name: "name", exact: true }).waitFor();
    await p.evaluate(() => {
      history.pushState(null, "", "/hotel?branchId=1");
      dispatchEvent(new PopStateEvent("popstate"));
    });
    await p
      .getByRole("link", { name: "Update branch information", exact: true })
      .click();
    await p.waitForURL("**/branches/1/edit");
    await p.getByRole("textbox", { name: "name", exact: true }).waitFor();
    await p.evaluate(() => {
      history.pushState(null, "", "/profile");
      dispatchEvent(new PopStateEvent("popstate"));
    });
    await p
      .getByRole("heading", { name: /profile|hồ sơ/i })
      .first()
      .waitFor();
    assert.equal((await writes(p)).length, 0);
  }));
