import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
import { createServer } from "node:http";
import { mkdir } from "node:fs/promises";
await mkdir(".test-output/shared-host", { recursive: true });
await build({
  entryPoints: ["src/shared-host/returnUrl.ts"],
  outbase: "src",
  outdir: ".test-output/shared-host",
  platform: "node",
  format: "esm",
  bundle: true,
});
const { validateReturnUrl, approvedReturnDestination } =
  await import("../.test-output/shared-host/shared-host/returnUrl.js");
for (const bad of [
  "https://evil.test",
  "//evil.test",
  "/\\evil",
  "/%5cevil",
  "/%2f%2fevil",
  "/%252fevil",
  "/a\n",
  "/a%0d",
  "/%",
  "/%GG",
  "/a#fragment",
  "/a/../b",
  "/%2e%2e/a",
])
  test("deny redirect " + JSON.stringify(bad), () =>
    assert.equal(validateReturnUrl(bad), null),
  );
test("preserve local query but require fresh session and enabled destination", () => {
  const p = "/rooms/status?branchId=7&roomId=11";
  assert.equal(validateReturnUrl(p), p);
  assert.equal(approvedReturnDestination(p, false), null);
  assert.equal(approvedReturnDestination(p, true), p);
  for (const p of [
    "/incidents/new?branchId=7",
    "/inspections/assignment?branchId=7",
    "/login",
    "/unknown",
  ])
    assert.equal(approvedReturnDestination(p, true), null);
});
const bundle = await build({
  loader: { ".css": "empty" },
  stdin: {
    loader: "tsx",
    resolveDir: process.cwd(),
    contents: `
import{createAuthStore}from'./src/auth/authStore';import{createAuthApi}from'./src/auth/authApi';import React,{StrictMode}from'react';import{createRoot}from'react-dom/client';import{BrowserRouter}from'react-router-dom';import{AuthProvider}from'./src/auth/AuthProvider';import{SharedAuthHost}from'./src/shared-host/SharedAuthHost';
let snapshot={authorityGeneration:Object.freeze({}),status:'INITIALIZING',session:null,pending:null,error:null};const listeners=new Set();const session={user:{id:'7',username:'staff',name:'Staff'},expiresAt:'2030-01-01T00:00:00Z',absoluteExpiresAt:'2030-01-02T00:00:00Z',mustChangePassword:false};window.calls=[];window.fetch=async(url)=>{window.calls.push(String(url));if(window.realAuth){const response=window.realReplies[String(url)]?.shift();if(!response)throw Error("Unexpected offline request");if(response.deferred)await new Promise(r=>window.finishRealRevalidation=r);return new Response(response.status===204?null:JSON.stringify(response.body),{status:response.status,headers:{"Content-Type":"application/json"}})}return new Response('{}',{status:503})};function set(status){snapshot={authorityGeneration:Object.freeze({}),status,session:['AUTHENTICATED','RESTRICTED'].includes(status)?{...session,mustChangePassword:status==='RESTRICTED'}:null,pending:null,error:null};listeners.forEach(f=>f())}const store={invalidateSession(expected){if(expected===snapshot.authorityGeneration)set("EXPIRED_INVALID")},subscribe(f){listeners.add(f);return()=>listeners.delete(f)},getSnapshot:()=>snapshot,initialize:async()=>({ok:false,error:{kind:'network'}}),revalidate:async()=>{window.revalidationCalls=(window.revalidationCalls??0)+1;if(window.revalidationMode==="fail"){set("ERROR_UNAVAILABLE");return{ok:false,error:{kind:"network"}}}if(window.revalidationMode==="defer")await new Promise(r=>window.finishRevalidation=r);set('AUTHENTICATED');return{ok:true,data:snapshot.session}},login:async()=>{set('AUTHENTICATED');return{ok:true,data:snapshot.session}},logout:async()=>{set('ANONYMOUS');return{ok:true}},changePassword:async()=>{set('AUTHENTICATED');return{ok:true,data:snapshot.session}}};window.fixture={set,navigate(path){history.pushState(null,'',path);dispatchEvent(new PopStateEvent('popstate'))}};const host=<SharedAuthHost/>;createRoot(document.getElementById('root')).render(<StrictMode><BrowserRouter><AuthProvider store={window.realAuth?window.realStore??(window.realStore=createAuthStore(createAuthApi(window.fetch))):store} fallback={host}>{host}</AuthProvider></BrowserRouter></StrictMode>);
`,
  },
  bundle: true,
  write: false,
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"production"', "import.meta.env": "{}" },
});
let browser, server, origin;
before(async () => {
  server = createServer((req, res) => {
    res.setHeader("Content-Type", "text/html");
    res.end(
      '<div id="root"></div><script>' +
        bundle.outputFiles[0].text +
        "</script>",
    );
  });
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  origin = "http://127.0.0.1:" + server.address().port;
  browser = await chromium.launch({
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    headless: true,
  });
});
after(async () => {
  await browser?.close();
  await new Promise((resolve) => server?.close(resolve));
});
async function pageAt(path, status) {
  const page = await browser.newPage();
  await page.goto(origin + path);
  await page.waitForFunction(() => !!window.fixture);
  if (status) await page.evaluate((s) => window.fixture.set(s), status);
  return page;
}
test("bootstrap fail closed and explicit recovery", async () => {
  const p = await pageAt("/rooms/status");
  await p.getByText("Checking your session...").waitFor();
  assert.equal(await p.getByRole("navigation").count(), 0);
  await p.evaluate(() => window.fixture.set("ERROR_UNAVAILABLE"));
  await p.getByRole("button", { name: "Check session" }).click();
  await p.getByRole("navigation").waitFor();
  await p.close();
});
test("anonymous deep link then login restores default only", async () => {
  const p = await pageAt("/rooms/status?branchId=7", "ANONYMOUS");
  await p.getByRole("heading", { name: "Sign In", exact: true }).waitFor();
  await p.getByLabel("Username or email").fill("staff");
  await p.getByLabel("Password", { exact: true }).fill("synthetic");
  await p.getByRole("button", { name: "Sign in", exact: true }).click();
  await p.getByRole("navigation").waitFor();
  assert.equal(new URL(p.url()).pathname, "/rooms/status");
  await p.close();
});
test("restricted cannot render operations; profile logout removes protected navigation", async () => {
  const p = await pageAt("/rooms/status", "RESTRICTED");
  await p
    .getByRole("heading", { name: "Change Password", exact: true })
    .waitFor();
  assert.equal(await p.getByRole("navigation").count(), 0);
  await p.evaluate(() => {
    window.fixture.set("AUTHENTICATED");
    window.fixture.navigate("/profile");
  });
  await p.getByRole("button", { name: "Logout", exact: true }).click();
  await p.getByRole("heading", { name: "Sign In", exact: true }).waitFor();
  await p.close();
});
for (const path of [
  "/rooms/status",
  "/rooms/turnover",
  "/cleaning/tasks",
  "/cleaning/tasks/new",
  "/cleaning/tasks/12",
  "/cleaning/assignment",
  "/rework",
  "/rework/12",
])
  test("existing operations route preserved " + path, async () => {
    const p = await pageAt(path, "AUTHENTICATED");
    await p.getByRole("navigation", { name: "Hotel operations" }).waitFor();
    assert.equal(
      await p.getByText("Page not found", { exact: true }).count(),
      0,
    );
    await p.close();
  });
