import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
import { createServer } from "node:http";
import { mkdir } from "node:fs/promises";
await mkdir(".test-output/shared-host", { recursive: true });
await build({
  entryPoints: [
    "src/shared-host/returnUrl.ts",
    "src/features/inspectionAssignment/navigation.ts",
    "src/incidents/navigation.ts",
  ],
  outbase: "src",
  outdir: ".test-output/shared-host",
  platform: "node",
  format: "esm",
  bundle: true,
});
const { validateReturnUrl, approvedReturnDestination } =
  await import("../.test-output/shared-host/shared-host/returnUrl.js");
const { parseAssignmentContext } =
  await import("../.test-output/shared-host/features/inspectionAssignment/navigation.js");
const { parseIncidentContext } =
  await import("../.test-output/shared-host/incidents/navigation.js");
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
test("DEV1 exact assignment parser", () => {
  assert.deepEqual(parseAssignmentContext("?branchId=7&roomId=11"), {
    branchId: 7,
    roomId: 11,
  });
  for (const q of [
    "",
    "?roomId=11",
    "?branchId=0",
    "?branchId=7&branchId=8",
    "?branchId=7&unknown=1",
  ])
    assert.equal(parseAssignmentContext(q), null);
});
test("DEV2 exact incident parser", () => {
  assert.deepEqual(parseIncidentContext("?branchId=7&roomId=11"), {
    branchId: 7,
    roomId: 11,
  });
  for (const q of [
    "?roomId=11",
    "?branchId=0",
    "?branchId=7&branchId=8",
    "?branchId=9007199254740992",
  ])
    assert.throws(() => parseIncidentContext(q));
});
const bundle = await build({
  stdin: {
    loader: "tsx",
    resolveDir: process.cwd(),
    contents: `
import React from'react';import{createRoot}from'react-dom/client';import{BrowserRouter}from'react-router-dom';import{AuthProvider}from'./src/auth/AuthProvider';import{SharedAuthHost}from'./src/shared-host/SharedAuthHost';
let snapshot={status:'INITIALIZING',session:null,pending:null,error:null};const listeners=new Set();const session={user:{id:'7',username:'staff',name:'Staff'},expiresAt:'2030-01-01T00:00:00Z',absoluteExpiresAt:'2030-01-02T00:00:00Z',mustChangePassword:false};window.calls=[];window.fetch=async(url)=>{window.calls.push(String(url));return new Response('{}',{status:503})};function set(status){snapshot={status,session:['AUTHENTICATED','RESTRICTED'].includes(status)?{...session,mustChangePassword:status==='RESTRICTED'}:null,pending:null,error:null};listeners.forEach(f=>f())}const store={subscribe(f){listeners.add(f);return()=>listeners.delete(f)},getSnapshot:()=>snapshot,initialize:async()=>({ok:false,error:{kind:'network'}}),revalidate:async()=>{set('AUTHENTICATED');return{ok:true,data:session}},login:async()=>{set('AUTHENTICATED');return{ok:true,data:session}},logout:async()=>{set('ANONYMOUS');return{ok:true}},changePassword:async()=>{set('AUTHENTICATED');return{ok:true,data:session}}};window.fixture={set,navigate(path){history.pushState(null,'',path);dispatchEvent(new PopStateEvent('popstate'))}};const host=<SharedAuthHost/>;createRoot(document.getElementById('root')).render(<BrowserRouter><AuthProvider store={store} fallback={host}>{host}</AuthProvider></BrowserRouter>);
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
