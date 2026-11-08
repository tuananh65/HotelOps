import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";

const secret = "PRIVATE-PROFILE-ERROR";
const token = "PRIVATE-CSRF-TOKEN";
const tag = '"ABCDEF0123456789"',
  nextTag = '"ABCDEF0123456790"';
const profile = (id = 42, fullName = "Server Employee") => ({
  id,
  fullName,
  email: "server@example.test",
  phone: "123",
});
const ok = (id = 42, etag = tag, name = "Server Employee") => ({
  body: profile(id, name),
  headers: { ETag: etag },
});
const problem = (status, fields = {}) => ({
  status,
  body: { status, title: secret, detail: secret, traceId: token, ...fields },
});
const session = (restricted = false, id = "7") => ({
  user: { id, username: "manager", name: "Confirmed Manager" },
  expiresAt: "2026-11-07T12:00:00Z",
  absoluteExpiresAt: "2026-11-07T20:00:00Z",
  mustChangePassword: restricted,
  csrfToken: token,
});
const unknown =
  "Profile update outcome could not be confirmed. Changes may already have been saved. Do not retry this update automatically. Ask an authorized administrator to verify the current profile before making another change.";

const bundle = await build({
  stdin: {
    loader: "tsx",
    resolveDir: process.cwd(),
    contents: `
  import React, { StrictMode } from "react";
  import { createRoot } from "react-dom/client";
  import { BrowserRouter } from "react-router-dom";
  import { AuthProvider } from "./src/auth/AuthProvider";
  import { AuthRouteHost } from "./src/auth/AuthRouteHost";
  import { createAuthStore } from "./src/auth/authStore";
  import { createAuthApi } from "./src/auth/authApi";
  const s=window.scenario, calls=[], unexpected=[], waiting=new Map();
  const queues={
    "/api/auth/session":[...(s.sessions??[{body:${JSON.stringify(session())}}])],
    "/api/auth/csrf":[...(s.csrf??[{body:{csrfToken:${JSON.stringify(token)},headerName:"X-CSRF-TOKEN"}}])],
    "/api/users/42/profile":[...(s.reads??[${JSON.stringify(ok())}])],
    "/api/users/42":[...(s.patches??[${JSON.stringify(ok(42, nextTag, "Updated Employee"))}])],
    "/api/auth/login":[{body:${JSON.stringify(session(false, "8"))}}],
    ...s.responses
  };
  function respond(r){if(r.network)throw new Error(${JSON.stringify(secret)});if(r.timeout)throw new DOMException(${JSON.stringify(secret)},"TimeoutError");if(r.cancelled)throw new DOMException(${JSON.stringify(secret)},"AbortError");return new Response(r.raw??JSON.stringify(r.body),{status:r.status??200,headers:{"Content-Type":r.type??"application/json",...r.headers}});}
  const fetcher=async(url,init)=>{const c={url,method:init.method,body:init.body,headers:Object.fromEntries(new Headers(init.headers)),credentials:init.credentials,cache:init.cache,redirect:init.redirect,aborted:init.signal?.aborted??false};calls.push(c);init.signal?.addEventListener("abort",()=>{c.aborted=true;},{once:true});const r=queues[url]?.shift();if(!r){unexpected.push(url);throw new Error("Unexpected request");}if(r.deferred)return new Promise((resolve,reject)=>waiting.set(r.deferred,{resolve,reject}));return respond(r);};
  window.fetch=fetcher;const store=createAuthStore(createAuthApi(fetcher));const host=<AuthRouteHost/>;const root=createRoot(document.getElementById("root"));
  root.render(<StrictMode><BrowserRouter><AuthProvider store={store} fallback={host}>{host}</AuthProvider></BrowserRouter></StrictMode>);
  window.fixture={calls,unexpected,snapshot:()=>store.getSnapshot(),revalidate:()=>store.revalidate(),login:()=>store.login({username:"manager",password:"test-only"}),
    navigate(path){history.pushState(null,"",path);dispatchEvent(new PopStateEvent("popstate"));},
    finish(tag,r){const p=waiting.get(tag);if(!p)throw new Error("Missing deferred response");waiting.delete(tag);try{p.resolve(respond(r));}catch(e){p.reject(e);}},unmount(){root.unmount();}
  };
`,
  },
  bundle: true,
  write: false,
  metafile: true,
  format: "iife",
  platform: "browser",
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
async function settle(page) {
  await page.evaluate(
    () =>
      new Promise((resolve) =>
        requestAnimationFrame(() => requestAnimationFrame(resolve)),
      ),
  );
}
async function calls(page, url) {
  return page.evaluate(
    (url) => window.fixture.calls.filter((c) => !url || c.url === url),
    url,
  );
}
async function finish(page, label, reply) {
  await page.evaluate(
    ({ label, reply }) => window.fixture.finish(label, reply),
    { label, reply },
  );
  await settle(page);
}
async function navigate(page, path) {
  await page.evaluate((path) => window.fixture.navigate(path), path);
  await settle(page);
}
const body = (page) => page.locator("body").innerText();
const save = async (page) => {
  await page.getByRole("button", { name: "Save profile", exact: true }).click();
  await settle(page);
};
const ready = async (page) => {
  await page
    .getByRole("form", { name: "Update user profile", exact: true })
    .waitFor();
};
async function withUpdate(scenario, run, path = "/users/42/edit") {
  const page = await browser.newPage(),
    errors = [],
    logs = [],
    network = [];
  page.on("pageerror", (e) => errors.push(e.message));
  page.on("console", (m) => logs.push(m.text()));
  await page.route("**/*", (route) => {
    if (route.request().resourceType() === "document")
      return route.fulfill({
        contentType: "text/html",
        body: '<div id="root"></div>',
      });
    network.push(route.request().url());
    return route.abort();
  });
  try {
    await page.goto(`http://hotelops.test${path}`);
    await page.evaluate((scenario) => {
      window.scenario = scenario;
      window.storageTouches = 0;
      for (const key of ["localStorage", "sessionStorage", "indexedDB"])
        Object.defineProperty(window, key, {
          configurable: true,
          get() {
            window.storageTouches++;
            throw new Error("Forbidden storage");
          },
        });
    }, scenario);
    await page.addScriptTag({ content: bundle.outputFiles[0].text });
    await settle(page);
    await run(page);
    await settle(page);
    assert.deepEqual(errors, []);
    assert.deepEqual(network, []);
    assert.deepEqual(await page.evaluate(() => window.fixture.unexpected), []);
    assert.equal(await page.evaluate(() => window.storageTouches), 0);
    assert.ok(
      !logs.some((line) => line.includes(secret) || line.includes(token)),
    );
    assert.ok(!(await body(page)).includes(secret));
    assert.ok(!(await body(page)).includes(token));
    for (const c of await calls(page)) {
      assert.equal(c.credentials, "include");
      assert.equal(c.cache, "no-store");
      assert.equal(c.redirect, "error");
      assert.equal(c.headers.authorization, undefined);
      if (c.method === "PATCH") {
        assert.equal(c.headers["x-csrf-token"], token);
        assert.equal(c.headers["content-type"], "application/json");
        assert.ok(/^"[0-9A-F]{16}"$/.test(c.headers["if-match"]));
      }
    }
  } finally {
    await page.close();
  }
}

test("real import graph includes accepted profile client and excludes mock/prototype modules", () => {
  const inputs = Object.keys(bundle.metafile.inputs).map((p) =>
    p.replaceAll("\\", "/"),
  );
  for (const file of [
    "auth/authStore.ts",
    "auth/AuthProvider.tsx",
    "users/userProfileApi.ts",
    "users/UpdateUserFlowBoundary.tsx",
  ])
    assert.ok(inputs.some((p) => p.endsWith(file)));
  assert.ok(
    !inputs.some((p) =>
      /src\/(services(?:\/|\.ts)|ui\.tsx|i18n\.tsx|mocks\/|routes\/(routeConfig|ScreenIndex|DesignSystem))/.test(
        p,
      ),
    ),
  );
});
for (const [restricted, path] of [
  [false, "/login"],
  [true, "/change-password"],
])
  test(`guard redirects ${path} without management requests`, async () => {
    await withUpdate(
      { sessions: [restricted ? { body: session(true) } : problem(401)] },
      async (page) => {
        await page.waitForFunction((path) => location.pathname === path, path);
        assert.equal(
          (await calls(page)).filter((c) => c.url.startsWith("/api/users"))
            .length,
          0,
        );
      },
    );
  });
for (const reply of [{ deferred: "startup" }, problem(503)])
  test(`unconfirmed identity hides profile ${reply.status ?? "pending"}`, async () => {
    await withUpdate({ sessions: [reply] }, async (page) => {
      assert.equal(await page.locator("input").count(), 0);
      assert.equal(
        (await calls(page)).filter((c) => c.url.startsWith("/api/users"))
          .length,
        0,
      );
    });
  });
for (const id of ["0", "-1", "1.5", "9007199254740992", "bad"])
  test(`invalid route ID ${id} makes no management request`, async () => {
    await withUpdate(
      {},
      async (page) => {
        await page
          .getByRole("heading", { name: "Invalid user ID", exact: true })
          .waitFor();
        assert.equal(
          (await calls(page)).filter((c) => c.url.startsWith("/api/users"))
            .length,
          0,
        );
      },
      `/users/${id}/edit`,
    );
  });
for (const scope of ["GLOBAL", "BRANCH", "OWN"])
  test(`server-mocked ${scope} response drives management read; client infers no permission`, async () => {
    const id = scope === "OWN" ? 7 : 42;
    await withUpdate(
      { responses: { [`/api/users/${id}/profile`]: [ok(id)] } },
      async (page) => {
        await ready(page);
        assert.equal(
          await page.getByLabel("Full name", { exact: true }).inputValue(),
          "Server Employee",
        );
        assert.equal(await page.locator("input").count(), 3);
        assert.equal(
          (await calls(page)).filter((c) => c.method === "PATCH").length,
          0,
        );
        assert.equal((await calls(page, `/api/users/${id}/profile`)).length, 1);
        assert.ok(
          !(await calls(page)).some(
            (c) => c.url === "/api/users" || c.url.includes("effective-access"),
          ),
        );
      },
      `/users/${id}/edit`,
    );
  });
for (const reply of [
  { body: profile() },
  ok(42, 'W/"ABCDEF0123456789"'),
  ok(42, '"abcdef0123456789"'),
  ok(43),
  { body: { id: 42, fullName: "Incomplete" }, headers: { ETag: tag } },
])
  test(`invalid GET snapshot ${JSON.stringify(reply.headers ?? {})} never enables Save`, async () => {
    await withUpdate({ reads: [reply] }, async (page) => {
      assert.equal(
        await page.getByRole("form", { name: "Update user profile" }).count(),
        0,
      );
      assert.equal(
        (await calls(page)).filter((c) => c.method === "PATCH").length,
        0,
      );
      assert.match(await body(page), /could not be completed safely/);
    });
  });
for (const [reply, message] of [
  [problem(403), "does not confirm whether the account exists"],
  [problem(404), "could not find"],
  [problem(503), "could not be completed safely"],
  [{ network: true }, "could not be completed safely"],
])
  test(`management GET error ${reply.status ?? "network"} hides profile`, async () => {
    await withUpdate({ reads: [reply] }, async (page) => {
      assert.ok((await body(page)).includes(message));
      assert.equal(await page.locator("input").count(), 0);
    });
  });
test("GET 401 revalidation is bounded and recovery 200 requires explicit management reload", async () => {
  await withUpdate(
    {
      sessions: [{ body: session() }, { body: session() }],
      reads: [problem(401), ok()],
    },
    async (page) => {
      await page.waitForFunction(
        () =>
          window.fixture.calls.filter(
            (call) => call.url === "/api/auth/session",
          ).length === 2 &&
          window.fixture.snapshot().status === "AUTHENTICATED",
      );
      await settle(page);
      assert.equal((await calls(page, "/api/auth/session")).length, 2);
      assert.equal((await calls(page, "/api/users/42/profile")).length, 1);
      assert.equal(await page.locator("input").count(), 0);
      await page
        .getByRole("button", { name: "Reload profile", exact: true })
        .click();
      await ready(page);
      assert.equal((await calls(page, "/api/users/42/profile")).length, 2);
    },
  );
});
test("form validation sends no CSRF or PATCH; controls restricted to three fields", async () => {
  await withUpdate({}, async (page) => {
    await ready(page);
    await page.getByLabel("Full name", { exact: true }).fill(" ");
    await save(page);
    assert.match(await body(page), /Full name and email are required/);
    assert.equal((await calls(page, "/api/auth/csrf")).length, 0);
    assert.equal(
      (await calls(page)).filter((c) => c.method === "PATCH").length,
      0,
    );
    assert.deepEqual(
      await page
        .locator("input")
        .evaluateAll((nodes) => nodes.map((n) => n.name)),
      ["fullName", "email", "phone"],
    );
  });
});
for (const [field, value, expected] of [
  ["Full name", " Updated Employee ", { fullName: "Updated Employee" }],
  ["Email", " new@example.test ", { email: "new@example.test" }],
  ["Phone", "   ", { phone: null }],
])
  test(`PATCH only edited field ${field}, normalized with exact If-Match`, async () => {
    await withUpdate({}, async (page) => {
      await ready(page);
      await page.getByLabel(field, { exact: true }).fill(value);
      await save(page);
      const updates = (await calls(page)).filter((c) => c.method === "PATCH");
      assert.equal(updates.length, 1);
      assert.deepEqual(JSON.parse(updates[0].body), expected);
      assert.equal(updates[0].headers["if-match"], tag);
      assert.match(await body(page), /Profile update confirmed by the server/);
      assert.equal(
        await page.evaluate(() => location.pathname),
        "/users/42/edit",
      );
      assert.equal(
        await page.evaluate(() => window.fixture.snapshot().session.user.id),
        "7",
      );
    });
  });
test("double-submit and Enter while deferred issue one PATCH under StrictMode", async () => {
  await withUpdate(
    { csrf: [{ deferred: "csrf" }], patches: [{ deferred: "patch" }] },
    async (page) => {
      await ready(page);
      await page
        .getByLabel("Full name", { exact: true })
        .fill("Updated Employee");
      await page
        .getByRole("form", { name: "Update user profile" })
        .evaluate((form) => {
          form.dispatchEvent(
            new Event("submit", { bubbles: true, cancelable: true }),
          );
          form.dispatchEvent(
            new Event("submit", { bubbles: true, cancelable: true }),
          );
        });
      await settle(page);
      assert.equal((await calls(page, "/api/auth/csrf")).length, 1);
      assert.equal(
        (await calls(page)).filter((c) => c.method === "PATCH").length,
        0,
      );
      await page.getByLabel("Email", { exact: true }).press("Enter");
      await finish(page, "csrf", {
        body: { csrfToken: token, headerName: "X-CSRF-TOKEN" },
      });
      assert.equal(
        (await calls(page)).filter((c) => c.method === "PATCH").length,
        1,
      );
      await finish(page, "patch", ok(42, nextTag, "Updated Employee"));
      assert.equal(
        (await calls(page)).filter((c) => c.method === "PATCH").length,
        1,
      );
    },
  );
});
test("Enter initiates Save; no-op response may keep ETag unchanged", async () => {
  await withUpdate({ patches: [ok()] }, async (page) => {
    await ready(page);
    await page.getByLabel("Email", { exact: true }).press("Enter");
    await settle(page);
    assert.equal(
      (await calls(page)).filter((c) => c.method === "PATCH").length,
      1,
    );
    assert.match(await body(page), /Profile update confirmed/);
    assert.ok((await body(page)).includes(tag));
  });
});
test("confirmed response snapshot and new ETag are used by the next explicit Save", async () => {
  await withUpdate(
    {
      csrf: [
        { body: { csrfToken: token, headerName: "X-CSRF-TOKEN" } },
        { body: { csrfToken: token, headerName: "X-CSRF-TOKEN" } },
      ],
      patches: [
        ok(42, nextTag, "Updated Employee"),
        ok(42, nextTag, "Updated Employee"),
      ],
    },
    async (page) => {
      await ready(page);
      await page
        .getByLabel("Full name", { exact: true })
        .fill("Updated Employee");
      await save(page);
      assert.equal(
        await page.getByLabel("Full name", { exact: true }).inputValue(),
        "Updated Employee",
      );
      await page.getByLabel("Phone", { exact: true }).fill("456");
      await save(page);
      const updates = (await calls(page)).filter((c) => c.method === "PATCH");
      assert.equal(updates.length, 2);
      assert.equal(updates[1].headers["if-match"], nextTag);
      assert.deepEqual(JSON.parse(updates[1].body), { phone: "456" });
    },
  );
});
test("412 blocks old token; explicit Reload discards draft without replay", async () => {
  await withUpdate(
    {
      reads: [ok(), ok(42, nextTag, "Concurrent Editor")],
      patches: [problem(412)],
    },
    async (page) => {
      await ready(page);
      await page.getByLabel("Full name", { exact: true }).fill("Old Draft");
      await save(page);
      assert.match(await body(page), /Reload profile, review/);
      assert.equal(
        await page.getByRole("form", { name: "Update user profile" }).count(),
        0,
      );
      assert.equal((await calls(page, "/api/users/42/profile")).length, 1);
      await page
        .getByRole("button", { name: "Reload profile", exact: true })
        .click();
      await ready(page);
      assert.equal(
        await page.getByLabel("Full name", { exact: true }).inputValue(),
        "Concurrent Editor",
      );
      assert.ok((await body(page)).includes(nextTag));
      assert.equal(
        (await calls(page)).filter((c) => c.method === "PATCH").length,
        1,
      );
    },
  );
});
for (const [details, message] of [
  [{ title: "Email is already in use" }, "Email is already in use"],
  [{ title: "LAST_DURABLE_ADMIN" }, "safe administrator access"],
  [{ code: "CSRF_VALIDATION_FAILED" }, "security check was rejected"],
])
  test(`safe rejected PATCH ${message}`, async () => {
    await withUpdate(
      { patches: [problem(details.code ? 400 : 409, details)] },
      async (page) => {
        await ready(page);
        await save(page);
        assert.ok((await body(page)).includes(message));
        assert.equal(
          (await calls(page)).filter((c) => c.method === "PATCH").length,
          1,
        );
      },
    );
  });
for (const status of [401, 403])
  test(`PATCH ${status} removes editable data without replay`, async () => {
    await withUpdate(
      {
        sessions: [{ body: session() }, problem(401)],
        patches: [problem(status)],
      },
      async (page) => {
        await ready(page);
        await save(page);
        if (status === 401)
          await page.waitForFunction(() => location.pathname === "/login");
        else assert.match(await body(page), /Access denied/);
        assert.equal(
          await page
            .locator("input")
            .filter({ has: page.locator('[value="Server Employee"]') })
            .count(),
          0,
        );
        assert.equal(
          await page.getByRole("form", { name: "Update user profile" }).count(),
          0,
        );
        assert.equal(
          (await calls(page)).filter((c) => c.method === "PATCH").length,
          1,
        );
        assert.equal(
          (await calls(page, "/api/auth/session")).length,
          status === 401 ? 2 : 1,
        );
      },
    );
  });
for (const reply of [
  problem(500),
  problem(503),
  { network: true },
  { timeout: true },
  { cancelled: true },
  { body: profile(), headers: { ETag: "bad" } },
])
  test(`unknown PATCH ${reply.status ?? (reply.network ? "network" : reply.timeout ? "timeout" : reply.cancelled ? "abort" : "malformed")}`, async () => {
    await withUpdate({ patches: [reply] }, async (page) => {
      await ready(page);
      await save(page);
      assert.ok((await body(page)).includes(unknown));
      assert.equal(
        await page
          .getByRole("button", { name: /Retry Save|Save profile/ })
          .count(),
        0,
      );
      assert.equal(
        (await calls(page)).filter((c) => c.method === "PATCH").length,
        1,
      );
    });
  });
test("explicit reconciliation GET never certifies unknown PATCH or unlocks Save", async () => {
  await withUpdate(
    {
      reads: [ok(), ok(42, nextTag, "Current Profile")],
      patches: [{ network: true }],
    },
    async (page) => {
      await ready(page);
      await save(page);
      await page
        .getByRole("button", { name: "Reload profile", exact: true })
        .click();
      await ready(page);
      assert.equal(
        await page
          .getByRole("button", { name: "Save profile", exact: true })
          .isDisabled(),
        true,
      );
      assert.ok((await body(page)).includes(unknown));
      assert.match(
        await body(page),
        /does not prove that the earlier update committed/,
      );
      assert.ok(
        !(await body(page)).includes("Profile update confirmed by the server"),
      );
      assert.equal(
        (await calls(page)).filter((c) => c.method === "PATCH").length,
        1,
      );
    },
  );
});
test("refresh 403 drops the previous profile and its editable draft", async () => {
  await withUpdate({ reads: [ok(), problem(403)] }, async (page) => {
    await ready(page);
    await page.getByLabel("Full name", { exact: true }).fill("Private Draft");
    await page
      .getByRole("button", { name: "Reload profile", exact: true })
      .click();
    await settle(page);
    assert.equal(await page.locator("input").count(), 0);
    assert.ok(!(await body(page)).includes("Private Draft"));
    assert.ok(!(await body(page)).includes("Server Employee"));
  });
});
test("target change aborts GET and masks late previous target response", async () => {
  await withUpdate(
    {
      reads: [{ deferred: "read" }],
      responses: { "/api/users/43/profile": [ok(43, tag, "Second Target")] },
    },
    async (page) => {
      await navigate(page, "/users/43/edit");
      await ready(page);
      await finish(page, "read", ok(42, tag, "Old Target"));
      assert.equal(
        await page.getByLabel("Full name", { exact: true }).inputValue(),
        "Second Target",
      );
      assert.equal(
        (await calls(page, "/api/users/42/profile"))[0].aborted,
        true,
      );
      assert.ok(!(await body(page)).includes("Old Target"));
    },
  );
});
for (const nextId of ["7", "8"])
  test(`session replacement ${nextId} aborts pending PATCH and preserves unknown`, async () => {
    await withUpdate(
      {
        sessions: [{ body: session() }, { deferred: "check" }],
        patches: [{ deferred: "patch" }],
      },
      async (page) => {
        await ready(page);
        await save(page);
        await page.evaluate(() => {
          void window.fixture.revalidate();
        });
        await settle(page);
        assert.equal(await page.locator("input").count(), 0);
        await finish(page, "patch", ok(42, nextTag, "Late Employee"));
        await finish(page, "check", { body: session(false, nextId) });
        assert.ok((await body(page)).includes(unknown));
        assert.equal(
          await page.getByRole("form", { name: "Update user profile" }).count(),
          0,
        );
        assert.equal(
          (await calls(page)).filter((c) => c.method === "PATCH")[0].aborted,
          true,
        );
        assert.equal((await calls(page, "/api/users/42/profile")).length, 1);
      },
    );
  });
test("identity replacement aborts old GET even for the same user ID", async () => {
  await withUpdate(
    {
      sessions: [{ body: session() }, { body: session() }],
      reads: [{ deferred: "old" }, ok(42, nextTag, "Fresh Identity")],
    },
    async (page) => {
      await page.evaluate(() => window.fixture.revalidate());
      await ready(page);
      await finish(page, "old", ok(42, tag, "Old Identity"));
      assert.equal(
        await page.getByLabel("Full name", { exact: true }).inputValue(),
        "Fresh Identity",
      );
      assert.equal(
        (await calls(page, "/api/users/42/profile"))[0].aborted,
        true,
      );
    },
  );
});
test("route unmount aborts PATCH and late success cannot navigate or publish data", async () => {
  await withUpdate({ patches: [{ deferred: "patch" }] }, async (page) => {
    await ready(page);
    await save(page);
    await navigate(page, "/profile");
    await finish(page, "patch", ok(42, nextTag, "Late Employee"));
    assert.equal(await page.evaluate(() => location.pathname), "/profile");
    assert.ok(!(await body(page)).includes("Late Employee"));
    assert.equal(
      (await calls(page)).filter((c) => c.method === "PATCH")[0].aborted,
      true,
    );
  });
});
test("pending PATCH followed by unavailable session never becomes confirmed failure", async () => {
  await withUpdate(
    {
      sessions: [{ body: session() }, problem(503), { body: session() }],
      patches: [{ deferred: "patch" }],
    },
    async (page) => {
      await ready(page);
      await save(page);
      await page.evaluate(() => window.fixture.revalidate());
      await settle(page);
      assert.equal(await page.locator("input").count(), 0);
      await finish(page, "patch", ok());
      await page
        .getByRole("button", { name: "Check session", exact: true })
        .click();
      await settle(page);
      assert.ok((await body(page)).includes(unknown));
      assert.ok(!(await body(page)).includes("update failed"));
      assert.equal(
        (await calls(page)).filter((c) => c.method === "PATCH").length,
        1,
      );
    },
  );
});

test("CSRF bootstrap failure leaves a not-dispatched update and never PATCHes", async () => {
  await withUpdate({ csrf: [problem(503)] }, async (page) => {
    await ready(page);
    await save(page);
    assert.match(await body(page), /No profile update request was sent/);
    assert.equal(
      (await calls(page)).filter((c) => c.method === "PATCH").length,
      0,
    );
  });
});

test("route unmount aborts GET; late data cannot appear on Profile", async () => {
  await withUpdate({ reads: [{ deferred: "read" }] }, async (page) => {
    await navigate(page, "/profile");
    await finish(page, "read", ok(42, tag, "Late Private Profile"));
    assert.equal((await calls(page, "/api/users/42/profile"))[0].aborted, true);
    assert.equal(await page.evaluate(() => location.pathname), "/profile");
    assert.ok(!(await body(page)).includes("Late Private Profile"));
  });
});

test("target change during PATCH cannot publish old result into new target", async () => {
  await withUpdate(
    {
      patches: [{ deferred: "patch" }],
      responses: { "/api/users/43/profile": [ok(43, tag, "New Target")] },
    },
    async (page) => {
      await ready(page);
      await save(page);
      await navigate(page, "/users/43/edit");
      await ready(page);
      await finish(page, "patch", ok(42, nextTag, "Old Mutation"));
      assert.equal(
        await page.getByLabel("Full name", { exact: true }).inputValue(),
        "New Target",
      );
      assert.ok(
        !(await body(page)).includes("Profile update confirmed by the server"),
      );
      assert.equal(
        (await calls(page)).filter((c) => c.method === "PATCH")[0].aborted,
        true,
      );
    },
  );
});

test("unknown reconciliation is not repeated automatically by session replacement", async () => {
  await withUpdate(
    {
      sessions: [{ body: session() }, { body: session() }],
      reads: [ok(), ok(42, nextTag)],
      patches: [{ network: true }],
    },
    async (page) => {
      await ready(page);
      await save(page);
      await page
        .getByRole("button", { name: "Reload profile", exact: true })
        .click();
      await ready(page);
      await page.evaluate(() => window.fixture.revalidate());
      await settle(page);
      assert.ok((await body(page)).includes(unknown));
      assert.equal((await calls(page, "/api/users/42/profile")).length, 2);
      assert.equal(
        await page.getByRole("form", { name: "Update user profile" }).count(),
        0,
      );
    },
  );
});