for (const path of [
  "/inspections/assignment?branchId=7&roomId=11",
  "/incidents/new?branchId=7&roomId=11",
])
  test("disabled destination no API " + path, async () => {
    const p = await pageAt(path, "AUTHENTICATED");
    await p.getByText(/not activated/).waitFor();
    assert.deepEqual(await p.evaluate(() => window.calls), []);
    await p.close();
  });

for (const destination of [
  "/rooms/status?branchId=7&roomId=11",
  "/cleaning/tasks/12?branchId=7",
  "/rework?branchId=7",
])
  test("login restores local path and query " + destination, async () => {
    const p = await pageAt(
      "/login?" + new URLSearchParams({ returnTo: destination }),
      "ANONYMOUS",
    );
    await p.getByLabel("Username or email").fill("staff");
    await p.getByLabel("Password", { exact: true }).fill("synthetic");
    await p.getByRole("button", { name: "Sign in", exact: true }).click();
    await p.waitForURL(origin + destination);
    assert.equal(await p.evaluate(() => window.revalidationCalls), 1);
    await p.close();
  });
for (const destination of [
  "https://evil.test",
  "//evil.test",
  "/login",
  "/forgot-password",
  "/incidents/new?branchId=7",
  "/inspections/assignment?branchId=7",
  "/rooms/status?token=private",
  "/rooms/status?branchId=0",
])
  test("invalid or inactive return falls back " + destination, async () => {
    const p = await pageAt(
      "/login?" + new URLSearchParams({ returnTo: destination }),
      "AUTHENTICATED",
    );
    await p.waitForURL(origin + "/rooms/status");
    await p.close();
  });
