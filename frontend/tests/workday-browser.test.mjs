import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { readFileSync, existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
import { day, shift, session, secret } from "./workday-fixtures.mjs";
const bundle = await build({
  stdin: {
    loader: "tsx",
    resolveDir: process.cwd(),
    contents: `
import React,{StrictMode,useState} from 'react';import{createRoot}from'react-dom/client';import{BrowserRouter,Routes,Route}from'react-router-dom';
import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthStore}from'./src/auth/authStore';import{createAuthApi}from'./src/auth/authApi';
import{WorkdayReadPanel}from'./src/workday/WorkdayReadPanel';import{HotelRouteHost}from'./src/hotel/HotelRouteHost';
const calls=[],waiting=new Map();let sessions=[...(window.scenario.sessions??[{body:${JSON.stringify(session())}}])];
const respond=r=>new Response(r.status===204?null:JSON.stringify(r.body),{status:r.status??200,headers:{'Content-Type':'application/json'}});
window.fetch=async(url,init)=>{const call={url,method:init.method,credentials:init.credentials,cache:init.cache};calls.push(call);
init.signal?.addEventListener('abort',()=>{call.aborted=true},{once:true});
let r;if(url==='/api/auth/session')r=sessions.shift();else r=window.scenario.responses?.[url]??(url.endsWith('own-shifts')?{body:[${JSON.stringify(shift())}]}:{body:${JSON.stringify(day())}});
if(r?.deferred)return new Promise(resolve=>waiting.set(r.deferred,[...(waiting.get(r.deferred)??[]),resolve]));if(!r)throw Error('Missing response');return respond(r)};
const store=createAuthStore(createAuthApi(window.fetch));function App(){const[branch,setBranch]=useState(1);window.changeBranch=setBranch;return <Routes><Route path='/workday' element={<WorkdayReadPanel branchId={branch}/>}/><Route path='*' element={<HotelRouteHost/>}/></Routes>}
const host=<App/>;createRoot(document.getElementById('root')).render(<StrictMode><BrowserRouter><AuthProvider store={store} fallback={host}>{host}</AuthProvider></BrowserRouter></StrictMode>);
window.fixture={calls,finish:(key,r)=>waiting.get(key).shift()(respond(r)),revalidate:()=>store.revalidate(),navigate(path){history.pushState(null,'',path);dispatchEvent(new PopStateEvent('popstate'))}};
`,
  },
  bundle: true,
  write: false,
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"development"' },
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
async function withPage(scenario, run) {
  const p = await browser.newPage();
  const errors = [];
  p.on("pageerror", (e) => errors.push(e.message));
  await p.route("**/*", (r) =>
    r.request().resourceType() === "document"
      ? r.fulfill({ contentType: "text/html", body: '<div id="root"></div>' })
      : r.abort(),
  );
  try {
    await p.goto("http://offline.test/workday");
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
    assert.ok(!(await p.locator("body").innerText()).includes(secret));
    const calls = await p.evaluate(() => window.fixture.calls);
    for (const c of calls) {
      assert.equal(c.method, "GET");
      assert.equal(c.credentials, "include");
      assert.equal(c.cache, "no-store");
    }
  } finally {
    await p.close();
  }
}
const path = (branch, op) => `/api/branches/${branch}/workdays/${op}`;
const shiftsSection = (p) =>
  p.getByRole("region", { name: "Ca được phân công trong ngày", exact: true });
const currentSection = (p) =>
  p.getByRole("region", { name: "Workday hiện tại", exact: true });
const settle = (p) =>
  p.evaluate(
    () =>
      new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))),
  );

test("own-shifts 403 does not block current 200", () =>
  withPage(
    { responses: { [path(1, "own-shifts")]: { status: 403, body: {} } } },
    async (p) => {
      await shiftsSection(p).getByRole("alert").waitFor();
      await currentSection(p)
        .getByText("Workday 20,", { exact: false })
        .waitFor();
    },
  ));
test("current 403 does not hide authorized shifts", () =>
  withPage(
    { responses: { [path(1, "current")]: { status: 403, body: {} } } },
    async (p) => {
      await currentSection(p).getByRole("alert").waitFor();
      await shiftsSection(p).getByText("Ca 11:", { exact: false }).waitFor();
    },
  ));
test("both 403 expose no workday or shift data", () =>
  withPage(
    {
      responses: {
        [path(1, "current")]: { status: 403, body: {} },
        [path(1, "own-shifts")]: { status: 403, body: {} },
      },
    },
    async (p) => {
      await shiftsSection(p).getByRole("alert").waitFor();
      await currentSection(p).getByRole("alert").waitFor();
      assert.equal(await p.getByText("Ca 11:", { exact: false }).count(), 0);
      assert.equal(
        await p.getByText("Workday 20,", { exact: false }).count(),
        0,
      );
    },
  ));
