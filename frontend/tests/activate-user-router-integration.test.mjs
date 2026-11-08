import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";

const secret = "PRIVATE-ACTIVATION-ERROR";
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
  "Account activation outcome could not be confirmed. The account may already have been activated. Do not retry automatically. Ask an authorized administrator to verify the current account status before making another change.";

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
    "/api/users/42/activate":[...(s.patches??[{body:{userId:42,accountStatus:"ACTIVE"},headers:{ETag:${JSON.stringify(nextTag)}}}])],
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

test("real activation graph excludes mock Shell and includes accepted clients", () => {
  const paths = Object.keys(bundle.metafile.inputs).map((p) =>
    p.replaceAll("\\", "/"),
  );
  for (const path of [
    "auth/authStore.ts",
    "auth/AuthProvider.tsx",
    "users/userProfileApi.ts",
    "users/userActivationApi.ts",
    "users/ActivateUserFlowBoundary.tsx",
  ])
    assert.ok(paths.some((p) => p.endsWith(path)));
  assert.ok(
    !paths.some((p) =>
      /src\/(services(?:\/|\.ts)|ui\.tsx|i18n\.tsx|mocks\/|screens\/users\/)/.test(
        p,
      ),
    ),
  );
});

for (const [restricted, destination] of [
  [false, "/login"],
  [true, "/change-password"],
])
  test(`guard redirects ${destination} without user API`, async () => {
    await withActivation(
      { sessions: [restricted ? { body: session(true) } : problem(401)] },
      async (p) => {
        await p.waitForFunction(
          (path) => location.pathname === path,
          destination,
        );
        assert.equal(
          (await calls(p)).filter((c) => c.url.startsWith("/api/users")).length,
          0,
        );
      },
    );
  });
for (const reply of [{ deferred: "startup" }, problem(503)])
  test(`unconfirmed startup ${reply.status ?? "pending"} has no form or management request`, async () => {
    await withActivation({ sessions: [reply] }, async (p) => {
      assert.equal(await p.locator("textarea").count(), 0);
      assert.equal(
        (await calls(p)).filter((c) => c.url.startsWith("/api/users")).length,
        0,
      );
    });
  });
for (const id of ["0", "-1", "1.5", "9007199254740992", "bad"])
  test(`invalid route ID ${id} cannot GET or PATCH`, async () => {
    await withActivation(
      {},
      async (p) => {
        await p
          .getByRole("heading", { name: "Invalid user ID", exact: true })
          .waitFor();
        assert.equal(
          (await calls(p)).filter((c) => c.url.startsWith("/api/users")).length,
          0,
        );
      },
      `/users/${id}/activate`,
    );
  });
for (const etag of [
  undefined,
  "",
  'W/"ABCDEF0123456789"',
  '"abcdef0123456789"',
])
  test(`invalid management ETag ${String(etag)} cannot enable activation`, async () => {
    await withActivation(
      {
        reads: [
          {
            body: profile(),
            headers: etag === undefined ? {} : { ETag: etag },
          },
        ],
      },
      async (p) => {
        await p.getByRole("alert").waitFor();
        assert.equal(await p.locator("textarea").count(), 0);
        assert.equal(
          (await calls(p)).filter((c) => c.method === "PATCH").length,
          0,
        );
      },
    );
  });

