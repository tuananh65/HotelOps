import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
import { session } from "./workday-fixtures.mjs";
const bundle = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `import React from'react';import{createRoot}from'react-dom/client';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthApi}from'./src/auth/authApi';import{createAuthStore}from'./src/auth/authStore';import{CustomRoleForm}from'./src/roles/CustomRoleForm';const s=window.scenario,calls=[],waiters=[];const dto={roleId:12,roleCode:s.createdCode??'CUSTOM_X',roleName:'Custom name',description:null,isSystemRole:s.system??false,isActive:true,assignedUserCount:0,permissionGrantCount:0};window.fetch=async(u,o)=>{calls.push({url:u,method:o.method});if(u==='/api/auth/session')return Response.json({...${JSON.stringify(session())},mustChangePassword:s.restricted??false});if(u==='/api/auth/csrf')return Response.json({headerName:'X-CSRF-TOKEN',csrfToken:'synthetic'});if(o.method==='GET')return Response.json({role:dto,grants:[]});const finish=()=>{if(s.network)throw Error('SECRET');return s.status?Response.json({detail:'SECRET'},{status:s.status}):Response.json(dto,{status:201,headers:{Location:'/api/roles/12'}});};if(s.late)return new Promise(resolve=>waiters.push(()=>resolve(finish())));return finish();};const store=createAuthStore(createAuthApi(window.fetch));const app=<CustomRoleForm roleId={s.edit?12:undefined} dispatchEnabled={s.enabled??false}/>;createRoot(document.getElementById('root')).render(<AuthProvider store={store} fallback={app}>{app}</AuthProvider>);window.fixture={calls,replace:()=>store.revalidate(),finish:()=>waiters.splice(0).forEach(f=>f())};`,
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
async function run(s, fn) {
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
    await p.goto("https://offline.test");
    await p.evaluate((s) => {
      window.scenario = s;
      for (const k of ["localStorage", "sessionStorage"])
        Object.defineProperty(window, k, {
          get() {
            throw Error("storage forbidden");
          },
        });
    }, s);
    await p.addScriptTag({ content: bundle.outputFiles[0].text });
    await fn(p);
    assert.deepEqual(errors, []);
    assert.ok(!(await p.locator("body").innerText()).includes("SECRET"));
  } finally {
    await p.close();
  }
}
async function review(p, code = "CUSTOM_X") {
  await p.getByLabel("Role code", { exact: true }).fill(code);
  await p.getByLabel("Role name", { exact: true }).fill("Custom name");
  await p.getByRole("button", { name: "Review metadata" }).click();
}
test("OFF allows review but no CSRF/POST", () =>
  run({}, async (p) => {
    await review(p);
    assert.equal(
      await p.getByRole("button", { name: "Confirm create" }).isDisabled(),
      true,
    );
    assert.equal((await p.evaluate(() => window.fixture.calls)).length, 1);
  }));
test("create refreshes authoritative detail", () =>
  run({ enabled: true }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm create" }).click();
    await p
      .getByText("Created custom role 12. Authoritative detail refreshed.")
      .waitFor();
    assert.equal(
      (await p.evaluate(() => window.fixture.calls)).filter(
        (c) => c.method === "POST",
      ).length,
      1,
    );
  }));
test("required fields before review", () =>
  run({}, async (p) => {
    await p.getByRole("button", { name: "Review metadata" }).click();
    await p.getByText("Required code/name or metadata is invalid.").waitFor();
    assert.equal(
      await p.getByRole("button", { name: "Confirm create" }).count(),
      0,
    );
  }));
test("double confirm one POST", () =>
  run({ enabled: true, late: true }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm create" }).evaluate((b) => {
      b.click();
      b.click();
    });
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.method === "POST"),
    );
    assert.equal(
      (await p.evaluate(() => window.fixture.calls)).filter(
        (c) => c.method === "POST",
      ).length,
      1,
    );
    await p.evaluate(() => window.fixture.finish());
    await p
      .getByText("Created custom role 12. Authoritative detail refreshed.")
      .waitFor();
  }));
test("unknown locks replay", () =>
  run({ enabled: true, network: true }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm create" }).click();
    await p.getByText(/Outcome unknown/).waitFor();
    assert.equal(
      await p.getByRole("button", { name: "Confirm create" }).count(),
      0,
    );
  }));
test("edit custom form validates but never PATCH", () =>
  run({ edit: true, enabled: true }, async (p) => {
    await p.getByLabel("Role name", { exact: true }).fill("Changed name");
    await p.getByRole("button", { name: "Review metadata" }).click();
    await p.getByText("Metadata valid; update remains fail-closed.").waitFor();
    assert.equal(
      await p.getByRole("button", { name: "Confirm create" }).isDisabled(),
      true,
    );
    assert.equal(
      (await p.evaluate(() => window.fixture.calls)).filter(
        (c) => c.method === "PATCH",
      ).length,
      0,
    );
  }));
test("system role edit protected", () =>
  run({ edit: true, system: true, enabled: true }, async (p) => {
    await p
      .getByText("System role protected. No metadata changes allowed.")
      .waitFor();
    assert.equal(
      await p.getByLabel("Role name", { exact: true }).isDisabled(),
      true,
    );
  }));
test("session replacement hides late create result", () =>
  run({ enabled: true, late: true }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm create" }).click();
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.method === "POST"),
    );
    await p.evaluate(() => window.fixture.replace());
    await p.getByLabel("Role code", { exact: true }).waitFor();
    await p.evaluate(() => window.fixture.finish());
    assert.equal(await p.getByText(/Created custom role/).count(), 0);
  }));
test("restricted no create", () =>
  run({ restricted: true }, async (p) => {
    await p.getByText("Confirmed unrestricted session required.").waitFor();
    assert.equal((await p.evaluate(() => window.fixture.calls)).length, 1);
  }));

test("invariant Unicode code confirms Create and refreshes without replay", () =>
  run({ enabled: true, createdCode: "STRAßE" }, async (p) => {
    await review(p, " straße ");
    await p.getByRole("button", { name: "Confirm create" }).click();
    await p
      .getByText("Created custom role 12. Authoritative detail refreshed.")
      .waitFor();
    await p
      .getByRole("region", { name: "Authoritative role" })
      .getByText("STRAßE — Custom name")
      .waitFor();
    const calls = await p.evaluate(() => window.fixture.calls);
    assert.equal(calls.filter((c) => c.method === "POST").length, 1);
    assert.equal(
      calls.filter((c) => c.url === "/api/roles/12" && c.method === "GET")
        .length,
      1,
    );
  }));