test("authorized shifts remain visible with current 204", () =>
  withPage(
    { responses: { [path(1, "current")]: { status: 204 } } },
    async (p) => {
      await shiftsSection(p).getByText("Ca 11:", { exact: false }).waitFor();
      await currentSection(p).getByText("Không có workday đang làm.").waitFor();
    },
  ));
for (const pending of ["current", "own-shifts"])
  test(`${pending} loading does not block other section; retry is independent`, () =>
    withPage(
      { responses: { [path(1, pending)]: { deferred: "pending" } } },
      async (p) => {
        const waiting =
          pending === "current" ? currentSection(p) : shiftsSection(p);
        const ready =
          pending === "current" ? shiftsSection(p) : currentSection(p);
        await ready
          .getByText(pending === "current" ? "Ca 11:" : "Workday 20,", {
            exact: false,
          })
          .waitFor();
        await waiting.getByRole("status").waitFor();
        await p.evaluate(() =>
          window.fixture.finish("pending", { status: 503, body: {} }),
        );
        await waiting.getByRole("alert").waitFor();
        const before = await p.evaluate(
          () =>
            window.fixture.calls.filter((c) => c.url.includes("/workdays/"))
              .length,
        );
        await waiting.getByRole("button", { name: "Đọc lại" }).click();
        await waiting.getByRole("status").waitFor();
        await p.waitForFunction(
          (n) =>
            window.fixture.calls.filter((c) => c.url.includes("/workdays/"))
              .length ===
            n + 1,
          before,
        );
        await ready
          .getByText(pending === "current" ? "Ca 11:" : "Workday 20,", {
            exact: false,
          })
          .waitFor();
      },
    ));
for (const status of [404, 503])
  test(`both ${status} unavailable independently, never empty`, () =>
    withPage(
      {
        responses: {
          [path(1, "current")]: { status, body: {} },
          [path(1, "own-shifts")]: { status, body: {} },
        },
      },
      async (p) => {
        await currentSection(p).getByRole("alert").waitFor();
        await shiftsSection(p).getByRole("alert").waitFor();
        assert.equal(
          await p.getByText("Không có workday đang làm.").count(),
          0,
        );
        assert.equal(
          await p.getByText("Không có ca đủ điều kiện trong ngày.").count(),
          0,
        );
      },
    ));
test("normal own shifts/current render, no rowversion exposed", () =>
  withPage({}, async (p) => {
    await p.getByText("Workday 20,", { exact: false }).waitFor();
    await p.getByText("Ca 11:", { exact: false }).waitFor();
    assert.ok(!(await p.locator("body").innerText()).includes("AAAAAAAAAAE="));
  }));
test("empty shifts and explicit current 204", () =>
  withPage(
    {
      responses: {
        [path(1, "own-shifts")]: { body: [] },
        [path(1, "current")]: { status: 204 },
      },
    },
    async (p) => {
      await p.getByText("Không có workday đang làm.").waitFor();
      await p.getByText("Không có ca đủ điều kiện trong ngày.").waitFor();
    },
  ));
for (const status of [401, 403, 404, 503])
  test(`backend ${status} never renders empty`, () =>
    withPage(
      {
        responses: {
          [path(1, "own-shifts")]: {
            status,
            body: { title: secret, detail: secret },
          },
        },
      },
      async (p) => {
        await p.getByRole("alert").waitFor();
        assert.equal(
          await p.getByText("Không có ca đủ điều kiện trong ngày.").count(),
          0,
        );
        assert.equal(
          (await p.evaluate(() => window.fixture.calls)).filter((c) =>
            c.url.endsWith("/current"),
          ).length,
          1,
        );
      },
    ));
for (const reply of [{ status: 401, body: {} }, { body: session(true) }])
  test(`unconfirmed/restricted cannot request workday ${reply.status ?? "restricted"}`, () =>
    withPage({ sessions: [reply] }, async (p) => {
      await p
        .getByText("Chưa xác nhận phiên đăng nhập không hạn chế.")
        .waitFor();
      assert.equal(
        (await p.evaluate(() => window.fixture.calls)).filter((c) =>
          c.url.includes("/workdays/"),
        ).length,
        0,
      );
    }));