test("server snapshot requires no USER_VIEW; explicit confirmation and exact CSRF/If-Match/body", async () => {
  await withActivation({}, async (p) => {
    await ready(p);
    const text = await body(p);
    assert.ok(text.includes("Server Employee"));
    assert.ok(text.includes("server@example.test"));
    assert.ok(
      text.includes("Loading a profile does not grant activation permission"),
    );
    assert.ok(!text.includes("INACTIVE"));
    assert.equal(
      (await calls(p)).filter(
        (c) => c.url === "/api/users" || c.url.includes("access-review"),
      ).length,
      0,
    );
    await review(p, "  Account approved  ");
    assert.equal(
      (await calls(p)).filter((c) => c.method === "PATCH").length,
      0,
    );
    await confirm(p);
    await p.getByText(/Activation confirmed by the server/).waitFor();
    const mutations = (await calls(p)).filter((c) => c.method === "PATCH");
    assert.equal(mutations.length, 1);
    assert.equal(mutations[0].url, "/api/users/42/activate");
    assert.equal(mutations[0].headers["if-match"], tag);
    assert.equal(mutations[0].body, '{"reason":"Account approved"}');
    assert.equal((await calls(p, "/api/auth/csrf")).length, 1);
    assert.ok((await body(p)).includes("accountStatus: ACTIVE"));
    assert.ok(!(await body(p)).includes("Employee can now log in"));
    assert.equal(new URL(p.url()).pathname, "/users/42/activate");
    assert.equal(
      (await p.evaluate(() => window.fixture.snapshot())).session.user.id,
      "7",
    );
  });
});
for (const reason of [
  "",
  "123456789",
  "\u0085123456789\u0085",
  "界".repeat(5500),
])
  test(`reason boundary validation ${reason.length} code units`, async () => {
    await withActivation({}, async (p) => {
      await ready(p);
      await review(p, reason);
      await p.getByRole("alert").waitFor();
      assert.equal(
        await p
          .getByRole("button", { name: "Confirm activation", exact: true })
          .count(),
        0,
      );
      assert.equal(
        (await calls(p)).filter((c) => c.method === "PATCH").length,
        0,
      );
    });
  });
for (const reason of ["😀😀😀😀😀", "a".repeat(16371)])
  test(`valid Unicode/serialized-body boundary ${reason.length}`, async () => {
    await withActivation({}, async (p) => {
      await ready(p);
      await review(p, reason);
      await confirm(p);
      await p.getByText(/Activation confirmed by the server/).waitFor();
      assert.equal(
        (await calls(p)).filter((c) => c.method === "PATCH")[0].body,
        JSON.stringify({ reason }),
      );
    });
  });
test("StrictMode, Enter and double confirmation send at most one PATCH", async () => {
  await withActivation({ patches: [{ deferred: "patch" }] }, async (p) => {
    await ready(p);
    assert.equal((await calls(p, "/api/users/42/profile")).length, 1);
    await p
      .getByRole("textbox", { name: "Reason", exact: true })
      .fill("Account approved");
    await p
      .getByRole("textbox", { name: "Reason", exact: true })
      .press("Enter");
    assert.equal(
      (await calls(p)).filter((c) => c.method === "PATCH").length,
      0,
    );
    await review(p);
    await p
      .getByRole("button", { name: "Confirm activation", exact: true })
      .evaluate((b) => {
        b.click();
        b.click();
      });
    await settle(p);
    assert.equal(
      (await calls(p)).filter((c) => c.method === "PATCH").length,
      1,
    );
    await finish(p, "patch", {
      body: { userId: 42, accountStatus: "ACTIVE" },
      headers: { ETag: nextTag },
    });
    await p.getByText(/Activation confirmed by the server/).waitFor();
    assert.equal(
      (await calls(p)).filter((c) => c.method === "PATCH").length,
      1,
    );
  });
});
test("editing after review invalidates confirmation", async () => {
  await withActivation({}, async (p) => {
    await ready(p);
    await review(p);
    await p
      .getByRole("textbox", { name: "Reason", exact: true })
      .fill("Changed reason");
    assert.equal(
      await p
        .getByRole("button", { name: "Confirm activation", exact: true })
        .count(),
      0,
    );
    assert.equal(
      (await calls(p)).filter((c) => c.method === "PATCH").length,
      0,
    );
  });
});
for (const [status, code] of [
  [400, "CSRF_VALIDATION_FAILED"],
  [400, "VALIDATION_FAILED"],
  [403, "ACTIVATION_NOT_ALLOWED"],
  [404, "USER_NOT_FOUND"],
  [409, "INVALID_STATUS_TRANSITION"],
  [428, "IF_MATCH_REQUIRED"],
  [413, "REQUEST_TOO_LARGE"],
  [415, "UNSUPPORTED_MEDIA_TYPE"],
])
  test(`rejected ${status}/${code} is sanitized and never replayed`, async () => {
    await withActivation(
      { patches: [problem(status, { code })] },
      async (p) => {
        await ready(p);
        await review(p);
        await confirm(p);
        await p.getByRole("alert").waitFor();
        assert.equal(
          (await calls(p)).filter((c) => c.method === "PATCH").length,
          1,
        );
        assert.equal((await calls(p, "/api/auth/session")).length, 1);
        if ([403, 404, 428].includes(status)) {
          assert.ok(!(await body(p)).includes("server@example.test"));
          assert.equal(await p.locator("textarea").count(), 0);
        }
        if (status === 403)
          assert.ok(
            (await body(p)).includes(
              "does not confirm whether the account exists",
            ),
          );
        if (status === 409) {
          assert.ok(
            (await body(p)).includes(
              "current account status has not been confirmed",
            ),
          );
          assert.ok(!(await body(p)).includes("accountStatus: ACTIVE"));
        }
      },
    );
  });
