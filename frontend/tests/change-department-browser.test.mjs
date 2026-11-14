import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
import { existsSync } from "node:fs";
const tag = '"0011223344556677"',
  next = '"0011223344556678"';
const session = {
  user: { id: "7", username: "actor", name: "Actor" },
  expiresAt: "2099-01-01T00:00:00Z",
  absoluteExpiresAt: "2099-01-02T00:00:00Z",
  mustChangePassword: false,
  csrfToken: "PRIVATE-CSRF",
};
const result = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `
import React from'react';import{createRoot}from'react-dom/client';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthStore}from'./src/auth/authStore';import{createAuthApi}from'./src/auth/authApi';import{ChangeDepartmentFlow}from'./src/users/ChangeDepartmentFlow';
const s=window.scenario,calls=[],waiting=new Map();const make=r=>{const response=new Response(JSON.stringify(r.body??{}),{status:r.status??200,headers:{'Content-Type':'application/json',ETag:${JSON.stringify(next)},...r.headers}});if(r.unreadable)response.text=async()=>{throw Error("PRIVATE-ERROR")};return response;};
window.fetch=async(url,init)=>{calls.push({url,...init});let r;if(url==='/api/auth/session')r=s.auth??{body:${JSON.stringify(session)}};else if(url==='/api/auth/csrf')r=s.csrf??{body:{headerName:'X-CSRF-TOKEN',csrfToken:'PRIVATE-CSRF'}};else if(init.method==='GET')r=s.read??{body:{userId:Number(url.split('/')[3]),branchId:1,departmentId:2},headers:{ETag:${JSON.stringify(tag)}}};else r=s.mutation??{body:{userId:42,branchId:1,previousDepartmentId:2,departmentId:3}};if(r.deferred)return new Promise(resolve=>waiting.set(r.deferred,resolve));return make(r);};
const store=createAuthStore(createAuthApi(window.fetch)),root=createRoot(document.getElementById('root'));let target=42;const render=()=>{const app=<ChangeDepartmentFlow userId={target} dispatchEnabled={s.enabled===true}/>;root.render(<AuthProvider store={store} fallback={app}>{app}</AuthProvider>);};render();window.fixture={calls,scenario:s,revalidate:()=>store.revalidate(),target:id=>{target=id;render();},finish:(k,r)=>waiting.get(k)(make(r)),unmount:()=>root.unmount()};
`,
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
            throw Error("Storage forbidden");
          },
        });
    }, s);
    await p.addScriptTag({ content: result.outputFiles[0].text });
    await fn(p);
    assert.deepEqual(errors, []);
    assert.equal(
      (await p.locator("body").innerText()).includes("PRIVATE-CSRF"),
      false,
    );
    assert.equal(
      (await p.locator("body").innerText()).includes("PRIVATE-ERROR"),
      false,
    );
  } finally {
    await p.close();
  }
}
async function review(p) {
  await p.getByLabel("Destination Department ID").fill("3");
  await p.getByRole("button", { name: "Review change", exact: true }).click();
}
const puts = (p) =>
  p.evaluate(
    () => window.fixture.calls.filter((c) => c.method === "PUT").length,
  );
test("default OFF allows review but never dispatch", () =>
  run({}, async (p) => {
    await review(p);
    assert.equal(
      await p
        .getByRole("button", { name: "Confirm department change" })
        .isDisabled(),
      true,
    );
    assert.equal(await puts(p), 0);
  }));
test("required destination, same destination and unsafe IDs cannot review", () =>
  run({}, async (p) => {
    const b = p.getByRole("button", { name: "Review change", exact: true });
    await b.waitFor();
    for (const value of ["", "2", "0", "-1", "3.5", "9007199254740992"]) {
      await p.getByLabel("Destination Department ID").fill(value);
      assert.equal(await b.isDisabled(), true);
    }
  }));
test("Review then two synchronous confirms yields exactly one PUT and confirmed state", () =>
  run({ enabled: true }, async (p) => {
    await review(p);
    assert.equal(await puts(p), 0);
    await p
      .getByRole("button", { name: "Confirm department change" })
      .evaluate((b) => {
        b.click();
        b.click();
      });
    await p
      .getByText("Department change confirmed. Target sessions revoked.")
      .waitFor();
    assert.equal(await puts(p), 1);
    assert.equal(
      await p.getByRole("region", { name: "Server scope" }).count(),
      0,
    );
  }));