test("branch change hides data and ignores late current", () =>
  withPage(
    {
      responses: {
        [path(1, "current")]: { deferred: "old" },
        [path(2, "own-shifts")]: { body: [shift(2)] },
        [path(2, "current")]: { body: { ...day(2), workdayId: 22 } },
      },
    },
    async (p) => {
      await p.waitForFunction(() =>
        window.fixture.calls.some((c) => c.url.endsWith("/current")),
      );
      await p.evaluate(() => window.changeBranch(2));
      await p.getByText("Workday 22,", { exact: false }).waitFor();
      await p.evaluate((r) => window.fixture.finish("old", { body: r }), day());
      assert.equal(
        await p.getByText("Workday 20,", { exact: false }).count(),
        0,
      );
    },
  ));
test("identity replacement cannot publish old identity data", () =>
  withPage(
    {
      sessions: [{ body: session() }, { body: session(false, "8") }],
      responses: {
        [path(1, "own-shifts")]: { deferred: "old" },
        [path(1, "current")]: { deferred: "oldCurrent" },
      },
    },
    async (p) => {
      await p.waitForFunction(() =>
        window.fixture.calls.some((c) => c.url.includes("/workdays/")),
      );
      await p.evaluate(
        ({ s, d }) => {
          window.scenario.responses["/api/branches/1/workdays/own-shifts"] = {
            body: [s],
          };
          window.scenario.responses["/api/branches/1/workdays/current"] = {
            body: d,
          };
          return window.fixture.revalidate();
        },
        { s: { ...shift(), shiftId: 88 }, d: { ...day(), workdayId: 99 } },
      );
      await p
        .getByText("Chưa xác nhận phiên đăng nhập không hạn chế.")
        .waitFor({ state: "hidden" });
      await p.evaluate(() => window.fixture.finish("old", { body: [] }));
      await p.evaluate(
        (d) => window.fixture.finish("oldCurrent", { body: d }),
        day(),
      );
      await p.getByText("Workday 99,", { exact: false }).waitFor();
      await p.getByText("Ca 88:", { exact: false }).waitFor();
      await settle(p);
      assert.equal(
        await p.getByText("Không có ca đủ điều kiện trong ngày.").count(),
        0,
      );
      assert.equal(
        await p.getByText("Workday 20,", { exact: false }).count(),
        0,
      );
    },
  ));
test("FE01 and FE02 navigation delegates to accepted hosts", () =>
  withPage({}, async (p) => {
    await p.getByText("Workday 20,", { exact: false }).waitFor();
    await p.getByRole("link", { name: "Hồ sơ", exact: true }).click();
    await p
      .getByRole("heading", { name: /hồ sơ|profile/i })
      .first()
      .waitFor();
    await p.evaluate(() => window.fixture.navigate("/hotel"));
    await p.waitForURL("**/hotel");
    assert.equal(
      await p
        .getByRole("heading", { name: "Ca làm và workday của tôi" })
        .count(),
      0,
    );
    await p.goBack();
    await p.waitForURL("**/profile");
  }));
test("accepted entry remains unchanged; no production route activation", () => {
  assert.ok(readFileSync("index.html", "utf8").includes("/src/hotel/main.tsx"));
  assert.ok(
    !readFileSync("src/hotel/HotelRouteHost.tsx", "utf8").includes("workday"),
  );
});

test("malformed branch binding never publishes another branch", () =>
  withPage(
    { responses: { [path(1, "own-shifts")]: { body: [shift(2)] } } },
    async (p) => {
      await p.getByRole("alert").waitFor();
      assert.equal(
        (await p.evaluate(() => window.fixture.calls)).filter((c) =>
          c.url.endsWith("/current"),
        ).length,
        1,
      );
    },
  ));
test("current 404 is unavailable even after valid empty shifts", () =>
  withPage(
    {
      responses: {
        [path(1, "own-shifts")]: { body: [] },
        [path(1, "current")]: { status: 404, body: {} },
      },
    },
    async (p) => {
      await p.getByRole("alert").waitFor();
      assert.equal(await p.getByText("Không có workday đang làm.").count(), 0);
    },
  ));
test("unmount ignores late read; existing profile remains", () =>
  withPage(
    { responses: { [path(1, "current")]: { deferred: "unmounted" } } },
    async (p) => {
      await p.waitForFunction(() =>
        window.fixture.calls.some((c) => c.url.endsWith("/current")),
      );
      await p.getByRole("status").waitFor();
      await p.getByRole("link", { name: "Hồ sơ", exact: true }).click();
      await p.waitForURL("**/profile");
      await p.evaluate(
        (d) => window.fixture.finish("unmounted", { body: d }),
        day(),
      );
      assert.equal(
        await p.getByText("Workday 20,", { exact: false }).count(),
        0,
      );
    },
  ));