test("failed revalidation does not restore destination; explicit recovery required", async () => {
  const p = await pageAt(
    "/login?" + new URLSearchParams({ returnTo: "/rework?branchId=7" }),
    "ANONYMOUS",
  );
  await p.evaluate(() => (window.revalidationMode = "fail"));
  await p.getByLabel("Username or email").fill("staff");
  await p.getByLabel("Password", { exact: true }).fill("synthetic");
  await p.getByRole("button", { name: "Sign in", exact: true }).click();
  await p.getByRole("button", { name: "Check session" }).waitFor();
  assert.equal(await p.getByRole("navigation").count(), 0);
  assert.equal(new URL(p.url()).pathname, "/login");
  await p.evaluate(() => (window.revalidationMode = "success"));
  await p.getByRole("button", { name: "Check session" }).click();
  await p.waitForURL(origin + "/rework?branchId=7");
  await p.close();
});
test("post-login revalidation blocks destination until completion", async () => {
  const p = await pageAt(
    "/login?" + new URLSearchParams({ returnTo: "/rooms/status?branchId=7" }),
    "ANONYMOUS",
  );
  await p.evaluate(() => (window.revalidationMode = "defer"));
  await p.getByLabel("Username or email").fill("staff");
  await p.getByLabel("Password", { exact: true }).fill("synthetic");
  await p.getByRole("button", { name: "Sign in", exact: true }).click();
  await p.waitForFunction(() => !!window.finishRevalidation);
  assert.equal(await p.getByRole("navigation").count(), 0);
  await p.evaluate(() => window.finishRevalidation());
  await p.waitForURL(origin + "/rooms/status?branchId=7");
  await p.close();
});

const realSession = {
  user: { id: "7", username: "staff", name: "Staff" },
  expiresAt: "2030-01-01T00:00:00Z",
  absoluteExpiresAt: "2030-01-02T00:00:00Z",
  mustChangePassword: false,
  csrfToken: "SYNTHETIC-ONLY-CSRF",
};
async function realLoginPage(replies) {
  const p = await browser.newPage();
  await p.addInitScript((replies) => {
    window.realAuth = true;
    window.realReplies = replies;
  }, replies);
  await p.goto(
    origin + "/login?" + new URLSearchParams({ returnTo: "/profile" }),
  );
  await p.getByRole("heading", { name: "Sign In", exact: true }).waitFor();
  await p.getByLabel("Username or email").fill("staff");
  await p.getByLabel("Password", { exact: true }).fill("synthetic-only");
  await p.getByRole("button", { name: "Sign in", exact: true }).click();
  return p;
}
function repliesAfterLogin(last) {
  return {
    "/api/auth/session": [
      { status: 401, body: { status: 401, code: "AUTHENTICATION_REQUIRED" } },
      last,
    ],
    "/api/auth/csrf": [
      {
        status: 200,
        body: { csrfToken: "SYNTHETIC-ONLY-CSRF", headerName: "X-CSRF-TOKEN" },
      },
    ],
    "/api/auth/login": [{ status: 200, body: realSession }],
  };
}
test("real AuthApi/store StrictMode login revalidates before return URL and strips token", async () => {
  const p = await realLoginPage(
    repliesAfterLogin({ status: 200, body: realSession }),
  );
  await p.waitForURL(origin + "/profile");
  assert.deepEqual(await p.evaluate(() => window.calls), [
    "/api/auth/session",
    "/api/auth/csrf",
    "/api/auth/login",
    "/api/auth/session",
  ]);
  assert.equal(
    (await p.locator("body").innerText()).includes("SYNTHETIC-ONLY-CSRF"),
    false,
  );
  await p.close();
});
test("real AuthApi/store revalidation 401 denies restoration and explicit GET recovers", async () => {
  const r = repliesAfterLogin({
    status: 401,
    body: { status: 401, code: "SESSION_INVALID" },
  });
  r["/api/auth/session"].push({ status: 200, body: realSession });
  const p = await realLoginPage(r);
  await p.getByRole("button", { name: "Check session" }).waitFor();
  assert.equal(new URL(p.url()).pathname, "/login");
  assert.equal(await p.getByRole("navigation").count(), 0);
  await p.getByRole("button", { name: "Check session" }).click();
  await p.waitForURL(origin + "/profile");
  assert.equal(
    (await p.evaluate(() => window.calls)).filter(
      (x) => x === "/api/auth/login",
    ).length,
    1,
  );
  await p.close();
});
test("real restricted session cannot restore business destination", async () => {
  const restricted = { ...realSession, mustChangePassword: true };
  const r = repliesAfterLogin({ status: 200, body: restricted });
  r["/api/auth/login"] = [{ status: 200, body: restricted }];
  const p = await realLoginPage(r);
  await p.waitForURL(origin + "/change-password");
  assert.equal(await p.getByRole("navigation").count(), 0);
  await p.close();
});