test("412 requires explicit Reload, fresh target review and new confirmation", async () => {
  await withActivation(
    {
      reads: [ok(), ok(42, nextTag, "Changed Server Name")],
      patches: [
        problem(412),
        {
          body: { userId: 42, accountStatus: "ACTIVE" },
          headers: { ETag: '"ABCDEF0123456791"' },
        },
      ],
      csrf: [
        { body: { csrfToken: token, headerName: "X-CSRF-TOKEN" } },
        { body: { csrfToken: token, headerName: "X-CSRF-TOKEN" } },
      ],
    },
    async (p) => {
      await ready(p);
      await review(p);
      await confirm(p);
      await p.getByRole("alert").waitFor();
      assert.equal((await calls(p, "/api/users/42/profile")).length, 1);
      assert.equal(await p.locator("textarea").count(), 0);
      await p
        .getByRole("button", { name: "Reload profile", exact: true })
        .click();
      await ready(p);
      assert.ok((await body(p)).includes("Changed Server Name"));
      assert.equal(
        await p
          .getByRole("textbox", { name: "Reason", exact: true })
          .inputValue(),
        "",
      );
      assert.equal(
        (await calls(p)).filter((c) => c.method === "PATCH").length,
        1,
      );
      await review(p, "Reviewed fresh target");
      await confirm(p);
      await p.getByText(/Activation confirmed by the server/).waitFor();
      assert.equal(
        (await calls(p)).filter((c) => c.method === "PATCH")[1].headers[
          "if-match"
        ],
        nextTag,
      );
    },
  );
});
for (const recovery of [{ body: session() }, problem(401), problem(503)])
  test(`401 bounded recovery ${recovery.status ?? 200} never replays PATCH`, async () => {
    await withActivation(
      { patches: [problem(401)], sessions: [{ body: session() }, recovery] },
      async (p) => {
        await ready(p);
        await review(p);
        await confirm(p);
        await p.waitForFunction(
          () =>
            window.fixture.calls.filter((c) => c.url === "/api/auth/session")
              .length === 2,
        );
        await settle(p);
        assert.equal(
          (await calls(p)).filter((c) => c.method === "PATCH").length,
          1,
        );
        assert.equal((await calls(p, "/api/users/42/profile")).length, 1);
        assert.equal(await p.locator("textarea").count(), 0);
      },
    );
  });
for (const reply of [
  problem(500),
  problem(503),
  { network: true },
  { timeout: true },
  { cancelled: true },
  { raw: "{", headers: { ETag: nextTag } },
])
  test(`unknown outcome ${JSON.stringify(reply)} stays locked after profile reconciliation`, async () => {
    await withActivation(
      { patches: [reply], reads: [ok(), ok(42, nextTag)] },
      async (p) => {
        await ready(p);
        await review(p);
        await confirm(p);
        await p.getByText(unknown, { exact: true }).waitFor();
        await p
          .getByRole("button", { name: "Reload profile", exact: true })
          .click();
        await p
          .getByText("Email: server@example.test", { exact: true })
          .waitFor();
        assert.equal(await p.locator("textarea").count(), 0);
        assert.equal(
          await p
            .getByRole("button", {
              name: /Confirm activation|Retry Activate|Review activation/,
            })
            .count(),
          0,
        );
        assert.equal(
          (await calls(p)).filter((c) => c.method === "PATCH").length,
          1,
        );
        assert.ok(!(await body(p)).includes("accountStatus: ACTIVE"));
      },
    );
  });