for (const status of [401, 403, 409, 412, 503])
  test("sanitized failure " + status, () =>
    run(
      {
        enabled: true,
        mutation: {
          status,
          body: {
            status,
            code: "ROLE_SCOPE_CONFLICT",
            title: "ROLE_SCOPE_CONFLICT",
            detail: "PRIVATE-ERROR",
          },
        },
      },
      async (p) => {
        await review(p);
        await p
          .getByRole("button", { name: "Confirm department change" })
          .click();
        await p.getByRole("alert").waitFor();
        assert.equal(await puts(p), 1);
        assert.equal(
          await p
            .getByRole("button", { name: "Confirm department change" })
            .count(),
          0,
        );
        assert.equal(
          await p.getByRole("region", { name: "Server scope" }).count(),
          0,
        );
        if (status === 401)
          await p.getByRole("button", { name: "Check session" }).waitFor();
      },
    ),
  );
for (const auth of [
  { status: 401 },
  { body: { ...session, mustChangePassword: true } },
])
  test(
    "anonymous/restricted sends no department request " + JSON.stringify(auth),
    () =>
      run({ auth, enabled: true }, async (p) => {
        await p.getByText("Confirm an unrestricted session.").waitFor();
        assert.equal(await puts(p), 0);
        assert.equal(await p.evaluate(() => window.fixture.calls.length), 1);
      }),
  );
test("late mutation cannot restore identity after session invalidation", () =>
  run({ enabled: true, mutation: { deferred: "put" } }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm department change" }).click();
    await p.getByText("Department change pending...").waitFor();
    await p.evaluate(() => {
      window.fixture.scenario.auth = { status: 401 };
      void window.fixture.revalidate();
    });
    await p.getByText("Confirm an unrestricted session.").waitFor();
    await p.evaluate(() =>
      window.fixture.finish("put", {
        body: {
          userId: 42,
          branchId: 1,
          previousDepartmentId: 2,
          departmentId: 3,
        },
      }),
    );
    assert.equal(
      await p
        .getByText("Department change confirmed. Target sessions revoked.")
        .count(),
      0,
    );
  }));
test("target replacement hides previous review and ignores late PUT", () =>
  run({ enabled: true, mutation: { deferred: "put" } }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm department change" }).click();
    await p.getByText("Department change pending...").waitFor();
    await p.evaluate(() => window.fixture.target(99));
    await p.getByText("User 99; Branch 1; Department 2").waitFor();
    await p.evaluate(() =>
      window.fixture.finish("put", {
        body: {
          userId: 42,
          branchId: 1,
          previousDepartmentId: 2,
          departmentId: 3,
        },
      }),
    );
    assert.equal(
      await p.getByText("User 42; Branch 1; Department 2").count(),
      0,
    );
    assert.equal(
      await p
        .getByText("Department change confirmed. Target sessions revoked.")
        .count(),
      0,
    );
  }));
test("unmount ignores late response", () =>
  run({ enabled: true, mutation: { deferred: "put" } }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm department change" }).click();
    await p.getByText("Department change pending...").waitFor();
    await p.evaluate(() => {
      window.fixture.unmount();
      window.fixture.finish("put", {
        body: {
          userId: 42,
          branchId: 1,
          previousDepartmentId: 2,
          departmentId: 3,
        },
      });
    });
    assert.equal(
      await p
        .getByText("Department change confirmed. Target sessions revoked.")
        .count(),
      0,
    );
  }));

test("unreadable CSRF401 exposes explicit session recovery without PUT or retry", () =>
  run({ enabled: true, csrf: { status: 401, unreadable: true } }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm department change" }).click();
    await p
      .getByText("Session invalid. Revalidate before a new review.")
      .waitFor();
    await p.getByRole("button", { name: "Check session" }).waitFor();
    assert.equal(
      await p.getByText("User 42; Branch 1; Department 2").count(),
      0,
    );
    assert.equal(
      await p.evaluate(
        () => window.fixture.calls.filter((c) => c.method === "PUT").length,
      ),
      0,
    );
    await p.getByRole("button", { name: "Check session" }).click();
    await p.getByText("User 42; Branch 1; Department 2").waitFor();
    assert.equal(
      await p.evaluate(
        () => window.fixture.calls.filter((c) => c.method === "PUT").length,
      ),
      0,
    );
  }));