function loginUrl(destination) {
  return "/login?" + new URLSearchParams({ returnTo: destination });
}
async function signIn(p) {
  await p.getByLabel("Username or email").fill("staff");
  await p.getByLabel("Password", { exact: true }).fill("synthetic");
  await p.getByRole("button", { name: "Sign in", exact: true }).click();
}
test("031 query A to B before login uses B", async () => {
  const p = await pageAt(loginUrl("/profile"), "ANONYMOUS");
  await p.evaluate(
    (path) => window.fixture.navigate(path),
    loginUrl("/rework?branchId=7"),
  );
  await signIn(p);
  await p.waitForURL(origin + "/rework?branchId=7");
  assert.equal(await p.evaluate(() => window.revalidationCalls), 1);
  await p.close();
});
for (const next of [
  "/rework?branchId=7",
  "https://evil.test",
  "/incidents/new?branchId=7",
  "/inspections/assignment?branchId=7",
])
  test("031 pending old GET cannot restore A after query " + next, async () => {
    const p = await pageAt(loginUrl("/profile"), "ANONYMOUS");
    await p.evaluate(() => (window.revalidationMode = "defer"));
    await signIn(p);
    await p.waitForFunction(() => !!window.finishRevalidation);
    await p.evaluate((path) => {
      window.oldFinish = window.finishRevalidation;
      window.fixture.navigate(path);
    }, loginUrl(next));
    await p.getByText("Checking your session...").waitFor();
    await p.evaluate(() => window.oldFinish());
    await p.waitForFunction(() => window.revalidationCalls === 2);
    assert.equal(new URL(p.url()).pathname, "/login");
    assert.equal(await p.getByRole("navigation").count(), 0);
    await p.evaluate(() => window.finishRevalidation());
    await p.waitForURL(
      origin + (next.startsWith("/rework") ? next : "/rooms/status"),
    );
    await p.close();
  });
test("031 real same-account session replacement invalidates old GET without replaying login", async () => {
  const r = repliesAfterLogin({
    status: 200,
    body: realSession,
    deferred: true,
  });
  r["/api/auth/change-password"] = [
    {
      status: 200,
      body: { ...realSession, csrfToken: "REPLACEMENT-SYNTHETIC" },
    },
  ];
  const p = await realLoginPage(r);
  await p.waitForFunction(() => !!window.finishRealRevalidation);
  await p.evaluate(async () => {
    await window.realStore.changePassword({
      currentPassword: "synthetic-old",
      newPassword: "synthetic-new",
      confirmNewPassword: "synthetic-new",
    });
    window.finishRealRevalidation();
  });
  await p.getByRole("button", { name: "Check session" }).waitFor();
  assert.equal(new URL(p.url()).pathname, "/login");
  assert.equal(
    (await p.evaluate(() => window.calls)).filter(
      (x) => x === "/api/auth/login",
    ).length,
    1,
  );
  assert.equal(await p.getByRole("navigation").count(), 0);
  await p.close();
});
test("031 real logout during pending revalidation cannot restore protected destination", async () => {
  const r = repliesAfterLogin({
    status: 200,
    body: realSession,
    deferred: true,
  });
  r["/api/auth/logout"] = [{ status: 204 }];
  const p = await realLoginPage(r);
  await p.waitForFunction(() => !!window.finishRealRevalidation);
  await p.evaluate(async () => {
    await window.realStore.logout();
    window.finishRealRevalidation();
  });
  await p.getByRole("button", { name: "Check session" }).waitFor();
  assert.equal(new URL(p.url()).pathname, "/login");
  assert.equal(await p.getByRole("navigation").count(), 0);
  assert.equal(
    (await p.evaluate(() => window.calls)).filter(
      (x) => x === "/api/auth/login",
    ).length,
    1,
  );
  await p.close();
});

test("031 real store navigation change requires a fresh GET for current query", async () => {
  const replies = repliesAfterLogin({
    status: 200,
    body: realSession,
    deferred: true,
  });
  replies["/api/auth/session"].push({ status: 200, body: realSession });
  const p = await realLoginPage(replies);
  await p.waitForFunction(() => !!window.finishRealRevalidation);
  await p.evaluate(
    (path) => window.fixture.navigate(path),
    loginUrl("/profile?branchId=7&roomId=11"),
  );
  await p.getByText("Checking your session...").waitFor();
  await p.evaluate(() => window.finishRealRevalidation());
  await p.waitForURL(origin + "/profile?branchId=7&roomId=11");
  const calls = await p.evaluate(() => window.calls);
  assert.equal(calls.filter((x) => x === "/api/auth/session").length, 3);
  assert.equal(calls.filter((x) => x === "/api/auth/login").length, 1);
  await p.close();
});