for (const id of ["7", "8"])
  test(`identity replacement ${id} aborts PATCH, ignores late 200 and stays unknown`, async () => {
    await withActivation(
      {
        patches: [{ deferred: "patch" }],
        sessions: [{ body: session() }, { body: session(false, id) }],
      },
      async (p) => {
        await ready(p);
        await review(p);
        await confirm(p);
        await p.evaluate(() => window.fixture.revalidate());
        await settle(p);
        await finish(p, "patch", {
          body: { userId: 42, accountStatus: "ACTIVE" },
          headers: { ETag: nextTag },
        });
        await p.getByText(unknown, { exact: true }).waitFor();
        assert.equal(
          (await calls(p)).filter((c) => c.method === "PATCH")[0].aborted,
          true,
        );
        assert.ok(!(await body(p)).includes("accountStatus: ACTIVE"));
        assert.equal(await p.locator("textarea").count(), 0);
      },
    );
  });
test("session expires during PATCH; late success cannot restore flow or identity", async () => {
  await withActivation(
    {
      patches: [{ deferred: "patch" }],
      sessions: [{ body: session() }, problem(401)],
    },
    async (p) => {
      await ready(p);
      await review(p);
      await confirm(p);
      await p.evaluate(() => window.fixture.revalidate());
      await settle(p);
      await finish(p, "patch", {
        body: { userId: 42, accountStatus: "ACTIVE" },
        headers: { ETag: nextTag },
      });
      assert.equal(new URL(p.url()).pathname, "/login");
      assert.ok(!(await body(p)).includes("Server Employee"));
      assert.equal(
        (await calls(p)).filter((c) => c.method === "PATCH")[0].aborted,
        true,
      );
    },
  );
});
test("target change ignores late GET and uses new target ETag", async () => {
  await withActivation(
    {
      reads: [{ deferred: "read" }],
      responses: {
        "/api/users/43/profile": [ok(43, nextTag, "Second Target")],
      },
    },
    async (p) => {
      await p.waitForFunction(() =>
        window.fixture.calls.some((c) => c.url === "/api/users/42/profile"),
      );
      await navigate(p, "/users/43/activate");
      await ready(p);
      await finish(p, "read", ok());
      assert.ok((await body(p)).includes("Second Target"));
      assert.ok(!(await body(p)).includes("Server Employee"));
      assert.equal((await calls(p, "/api/users/42/profile"))[0].aborted, true);
    },
  );
});
for (const destination of ["/profile", "/users/43/activate"])
  test(`pending PATCH cannot publish success after navigation ${destination}`, async () => {
    await withActivation(
      {
        patches: [{ deferred: "patch" }],
        responses: {
          "/api/users/43/profile": [ok(43, nextTag, "Second Target")],
        },
      },
      async (p) => {
        await ready(p);
        await review(p);
        await confirm(p);
        await navigate(p, destination);
        await finish(p, "patch", {
          body: { userId: 42, accountStatus: "ACTIVE" },
          headers: { ETag: nextTag },
        });
        assert.equal(new URL(p.url()).pathname, destination);
        assert.ok(
          !(await body(p)).includes("Activation confirmed by the server"),
        );
        assert.equal(
          (await calls(p)).filter((c) => c.method === "PATCH")[0].aborted,
          true,
        );
      },
    );
  });
test("same-ID replacement drops late management GET", async () => {
  await withActivation(
    {
      reads: [{ deferred: "read" }, ok(42, nextTag, "Fresh Owner Read")],
      sessions: [{ body: session() }, { body: session() }],
    },
    async (p) => {
      await p.waitForFunction(() =>
        window.fixture.calls.some((c) => c.url === "/api/users/42/profile"),
      );
      await p.evaluate(() => window.fixture.revalidate());
      await ready(p);
      await finish(p, "read", ok(42, tag, "Old Owner Read"));
      assert.ok((await body(p)).includes("Fresh Owner Read"));
      assert.ok(!(await body(p)).includes("Old Owner Read"));
    },
  );
});
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
const review = async (page, reason = "Account approved") => {
  await page.getByRole("textbox", { name: "Reason", exact: true }).fill(reason);
  await page
    .getByRole("button", { name: "Review activation", exact: true })
    .click();
};
const confirm = async (page) => {
  await page
    .getByRole("button", { name: "Confirm activation", exact: true })
    .click();
  await settle(page);
};
const ready = (page) =>
  page
    .getByRole("form", { name: "Activate user account", exact: true })
    .waitFor();
async function withActivation(scenario, run, path = "/users/42/activate") {
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
