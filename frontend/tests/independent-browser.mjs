import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
import { structureReply, session, secret } from "./independent-fixtures.mjs";
const result = await build({
  loader: { ".css": "empty" },
  stdin: {
    loader: "tsx",
    resolveDir: process.cwd(),
    contents: `
import React,{StrictMode}from'react';import{createRoot}from'react-dom/client';import{BrowserRouter}from'react-router-dom';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthApi}from'./src/auth/authApi';import{createAuthStore}from'./src/auth/authStore';import{SharedAuthHost}from'./src/shared-host/SharedAuthHost';
const scenario=window.scenario,calls=[],waiting=new Map();const structure=${structureReply.toString()};const make=r=>new Response(JSON.stringify(r.body),{status:r.status??200,headers:{'Content-Type':'application/json',...r.headers}});
window.fetch=async(url,init)=>{const call={url,method:init.method,credentials:init.credentials,cache:init.cache,aborted:false};calls.push(call);init.signal?.addEventListener('abort',()=>call.aborted=true,{once:true});let r;if(url==='/api/auth/session')r=scenario.auth??{body:${JSON.stringify(session())}};else if(url.startsWith('/api/users?'))r=scenario.users??{body:{items:[],page:1,pageSize:20,totalCount:0}};else if(url.includes('/structure')){const u=new URL(url,'https://offline.test'),key=u.pathname.split('/')[3]+':'+u.searchParams.get('nodeType')+':'+u.searchParams.get('collection')+':'+u.searchParams.get('page');r=scenario.replies?.[key]??scenario.structure??{body:structure(url,scenario.management!==false)};}else if(url==='/api/branches/42/edit')r={body:{id:42,code:'B42',name:'Update preserved',address:'Address',phone:null,email:null,timezone:'Asia/Ho_Chi_Minh',isActive:true},headers:{ETag:'"'+'A'.repeat(64)+'"'}};else throw Error('Unexpected '+url);if(r.deferred)return new Promise(resolve=>waiting.set(r.deferred,resolve));return make(r)};
const store=createAuthStore(createAuthApi(window.fetch));const app=<BrowserRouter><SharedAuthHost usersReadEnabled structureReadEnabled/></BrowserRouter>;createRoot(document.getElementById('root')).render(<StrictMode><AuthProvider store={store} fallback={app}>{app}</AuthProvider></StrictMode>);window.fixture={calls,scenario,revalidate:()=>store.revalidate(),finish:(key,r)=>waiting.get(key)(make(r)),navigate(path){history.pushState(null,'',path);dispatchEvent(new PopStateEvent('popstate'))}};
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
export async function pageTest(scenario, run, path = "/hotel") {
  const p = await browser.newPage(),
    errors = [],
    logs = [],
    network = [];
  p.setDefaultTimeout(6000);
  p.on("pageerror", (e) => errors.push(e.message));
  p.on("console", (m) => logs.push(m.text()));
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
    }, scenario);
    await p.addScriptTag({ content: result.outputFiles[0].text });
    await run(p);
    assert.deepEqual(errors, []);
    assert.deepEqual(network, []);
    assert.ok(!logs.some((v) => v.includes(secret)));
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

export async function reloadCompositionPage(p, scenario = {}) {
  await p.reload();
  await p.evaluate((s) => (window.scenario = s), scenario);
  await p.addScriptTag({ content: result.outputFiles[0].text });
}
