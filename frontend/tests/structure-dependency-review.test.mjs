import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
import { session, secret } from "./independent-fixtures.mjs";
const bundle = await build({
  stdin: {
    loader: "tsx",
    resolveDir: process.cwd(),
    contents: `
import React from'react';import{createRoot}from'react-dom/client';import{BrowserRouter}from'react-router-dom';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthApi}from'./src/auth/authApi';import{createAuthStore}from'./src/auth/authStore';import{HotelRouteHost}from'./src/hotel/HotelRouteHost';
const s=window.scenario,calls=[],waiting=new Map(),make=r=>new Response(JSON.stringify(r.body),{status:r.status??200,headers:{'Content-Type':'application/json',ETag:'"'+'A'.repeat(64)+'"'}});
window.fetch=async(url,init)=>{const c={url,method:init.method,credentials:init.credentials,cache:init.cache,aborted:false};calls.push(c);init.signal?.addEventListener('abort',()=>c.aborted=true);let r;if(url==='/api/auth/session')r=s.auth??{body:${JSON.stringify(session())}};else if(url.endsWith('/deactivation-dependencies')){const parts=url.split('/'),kind=parts[4]==='floors'?'floor':'area';r=s.read??{body:{record:{kind,id:Number(parts[5]),branchId:Number(parts[3]),code:'LOCATION',name:'Protected '+kind+' '+parts[3],description:null,floorId:kind==='area'?11:null,floorNumber:kind==='floor'?1:null,isActive:true},dependencies:s.empty?[]:[{kind:'ACTIVE_ROOM',count:2,ids:[999]}],writerCoordinationRequired:true}};}else if(url==='/api/branches/42/floors/11')r={body:{kind:'floor',id:11,branchId:42,code:'LOCATION',name:'Protected floor 42',description:null,floorId:null,floorNumber:1,isActive:true}};else throw Error('Unexpected request');if(r.deferred)return new Promise(resolve=>waiting.set(r.deferred,resolve));return make(r)};
const store=createAuthStore(createAuthApi(window.fetch)),app=<BrowserRouter><HotelRouteHost/></BrowserRouter>;createRoot(document.getElementById('root')).render(<AuthProvider store={store} fallback={app}>{app}</AuthProvider>);window.fixture={calls,scenario:s,revalidate:()=>store.revalidate(),finish:(key,r)=>waiting.get(key)(make(r)),navigate(path){history.pushState(null,'',path);dispatchEvent(new PopStateEvent('popstate'))}};
`,
  },
  bundle: true,
  write: false,
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"production"', "import.meta.env": "{}" },
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
async function run(s, fn, path = "/floors/11/deactivation-review?branchId=42") {
  const p = await browser.newPage(),
    errors = [],
    logs = [],
    network = [];
  p.setDefaultTimeout(5000);
  p.on("pageerror", (e) => errors.push(e.message));
  p.on("console", (e) => logs.push(e.text()));
  await p.route("**/*", (r) =>
    r.request().resourceType() === "document"
      ? r.fulfill({ contentType: "text/html", body: '<div id="root"></div>' })
      : (network.push(r.request().url()), r.abort()),
  );
  try {
    await p.goto("https://offline.test" + path);
    await p.evaluate((s) => {
      window.scenario = s;
      for (const k of ["localStorage", "sessionStorage"])
        Object.defineProperty(window, k, {
          get() {
            throw Error("Storage forbidden");
          },
        });
    }, s);
    await p.addScriptTag({ content: bundle.outputFiles[0].text });
    await fn(p);
    assert.deepEqual(errors, []);
    assert.deepEqual(network, []);
    assert.ok(!logs.join("").includes(secret));
    assert.ok(!(await p.locator("body").innerText()).includes(secret));
    for (const c of await p.evaluate(() => window.fixture.calls)) {
      assert.equal(c.method, "GET");
      assert.equal(c.credentials, "include");
      assert.equal(c.cache, "no-store");
    }
  } finally {
    await p.close();
  }
}
const reads = (p) =>
  p.evaluate(() =>
    window.fixture.calls.filter((c) =>
      c.url.endsWith("/deactivation-dependencies"),
    ),
  );
for (const kind of ["floor", "area"])
  test(kind + " route reads counts without mutation", () =>
    run(
      {},
      async (p) => {
        await p
          .getByRole("heading", { name: "Protected " + kind + " 42" })
          .waitFor();
        await p.getByText("Active rooms: 2", { exact: true }).waitFor();
        assert.equal((await reads(p)).length, 1);
        assert.equal(
          await p.getByRole("button", { name: /Deactivate|Confirm/ }).count(),
          0,
        );
        assert.ok(!(await p.locator("body").innerText()).includes("999"));
      },
      `/${kind}s/${kind === "floor" ? 11 : 12}/deactivation-review?branchId=42`,
    ),
  );
test("empty dependencies do not approve mutation", () =>
  run({ empty: true }, async (p) => {
    await p.getByText("No dependencies were reported by this read.").waitFor();
    await p
      .getByText(
        "Dependencies may change after this read. An empty list does not approve deactivation.",
      )
      .waitFor();
  }));
for (const status of [401, 403, 404, 503])
  test("sanitized error " + status, () =>
    run(
      { read: { status, body: { detail: secret, traceId: secret } } },
      async (p) => {
        await p.getByRole("alert").waitFor();
        assert.equal(
          await p
            .getByRole("region", { name: "Location dependencies" })
            .count(),
          0,
        );
        assert.equal((await reads(p)).length, 1);
        if (status === 401) {
          assert.ok(
            await p
              .getByRole("button", { name: "Refresh dependencies" })
              .isDisabled(),
          );
          await p.getByRole("button", { name: "Check session" }).waitFor();
        }
      },
    ),
  );
for (const path of [
  "/floors/0/deactivation-review?branchId=42",
  "/areas/12/deactivation-review?branchId=bad",
])
  test("invalid IDs " + path, () =>
    run(
      {},
      async (p) => {
        await p.getByRole("alert").waitFor();
        assert.equal((await reads(p)).length, 0);
      },
      path,
    ),
  );
for (const auth of [
  { status: 401, body: {} },
  { body: { ...session(), mustChangePassword: true } },
])
  test("anonymous/restricted " + (auth.status ?? "restricted"), () =>
    run({ auth }, async (p) => {
      await p.waitForURL(/\/(login|change-password)/);
      assert.equal((await reads(p)).length, 0);
    }),
  );
test("branch replacement rejects late response", () =>
  run({ read: { deferred: "old" } }, async (p) => {
    await p.getByText("Loading dependencies...").waitFor();
    await p.evaluate(() => {
      delete window.fixture.scenario.read;
      window.fixture.navigate("/floors/11/deactivation-review?branchId=43");
    });
    await p.getByRole("heading", { name: "Protected floor 43" }).waitFor();
    await p.evaluate(() => window.fixture.finish("old", { body: {} }));
    await p.waitForTimeout(50);
    assert.equal(
      await p.getByRole("heading", { name: "Protected floor 42" }).count(),
      0,
    );
    assert.equal((await reads(p))[0].aborted, true);
  }));
test("session invalidation clears data and aborts read", () =>
  run({ read: { deferred: "old" } }, async (p) => {
    await p.getByText("Loading dependencies...").waitFor();
    await p.evaluate(() => {
      window.fixture.scenario.auth = { status: 401, body: {} };
      void window.fixture.revalidate();
    });
    await p.waitForURL(/\/login/);
    await p.evaluate(() => window.fixture.finish("old", { body: {} }));
    assert.equal(
      await p.getByRole("region", { name: "Location dependencies" }).count(),
      0,
    );
    assert.equal((await reads(p))[0].aborted, true);
  }));
test("loaded snapshot disappears immediately during session replacement", () =>
  run({}, async (p) => {
    await p.getByRole("heading", { name: "Protected floor 42" }).waitFor();
    await p.evaluate(() => {
      window.fixture.scenario.auth = { status: 401, body: {} };
      void window.fixture.revalidate();
    });
    await p.waitForURL(/\/login/);
    assert.equal(await p.getByText("Protected floor 42").count(), 0);
  }));
test("manual refresh discards snapshot before new denial", () =>
  run({}, async (p) => {
    await p.getByRole("heading", { name: "Protected floor 42" }).waitFor();
    await p.evaluate(
      () =>
        (window.fixture.scenario.read = {
          status: 403,
          body: { detail: "SECRET" },
        }),
    );
    await p.getByRole("button", { name: "Refresh dependencies" }).click();
    await p.getByRole("alert").waitFor();
    assert.equal(
      await p.getByRole("region", { name: "Location dependencies" }).count(),
      0,
    );
    assert.equal((await reads(p)).length, 2);
  }));
test("review component import graph excludes candidate mutation panel/client", async () => {
  const result = await build({
    entryPoints: ["src/structure-deactivation/StructureDependencyReview.tsx"],
    bundle: true,
    write: false,
    metafile: true,
    jsx: "automatic",
  });
  for (const input of Object.keys(result.metafile.inputs))
    assert.ok(
      !/structureDeactivationApi|StructureDeactivationPanel/.test(input),
      input,
    );
});
test("floor detail links to server-authorized dependency review", () =>
  run(
    {},
    async (p) => {
      await p
        .getByRole("combobox", { name: "Action", exact: true })
        .selectOption("detail");
      await p
        .getByRole("textbox", { name: "Record ID", exact: true })
        .fill("11");
      await p
        .getByRole("button", { name: "Load from server", exact: true })
        .click();
      await p
        .getByRole("link", {
          name: "Review deactivation dependencies",
          exact: true,
        })
        .click();
      await p
        .getByRole("heading", {
          name: "Review floor dependencies",
          exact: true,
        })
        .waitFor();
      await p.getByText("Active rooms: 2", { exact: true }).waitFor();
      assert.equal((await reads(p)).length, 1);
    },
    "/floors?branchId=42",
  ));
