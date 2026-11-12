import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";

const secret = "PRIVATE-INITIAL-PASSWORD";
const csrfSecret = "PRIVATE-CSRF-TOKEN";
const expiry = "2026-11-08T08:30:00.1234567Z";
const session = (restricted = false, id = "7") => ({
  user: { id, username: "creator", name: "Confirmed Creator" },
  expiresAt: "2026-11-07T12:00:00Z",
  absoluteExpiresAt: "2026-11-07T20:00:00Z",
  mustChangePassword: restricted,
  csrfToken: csrfSecret,
});
const request = {
  employeeCode: "E42",
  username: "employee42",
  fullName: "New Employee",
  email: "employee@example.test",
  phone: null,
  branchId: 1,
  departmentId: 2,
  roleCode: "HK_STAFF",
};
const created = () => ({
  status: 201,
  headers: { Location: "/api/users/42" },
  body: {
    user: { id: 42, ...request, accountStatus: "ACTIVE" },
    temporaryCredential: { password: secret, expiresAtUtc: expiry },
  },
});
const problem = (status, fields = {}) => ({
  status,
  body: {
    status,
    title: "PRIVATE-RAW-TITLE",
    detail: secret,
    traceId: csrfSecret,
    ...fields,
  },
});
const unknown =
  "Account creation outcome could not be confirmed. The account may already exist. Do not submit this request again. Ask an authorized administrator to verify the account and arrange a credential reset if needed.";

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
  const scenario = window.scenario, calls = [], waiting = new Map(), unexpected = [];
  const queues = {
    "/api/auth/session": [...(scenario.sessions ?? [{body:${JSON.stringify(session())}}])],
    "/api/auth/csrf": [...(scenario.csrf ?? [{body:{csrfToken:${JSON.stringify(csrfSecret)},headerName:"X-CSRF-TOKEN"}}])],
    "/api/users": [...(scenario.posts ?? [${JSON.stringify(created())}])],
    "/api/auth/login": [{body:${JSON.stringify(session(false, "8"))}}],
    ...scenario.responses,
  };
  function respond(reply) {
    if (reply.bodyReadFailure) return {status:reply.status,headers:new Headers({"Content-Type":"application/json"}),text:async()=>{throw new Error(${JSON.stringify(secret)});}};
    if (reply.network) throw new Error(${JSON.stringify(secret)});
    if (reply.timeout) throw new DOMException(${JSON.stringify(secret)}, "TimeoutError");
    if (reply.cancelled) throw new DOMException(${JSON.stringify(secret)}, "AbortError");
    return new Response(reply.raw ?? JSON.stringify(reply.body), { status:reply.status ?? 200, headers:{"Content-Type":reply.type ?? "application/json", ...reply.headers} });
  }
  const fetcher = async (url, init) => {
    const call = {url, method:init.method, headers:Object.fromEntries(new Headers(init.headers)), body:init.body, credentials:init.credentials, cache:init.cache, redirect:init.redirect, aborted:init.signal?.aborted ?? false};
    init.signal?.addEventListener("abort", () => {call.aborted=true;}, {once:true}); calls.push(call);
    const reply = queues[url]?.shift();
    if (!reply) {unexpected.push(url); throw new Error("Unexpected request");}
    if (reply.deferred) return new Promise((resolve,reject) => waiting.set(reply.deferred,{resolve,reject}));
    return respond(reply);
  };
  window.fetch=fetcher;
  const store=createAuthStore(createAuthApi(fetcher));
  const host=<AuthRouteHost/>;
  const root=createRoot(document.getElementById("root"));
  root.render(<StrictMode><BrowserRouter><AuthProvider store={store} fallback={host}>{host}</AuthProvider></BrowserRouter></StrictMode>);
  window.fixture={calls,unexpected,snapshot:()=>store.getSnapshot(),revalidate:()=>store.revalidate(),login:()=>store.login({username:"creator",password:"test-only"}),
    navigate(path){history.pushState(null,"",path);dispatchEvent(new PopStateEvent("popstate"));},
    finish(tag,reply){const p=waiting.get(tag);if(!p)throw new Error("Missing deferred response");waiting.delete(tag);try{p.resolve(respond(reply));}catch(e){p.reject(e);}},
    unmount(){root.unmount();}
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
async function finish(page, tag, reply) {
  await page.evaluate(({ tag, reply }) => window.fixture.finish(tag, reply), {
    tag,
    reply,
  });
  await settle(page);
}
async function navigate(page, path) {
  await page.evaluate((path) => window.fixture.navigate(path), path);
  await settle(page);
}
async function fill(page) {
  await page
    .getByRole("heading", { name: "Create user account", exact: true })
    .waitFor();
  for (const [name, value] of Object.entries({
    "Employee code": " E42 ",
    Username: " employee42 ",
    "Full name": " New Employee ",
    Email: " employee@example.test ",
    "Branch ID": "1",
    "Department ID": "2",
  }))
    await page.getByLabel(name, { exact: true }).fill(value);
}
async function submit(page) {
  await page
    .getByRole("button", { name: "Create account", exact: true })
    .click();
  await settle(page);
}
async function visible(page) {
  return page.locator("body").innerText();
}
async function withFlow(scenario, run, path = "/users/new") {
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
      window.clipboardWrites = [];
      for (const key of ["localStorage", "sessionStorage", "indexedDB"])
        Object.defineProperty(window, key, {
          configurable: true,
          get() {
            window.storageTouches++;
            throw new Error("Forbidden storage");
          },
        });
      Object.defineProperty(navigator, "clipboard", {
        configurable: true,
        value: {
          writeText: async (value) => {
            if (scenario.clipboardFailure)
              throw new Error("private clipboard error");
            window.clipboardWrites.push(value);
          },
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
      !logs.some((line) => line.includes(secret) || line.includes(csrfSecret)),
    );
    assert.ok(!(await visible(page)).includes(csrfSecret));
    assert.ok(!(await visible(page)).includes("PRIVATE-RAW-TITLE"));
    for (const c of await calls(page)) {
      assert.equal(c.credentials, "include");
      assert.equal(c.cache, "no-store");
      assert.equal(c.headers.authorization, undefined);
      assert.ok(!c.url.includes("reset-credential"));
      if (c.method === "POST" && c.url === "/api/users") {
        assert.equal(c.redirect, "error");
        assert.equal(c.headers["x-csrf-token"], csrfSecret);
        assert.equal(c.headers["content-type"], "application/json");
        assert.equal(c.headers["if-match"], undefined);
      }
    }
  } finally {
    await page.close();
  }
}

test("real import graph includes accepted create API and excludes prototypes/mock Shell", () => {
  const paths = Object.keys(bundle.metafile.inputs).map((p) =>
    p.replaceAll("\\", "/"),
  );
  for (const part of [
    "auth/authStore.ts",
    "auth/AuthProvider.tsx",
    "users/createUserApi.ts",
    "users/CreateUserFlowBoundary.tsx",
  ])
    assert.ok(paths.some((p) => p.endsWith(part)));
  assert.ok(
    !paths.some((p) =>
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
  test(`direct URL guarded: ${path}`, async () => {
    await withFlow(
      { sessions: [restricted ? { body: session(true) } : problem(401)] },
      async (page) => {
        await page.waitForFunction((path) => location.pathname === path, path);
        assert.equal((await calls(page, "/api/users")).length, 0);
        assert.equal(
          await page.getByRole("form", { name: "Create user account" }).count(),
          0,
        );
      },
    );
  });
for (const reply of [{ deferred: "session" }, problem(503)])
  test(`unknown identity hides form: ${reply.status ?? "pending"}`, async () => {
    await withFlow({ sessions: [reply] }, async (page) => {
      assert.equal(
        await page.getByRole("form", { name: "Create user account" }).count(),
        0,
      );
      assert.equal((await calls(page, "/api/users")).length, 0);
      assert.ok(!(await visible(page)).includes(secret));
    });
  });
test("form is independent of USER_VIEW and does not infer create permission", async () => {
  await withFlow({ posts: [problem(403)] }, async (page) => {
    await fill(page);
    assert.equal((await calls(page, "/api/users")).length, 0);
    await submit(page);
    assert.match(await visible(page), /Access denied/);
    assert.equal((await calls(page, "/api/users")).length, 1);
    assert.equal(
      await page.evaluate(() => window.fixture.snapshot().status),
      "AUTHENTICATED",
    );
  });
});
test("required, max lengths and safe IDs validate before bootstrap; six initial roles only", async () => {
  await withFlow({}, async (page) => {
    await submit(page);
    assert.match(await visible(page), /Enter all required fields/);
    assert.equal((await calls(page, "/api/auth/csrf")).length, 0);
    await fill(page);
    for (const value of ["0", "-1", "1.5", "9007199254740992", "1e2"]) {
      await page.getByLabel("Branch ID", { exact: true }).fill(value);
      await submit(page);
      assert.equal((await calls(page, "/api/users")).length, 0);
    }
    assert.deepEqual(
      await page
        .getByLabel("Initial role", { exact: true })
        .locator("option")
        .evaluateAll((nodes) => nodes.map((n) => n.value)),
      [
        "HK_STAFF",
        "HK_SUPERVISOR",
        "MAINTENANCE_STAFF",
        "MAINTENANCE_LEAD",
        "STOREKEEPER",
        "RECEPTION",
      ],
    );
    assert.equal(await page.locator('input[type="password"]').count(), 0);
    assert.equal(
      await page
        .getByLabel("Employee code", { exact: true })
        .getAttribute("maxlength"),
      "40",
    );
  });
});
test("StrictMode, two submit events, Enter and deferred bootstrap dispatch one attempt", async () => {
  await withFlow(
    { csrf: [{ deferred: "csrf" }], posts: [{ deferred: "post" }] },
    async (page) => {
      await fill(page);
      assert.equal((await calls(page, "/api/auth/session")).length, 1);
      await page
        .getByRole("form", { name: "Create user account" })
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
      assert.equal((await calls(page, "/api/users")).length, 0);
      await page.getByLabel("Username", { exact: true }).press("Enter");
      await finish(page, "csrf", {
        body: { csrfToken: csrfSecret, headerName: "X-CSRF-TOKEN" },
      });
      assert.equal((await calls(page, "/api/users")).length, 1);
      assert.deepEqual(
        JSON.parse((await calls(page, "/api/users"))[0].body),
        request,
      );
      await finish(page, "post", created());
      assert.match(await visible(page), /Account created — initial credential/);
      assert.equal((await calls(page, "/api/users")).length, 1);
    },
  );
});
test("confirmed 201 handoff: hidden, explicit reveal/copy, server expiry and acknowledgement clear DOM", async () => {
  await withFlow({}, async (page) => {
    await fill(page);
    await submit(page);
    assert.equal(await page.evaluate(() => location.pathname), "/users/new");
    assert.equal(
      await page.evaluate(() => window.fixture.snapshot().session.user.id),
      "7",
    );
    assert.ok(!(await visible(page)).includes(secret));
    assert.equal(
      await page
        .locator("body")
        .evaluate((el) => el.innerHTML.includes("PRIVATE-INITIAL-PASSWORD")),
      false,
    );
    assert.deepEqual(await page.evaluate(() => window.clipboardWrites), []);
    assert.match(
      await visible(page),
      new RegExp(expiry.replaceAll(".", "\\.")),
    );
    await page
      .getByRole("button", { name: "Reveal password", exact: true })
      .click();
    assert.ok((await visible(page)).includes(secret));
    await page
      .getByRole("button", { name: "Hide password", exact: true })
      .click();
    assert.ok(!(await visible(page)).includes(secret));
    await page
      .getByRole("button", { name: "Copy password", exact: true })
      .click();
    assert.deepEqual(await page.evaluate(() => window.clipboardWrites), [
      secret,
    ]);
    await page
      .getByRole("button", {
        name: "I have securely handed off the credential",
        exact: true,
      })
      .click();
    assert.match(await visible(page), /Credential handoff complete/);
    assert.ok(!(await visible(page)).includes(secret));
    assert.equal(
      await page
        .getByRole("button", { name: "Reveal password", exact: true })
        .count(),
      0,
    );
    assert.equal(
      await page
        .getByRole("button", { name: "Create account", exact: true })
        .count(),
      0,
    );
  });
});
test("clipboard failure stays safe and allows manual reveal", async () => {
  await withFlow({ clipboardFailure: true }, async (page) => {
    await fill(page);
    await submit(page);
    await page
      .getByRole("button", { name: "Copy password", exact: true })
      .click();
    assert.match(await visible(page), /Clipboard copy is unavailable/);
    assert.ok(!(await visible(page)).includes(secret));
    await page
      .getByRole("button", { name: "Reveal password", exact: true })
      .click();
    assert.ok((await visible(page)).includes(secret));
  });
});
for (const [reply, message] of [
  [problem(400), "Check the account fields"],
  [
    problem(400, { code: "CSRF_VALIDATION_FAILED" }),
    "security check was rejected",
  ],
  [
    problem(409, {
      title: "Employee code, username or email already exists",
      duplicateFields: ["username", "email"],
    }),
    "Already in use: username, email",
  ],
  [
    problem(409, {
      title: "Employee code, username or email already exists",
      duplicateFields: [],
    }),
    "already in use",
  ],
  [problem(409, { title: "LAST_DURABLE_ADMIN" }), "safe administrator access"],
  [problem(415), "request format"],
])
  test(`safe rejected mutation: ${reply.status} ${message}`, async () => {
    await withFlow({ posts: [reply] }, async (page) => {
      await fill(page);
      await submit(page);
      assert.ok((await visible(page)).includes(message));
      assert.ok(!(await visible(page)).includes(secret));
      assert.equal((await calls(page, "/api/users")).length, 1);
      assert.equal(
        await page
          .getByRole("button", { name: "Create account", exact: true })
          .count(),
        1,
      );
    });
  });
for (const reply of [
  problem(503),
  problem(503, { title: "GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE" }),
  problem(500),
  { network: true },
  { timeout: true },
  { cancelled: true },
  { status: 201, body: { temporaryCredential: { password: secret } } },
])
  test(`unknown outcome: ${JSON.stringify({ status: reply.status, network: reply.network, timeout: reply.timeout, cancelled: reply.cancelled })}`, async () => {
    await withFlow({ posts: [reply] }, async (page) => {
      await fill(page);
      await submit(page);
      assert.ok((await visible(page)).includes(unknown));
      assert.ok(!(await visible(page)).includes(secret));
      assert.equal(
        await page
          .getByRole("button", { name: /Create account|Retry Create/ })
          .count(),
        0,
      );
      assert.equal((await calls(page, "/api/users")).length, 1);
    });
  });
test("CSRF bootstrap failure never dispatches POST", async () => {
  await withFlow({ csrf: [problem(503)] }, async (page) => {
    await fill(page);
    await submit(page);
    assert.match(await visible(page), /No account creation request was sent/);
    assert.equal((await calls(page, "/api/users")).length, 0);
  });
});
test("401 uses GET session recovery once without replay", async () => {
  await withFlow(
    { sessions: [{ body: session() }, problem(401)], posts: [problem(401)] },
    async (page) => {
      await fill(page);
      await submit(page);
      await page.waitForFunction(() => location.pathname === "/login");
      assert.equal((await calls(page, "/api/users")).length, 1);
      assert.equal((await calls(page, "/api/auth/session")).length, 2);
    },
  );
});
test("restricted response revalidates before restricted routing", async () => {
  await withFlow(
    {
      sessions: [{ body: session() }, { body: session(true) }],
      posts: [problem(403, { code: "PASSWORD_CHANGE_REQUIRED" })],
    },
    async (page) => {
      await fill(page);
      await submit(page);
      await page.waitForFunction(
        () => location.pathname === "/change-password",
      );
      assert.equal((await calls(page, "/api/users")).length, 1);
    },
  );
});
test("late 201 after route change/unmount cannot publish credential or navigate", async () => {
  await withFlow({ posts: [{ deferred: "post" }] }, async (page) => {
    await fill(page);
    await submit(page);
    await navigate(page, "/profile");
    await finish(page, "post", created());
    assert.equal(await page.evaluate(() => location.pathname), "/profile");
    assert.ok(!(await visible(page)).includes(secret));
    assert.equal((await calls(page, "/api/users"))[0].aborted, true);
  });
});
for (const nextId of ["7", "8"])
  test(`pending POST invalidated by session replacement ${nextId}`, async () => {
    await withFlow(
      {
        sessions: [{ body: session() }, { deferred: "check" }],
        posts: [{ deferred: "post" }],
      },
      async (page) => {
        await fill(page);
        await submit(page);
        await page.evaluate(() => {
          void window.fixture.revalidate();
        });
        await settle(page);
        assert.equal(
          await page.getByRole("form", { name: "Create user account" }).count(),
          0,
        );
        assert.ok(!(await visible(page)).includes(secret));
        await finish(page, "post", created());
        await finish(page, "check", { body: session(false, nextId) });
        assert.ok((await visible(page)).includes(unknown));
        assert.equal(
          await page
            .getByRole("button", { name: "Create account", exact: true })
            .count(),
          0,
        );
        assert.ok(!(await visible(page)).includes(secret));
        assert.equal((await calls(page, "/api/users")).length, 1);
      },
    );
  });
test("pre-POST bootstrap pending is aborted on revalidation without sending POST", async () => {
  await withFlow(
    {
      sessions: [{ body: session() }, { body: session() }],
      csrf: [{ deferred: "csrf" }],
    },
    async (page) => {
      await fill(page);
      await submit(page);
      await page.evaluate(() => window.fixture.revalidate());
      await settle(page);
      await finish(page, "csrf", {
        body: { csrfToken: csrfSecret, headerName: "X-CSRF-TOKEN" },
      });
      assert.equal((await calls(page, "/api/users")).length, 0);
      assert.ok((await visible(page)).includes(unknown));
    },
  );
});
test("handoff is destroyed by unavailable identity and cannot restore on recovery", async () => {
  await withFlow(
    { sessions: [{ body: session() }, problem(503), { body: session() }] },
    async (page) => {
      await fill(page);
      await submit(page);
      await page
        .getByRole("button", { name: "Reveal password", exact: true })
        .click();
      assert.ok((await visible(page)).includes(secret));
      await page.evaluate(() => window.fixture.revalidate());
      await settle(page);
      assert.ok(!(await visible(page)).includes(secret));
      assert.equal(
        await page
          .getByRole("button", { name: "Copy password", exact: true })
          .count(),
        0,
      );
      await page
        .getByRole("button", { name: "Check session", exact: true })
        .click();
      await settle(page);
      assert.ok(!(await visible(page)).includes(secret));
      assert.equal(
        await page
          .getByRole("button", { name: "Reveal password", exact: true })
          .count(),
        0,
      );
      assert.equal((await calls(page, "/api/users")).length, 1);
    },
  );
});
test("late 201 after login identity replacement stays discarded", async () => {
  await withFlow({ posts: [{ deferred: "post" }] }, async (page) => {
    await fill(page);
    await submit(page);
    await page.evaluate(() => window.fixture.login());
    await settle(page);
    await finish(page, "post", created());
    assert.equal(
      await page.evaluate(() => window.fixture.snapshot().session.user.id),
      "8",
    );
    assert.ok(!(await visible(page)).includes(secret));
    assert.ok((await visible(page)).includes(unknown));
  });
});

test("Enter starts one attempt and sends selected initial role without a Users read", async () => {
  await withFlow({}, async (page) => {
    await fill(page);
    await page
      .getByLabel("Initial role", { exact: true })
      .selectOption("RECEPTION");
    await page.getByLabel("Username", { exact: true }).press("Enter");
    await settle(page);
    const post = await calls(page, "/api/users");
    assert.equal(post.length, 1);
    assert.deepEqual(JSON.parse(post[0].body), {
      ...request,
      roleCode: "RECEPTION",
    });
    assert.ok(
      !(await calls(page)).some(
        (call) => call.method === "GET" && call.url.startsWith("/api/users"),
      ),
    );
  });
});

test("401 during CSRF bootstrap checks session without sending POST", async () => {
  await withFlow(
    { sessions: [{ body: session() }, problem(401)], csrf: [problem(401)] },
    async (page) => {
      await fill(page);
      await submit(page);
      await page.waitForFunction(() => location.pathname === "/login");
      assert.equal((await calls(page, "/api/users")).length, 0);
      assert.equal((await calls(page, "/api/auth/session")).length, 2);
    },
  );
});

for (const stage of ["csrf", "post"]) {
  test(`unreadable401 ${stage} revalidates session without replay or credential leak`, async () => {
    await withFlow(
      {
        sessions: [{ body: session() }, { deferred: "recover" }],
        ...(stage === "csrf"
          ? { csrf: [{ status: 401, bodyReadFailure: true }] }
          : { posts: [{ status: 401, bodyReadFailure: true }] }),
      },
      async (page) => {
        await fill(page);
        await submit(page);
        await page
          .getByRole("main", { name: "Create account session recovery" })
          .waitFor();
        assert.equal(
          (await calls(page, "/api/users")).length,
          stage === "csrf" ? 0 : 1,
        );
        assert.equal((await calls(page, "/api/auth/session")).length, 2);
        assert.ok(!(await visible(page)).includes(secret));
        await finish(page, "recover", { body: session() });
        if (stage === "post") {
          assert.ok((await visible(page)).includes(unknown));
          assert.equal(
            await page
              .getByRole("button", { name: "Create account", exact: true })
              .count(),
            0,
          );
        }
        assert.equal(
          (await calls(page, "/api/users")).length,
          stage === "csrf" ? 0 : 1,
        );
      },
    );
  });
}

test("revealed credential disappears during pending revalidation and never returns", async () => {
  await withFlow(
    { sessions: [{ body: session() }, { deferred: "check" }] },
    async (page) => {
      await fill(page);
      await submit(page);
      await page
        .getByRole("button", { name: "Reveal password", exact: true })
        .click();
      await page.evaluate(() => {
        void window.fixture.revalidate();
      });
      await settle(page);
      assert.ok(!(await visible(page)).includes(secret));
      assert.equal(
        await page
          .getByRole("button", { name: "Copy password", exact: true })
          .count(),
        0,
      );
      await finish(page, "check", { body: session() });
      assert.ok(!(await visible(page)).includes(secret));
      assert.equal(
        await page
          .getByRole("button", { name: "Reveal password", exact: true })
          .count(),
        0,
      );
      assert.equal((await calls(page, "/api/users")).length, 1);
    },
  );
});

for (const [field, wrong] of Object.entries({
  username: "other-account",
  branchId: 99,
  departmentId: 99,
  roleCode: "RECEPTION",
}))
  test(`201 mismatched ${field} locks flow without revealing or copying credential`, async () => {
    const reply = created();
    reply.body.user[field] = wrong;
    await withFlow({ posts: [reply] }, async (page) => {
      await fill(page);
      await submit(page);
      assert.ok((await visible(page)).includes(unknown));
      assert.ok(!(await visible(page)).includes(secret));
      assert.equal(
        await page
          .getByRole("button", { name: "Reveal password", exact: true })
          .count(),
        0,
      );
      assert.equal(
        await page
          .getByRole("button", { name: "Create account", exact: true })
          .count(),
        0,
      );
      assert.deepEqual(await page.evaluate(() => window.clipboardWrites), []);
      assert.equal((await calls(page, "/api/users")).length, 1);
      await page.evaluate(() =>
        document
          .querySelector("form")
          ?.dispatchEvent(
            new Event("submit", { bubbles: true, cancelable: true }),
          ),
      );
      assert.equal((await calls(page, "/api/users")).length, 1);
    });
  });
