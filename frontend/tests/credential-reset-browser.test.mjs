import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
const secret = "SYNTHETIC-RESET-PASSWORD",
  token = "SYNTHETIC-CSRF",
  tag = '"0123456789ABCDEF"',
  next = '"0123456789ABCDE0"';
const bundle = await build({
  stdin: {
    loader: "tsx",
    resolveDir: process.cwd(),
    contents: `
import React,{StrictMode}from'react';import{createRoot}from'react-dom/client';import{BrowserRouter}from'react-router-dom';import{AuthProvider}from'./src/auth/AuthProvider';import{AuthRouteHost}from'./src/auth/AuthRouteHost';import{createAuthApi}from'./src/auth/authApi';import{createAuthStore}from'./src/auth/authStore';
const s=window.scenario,calls=[],waiting=new Map(),session={user:{id:'7',username:'admin',name:'Admin'},expiresAt:'2030-01-01T00:00:00Z',absoluteExpiresAt:'2030-01-02T00:00:00Z',mustChangePassword:s.restricted??false,csrfToken:'SYNTHETIC-CSRF'};
const make=r=>{if(r.network)throw Error('PRIVATE-DETAIL');return Response.json(r.body??{}, {status:r.status??200,headers:r.headers??{}});};
window.fetch=async(url,o)=>{calls.push({url,method:o.method,body:o.body,headers:Object.fromEntries(new Headers(o.headers)),aborted:false});const c=calls.at(-1);o.signal?.addEventListener('abort',()=>c.aborted=true,{once:true});let r;if(url==='/api/auth/session')r=s.auth??{body:session};else if(url==='/api/auth/csrf')r={body:{headerName:'X-CSRF-TOKEN',csrfToken:${JSON.stringify(token)}}};else if(url==='/api/auth/reauthenticate')r=s.proof??{body:{expiresAtUtc:'2030-01-01T01:00:00Z'}};else if(url.endsWith('/profile')){const id=Number(url.split('/')[3]);r=s.profile??{body:{id,fullName:'Reviewed Employee '+id,email:'employee@example.test',phone:null},headers:{ETag:${JSON.stringify(tag)}}};}else if(url.endsWith('/reset-credential'))r=s.reset??{body:{temporaryCredential:{password:${JSON.stringify(secret)},expiresAtUtc:'2030-01-01T02:00:00Z'}},headers:{ETag:${JSON.stringify(next)}}};else if(url==='/api/users/42')r={body:{id:42,employeeCode:'E42',username:'staff42',fullName:'Reviewed Employee 42',email:'employee@example.test',phone:null,accountStatus:'ACTIVE',primaryBranch:null,department:null}};else if(url.includes('/effective-access'))r={status:403};else throw Error('Unexpected request '+url);if(r.defer)return new Promise(resolve=>waiting.set(r.defer,resolve));return make(r);};
const store=createAuthStore(createAuthApi(window.fetch));const host=<AuthRouteHost/>;const root=createRoot(document.getElementById('root'));root.render(<StrictMode><BrowserRouter><AuthProvider store={store} fallback={host}>{host}</AuthProvider></BrowserRouter></StrictMode>);window.fixture={calls,session,s,replace:()=>store.revalidate(),finish:(key,r)=>waiting.get(key)(make(r)),navigate(path){history.pushState(null,'',path);dispatchEvent(new PopStateEvent('popstate'));}};
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
async function run(s, fn, path = "/users/42/reset-credential") {
  const p = await browser.newPage();
  p.setDefaultTimeout(6000);
  const errors = [],
    logs = [],
    network = [];
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
      window.copied = [];
      Object.defineProperty(navigator, "clipboard", {
        value: {
          writeText: async (v) => {
            if (s.clipboardFailure) throw Error("PRIVATE-DETAIL");
            window.copied.push(v);
          },
        },
      });
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
    assert.ok(
      !logs.some(
        (x) =>
          x.includes(secret) ||
          x.includes(token) ||
          x.includes("ADMIN-PASSWORD"),
      ),
    );
    assert.ok(
      !(await p.locator("body").innerText()).includes("PRIVATE-DETAIL"),
    );
  } finally {
    await p.close();
  }
}
const calls = (p) => p.evaluate(() => window.fixture.calls);
async function prepare(p) {
  await p.getByLabel("Current administrator password").fill("ADMIN-PASSWORD");
  await p
    .getByRole("button", { name: "Verify administrator and load account" })
    .click();
  await p.getByRole("region", { name: "Review credential reset" }).waitFor();
}
async function confirm(p) {
  await p.getByRole("checkbox").check();
  await p.getByRole("button", { name: "Confirm credential reset" }).click();
}
async function settle(p) {
  await p.evaluate(
    () =>
      new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))),
  );
}
test("real route success: proof then correlated ETag review, confirmation, secret handoff and no replay", () =>
  run({}, async (p) => {
    await prepare(p);
    assert.equal(
      await p
        .getByRole("button", { name: "Confirm credential reset" })
        .isDisabled(),
      true,
    );
    assert.ok(
      (await p.locator("body").innerText()).includes("Reviewed Employee 42"),
    );
    assert.equal(
      (await calls(p)).filter((c) => c.url.endsWith("/reset-credential"))
        .length,
      0,
    );
    await confirm(p);
    await p
      .getByRole("heading", { name: "Credential reset confirmed" })
      .waitFor();
    assert.ok(!(await p.locator("body").innerText()).includes(secret));
    await p.getByRole("button", { name: "Reveal password" }).click();
    assert.equal(await p.getByLabel("Temporary password").innerText(), secret);
    await p.getByRole("button", { name: "Copy password" }).click();
    assert.deepEqual(await p.evaluate(() => window.copied), [secret]);
    await p
      .getByRole("button", { name: "I securely handed off the credential" })
      .click();
    assert.ok(!(await p.locator("body").innerText()).includes(secret));
    assert.equal(
      await p.getByRole("button", { name: "Confirm credential reset" }).count(),
      0,
    );
    const c = (await calls(p)).find((c) => c.url.endsWith("/reset-credential"));
    assert.deepEqual(JSON.parse(c.body), {
      identityVerificationConfirmed: true,
    });
    assert.equal(c.headers["if-match"], tag);
    assert.equal(c.headers["x-csrf-token"], token);
  }));
for (const status of [400, 403, 404, 409, 412, 428])
  test("reset " + status + " clears protected review and never retries", () =>
    run(
      {
        reset: {
          status,
          body: {
            status,
            code: "TARGET_NOT_ACTIVE",
            title: "TARGET_NOT_ACTIVE",
            detail: "PRIVATE-DETAIL",
          },
        },
      },
      async (p) => {
        await prepare(p);
        await confirm(p);
        await p.getByRole("alert").waitFor();
        assert.equal(
          await p
            .getByRole("region", { name: "Review credential reset" })
            .count(),
          0,
        );
        assert.equal(
          (await calls(p)).filter((c) => c.url.endsWith("/reset-credential"))
            .length,
          1,
        );
        assert.ok(!(await p.locator("body").innerText()).includes(secret));
      },
    ),
  );
for (const reset of [
  { status: 503, body: { detail: "PRIVATE-DETAIL" } },
  { network: true },
  {
    body: { temporaryCredential: { password: secret, expiresAtUtc: "bad" } },
    headers: { ETag: next },
  },
])
  test("uncertain reset locks replay " + JSON.stringify(reset), () =>
    run({ reset }, async (p) => {
      await prepare(p);
      await confirm(p);
      await p.getByText(/Reset outcome unknown/).waitFor();
      assert.equal(
        await p.getByLabel("Current administrator password").count(),
        0,
      );
      assert.equal(
        (await calls(p)).filter((c) => c.url.endsWith("/reset-credential"))
          .length,
        1,
      );
    }),
  );
test("double confirmation synchronous guard sends one reset POST", () =>
  run({ reset: { defer: "reset" } }, async (p) => {
    await prepare(p);
    await p.getByRole("checkbox").check();
    await p
      .getByRole("button", { name: "Confirm credential reset" })
      .evaluate((b) => {
        b.click();
        b.click();
      });
    await settle(p);
    assert.equal(
      (await calls(p)).filter((c) => c.url.endsWith("/reset-credential"))
        .length,
      1,
    );
  }));
test("proof failure never loads target or resets", () =>
  run(
    { proof: { status: 403, body: { detail: "PRIVATE-DETAIL" } } },
    async (p) => {
      await p
        .getByLabel("Current administrator password")
        .fill("ADMIN-PASSWORD");
      await p
        .getByRole("button", { name: "Verify administrator and load account" })
        .click();
      await p.getByRole("alert").waitFor();
      assert.equal(
        (await calls(p)).filter((c) => c.url.includes("/api/users/")).length,
        0,
      );
      assert.equal(
        await p.getByLabel("Current administrator password").inputValue(),
        "",
      );
    },
  ));
test("401 clears data; target change cannot bypass explicit session gate; failed revalidation stays closed", () =>
  run({ reset: { status: 401 }, auth: undefined }, async (p) => {
    await prepare(p);
    await confirm(p);
    await p.getByRole("main", { name: "Reset session recovery" }).waitFor();
    await p.evaluate(() =>
      window.fixture.navigate("/users/43/reset-credential"),
    );
    await settle(p);
    assert.equal(
      await p.getByLabel("Current administrator password").count(),
      0,
    );
    await p.evaluate(() => (window.fixture.s.auth = { network: true }));
    await p.getByRole("button", { name: "Check session", exact: true }).click();
    await settle(p);
    assert.equal(
      await p.getByLabel("Current administrator password").count(),
      0,
    );
    await p.evaluate(
      () => (window.fixture.s.auth = { body: window.fixture.session }),
    );
    await p.getByRole("button", { name: "Check session", exact: true }).click();
    await p.getByLabel("Current administrator password").waitFor();
    assert.equal(
      (await calls(p)).filter((c) => c.url.endsWith("/reset-credential"))
        .length,
      1,
    );
  }));
for (const change of ["target", "session", "unmount"])
  test("late secret hidden after " + change, () =>
    run({ reset: { defer: "reset" } }, async (p) => {
      await prepare(p);
      await confirm(p);
      await settle(p);
      if (change === "session")
        await p.evaluate(() => window.fixture.replace());
      else
        await p.evaluate(
          (path) => window.fixture.navigate(path),
          change === "target" ? "/users/43/reset-credential" : "/profile",
        );
      await settle(p);
      await p.evaluate(
        ({ secret, next }) =>
          window.fixture.finish("reset", {
            body: {
              temporaryCredential: {
                password: secret,
                expiresAtUtc: "2030-01-01T02:00:00Z",
              },
            },
            headers: { ETag: next },
          }),
        { secret, next },
      );
      await settle(p);
      assert.ok(!(await p.locator("body").innerText()).includes(secret));
      assert.equal(
        await p
          .getByRole("region", { name: "Reset credential handoff" })
          .count(),
        0,
      );
    }),
  );
for (const s of [{ restricted: true }, { auth: { status: 401 } }])
  test("unauthorized/restricted no reset or proof", () =>
    run(s, async (p) => {
      await settle(p);
      assert.equal(
        (await calls(p)).filter((c) => c.url !== "/api/auth/session").length,
        0,
      );
    }));
test("self target no proof or reset", () =>
  run(
    {},
    async (p) => {
      await p.getByRole("alert").waitFor();
      assert.equal(
        (await calls(p)).filter((c) => c.url !== "/api/auth/session").length,
        0,
      );
    },
    "/users/7/reset-credential",
  ));

test("account detail link opens real reset route without treating read as reset authority", () =>
  run(
    {},
    async (p) => {
      await p
        .getByRole("link", { name: "Reset credential", exact: true })
        .click();
      await p.getByLabel("Current administrator password").waitFor();
      assert.equal(
        (await calls(p)).filter((c) => c.method === "POST").length,
        0,
      );
    },
    "/users/42",
  ));

test("clipboard rejection retains manual handoff without leaking exception", () =>
  run({ clipboardFailure: true }, async (p) => {
    await prepare(p);
    await confirm(p);
    await p
      .getByRole("heading", { name: "Credential reset confirmed" })
      .waitFor();
    await p.getByRole("button", { name: "Copy password" }).click();
    await p
      .getByText("Clipboard unavailable. Use a secure manual handoff.")
      .waitFor();
    await p.getByRole("button", { name: "Reveal password" }).click();
    assert.equal(await p.getByLabel("Temporary password").innerText(), secret);
  }));
test("wrong administrator password does not invalidate session or load target", () =>
  run(
    {
      proof: {
        status: 401,
        body: {
          status: 401,
          code: "INVALID_CREDENTIALS",
          title: "INVALID_CREDENTIALS",
        },
      },
    },
    async (p) => {
      await p.getByLabel("Current administrator password").fill("wrong");
      await p
        .getByRole("button", { name: "Verify administrator and load account" })
        .click();
      await p.getByRole("alert").waitFor();
      assert.equal(
        await p.getByRole("main", { name: "Reset session recovery" }).count(),
        0,
      );
      assert.equal(
        await p.getByLabel("Current administrator password").inputValue(),
        "",
      );
      assert.equal(
        (await calls(p)).filter((c) => c.url.includes("/api/users/")).length,
        0,
      );
    },
  ));
