import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";

const secret = "PRIVATE-USERS-UI-SECRET";
const stamp = "2026-11-07T08:30:00.1234567Z";
const session = (restricted = false, id = "7") => ({
  user: { id, username: "reviewer", name: "Confirmed Reviewer" },
  expiresAt: "2026-11-07T12:00:00Z",
  absoluteExpiresAt: "2026-11-07T20:00:00Z",
  mustChangePassword: restricted,
  csrfToken: secret,
});
const account = (id = 42, fullName = "Minimal Target") => ({
  id,
  employeeCode: `E${id}`,
  username: `staff${id}`,
  fullName,
  email: `staff${id}@example.test`,
  phone: null,
  accountStatus: "ACTIVE",
  primaryBranch: { id: 1, code: "B1", name: "Globally visible primary branch" },
  department: { id: 2, code: "D1", name: "Globally visible department" },
});
const directory = (
  name = "Directory Target",
  page = 1,
  totalCount = 41,
  pageSize = 20,
) => {
  const { phone: _phone, ...item } = account(42, name);
  return {
    items: [
      {
        ...item,
        roles: [{ id: 3, code: "R1", name: "Server Role", branch: null }],
      },
    ],
    page,
    pageSize,
    totalCount,
  };
};
const report = (id = 42, global = false) => ({
  account: {
    ...account(id, "Reviewed Target"),
    primaryBranch: global ? account(id).primaryBranch : null,
    department: null,
  },
  evaluatedAtUtc: stamp,
  reviewerScope: { isGlobal: global, branchIds: global ? [] : [1] },
  assignedRoles: [
    {
      assignmentId: 10,
      roleId: 3,
      code: "R1",
      name: "Historical Role",
      branchId: 1,
      assignmentActive: false,
      roleActive: true,
      assignedAtUtc: stamp,
      expiresAtUtc: "2025-01-01T00:00:00Z",
    },
  ],
  branchMemberships: [
    {
      branchId: 1,
      code: "B1",
      name: "Allowed branch",
      accessLevel: "STANDARD",
      branchActive: true,
      grantedAtUtc: stamp,
      revokedAtUtc: null,
    },
  ],
  configuredGrants: [
    {
      permissionCode: "USER_VIEW",
      permissionName: "Configured view",
      feature: "ADMIN",
      configuredScope: "BRANCH",
      assignmentId: 10,
      roleCode: "R1",
      branchId: 1,
      isEffective: false,
    },
  ],
  effectivePermissions: [
    {
      code: "TASK_VIEW",
      name: "Conditional task view",
      feature: "TASK",
      sources: [
        {
          assignmentId: 11,
          roleCode: "R2",
          scope: "OWN",
          branchId: 1,
          requiresResourceOwnership: true,
        },
      ],
    },
  ],
});
const ok = (body) => ({ body });
const problem = (status, code = "FORBIDDEN") => ({
  status,
  body: { status, code, title: secret, detail: secret, traceId: secret },
});
const anon = problem(401, "SESSION_INVALID");
const unavailable = problem(503, "AUTH_SERVICE_UNAVAILABLE");
const listUrl = "/api/users?page=1&pageSize=20";

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
      const scenario = window.usersScenario;
      const calls = [], unexpected = [], waiting = new Map();
      const queues = {
        "/api/auth/session": [...(scenario.sessions ?? [${JSON.stringify(ok(session()))}])],
        "/api/auth/login": [${JSON.stringify(ok(session()))}],
        "/api/auth/logout": [{status:204}],
        "/api/auth/change-password": [${JSON.stringify(ok(session()))}],
        ...Object.fromEntries(Object.entries(scenario.responses ?? {}).map(([key, replies]) => [key, [...replies]])),
      };
      function response(reply) {
        if (reply.unreadable) { const r = new Response("", {status:reply.status,headers:{"Content-Type":"application/problem+json"}}); r.text=async()=>{throw Error("PRIVATE-092");}; return r; }
        if (reply.network) throw new Error(${JSON.stringify(secret)});
        if (reply.cancelled) throw new DOMException(${JSON.stringify(secret)}, "AbortError");
        return new Response(reply.status === 204 ? null : reply.raw ?? JSON.stringify(reply.body), {
          status: reply.status ?? 200, headers: { "Content-Type": reply.contentType ?? "application/json" },
        });
      }
      const fetcher = async (url, init) => {
        const call = { url, method: init.method, credentials: init.credentials, cache: init.cache,
          headers: Object.fromEntries(new Headers(init.headers)), body: init.body, aborted: init.signal?.aborted ?? false };
        init.signal?.addEventListener("abort", () => { call.aborted = true; }, { once: true });
        calls.push(call);
        if (url === "/api/auth/csrf") return response({ body: { csrfToken: "anonymous-token", headerName: "X-CSRF-TOKEN" } });
        const reply = queues[url]?.shift();
        if (!reply) { unexpected.push(url); throw new Error("Unexpected request"); }
        if (reply.deferred) return new Promise((resolve, reject) => waiting.set(reply.deferred, { resolve, reject }));
        return response(reply);
      };
      window.fetch = fetcher; // UsersRouteBoundary creates the real B-1 client over this fetch.
      const store = createAuthStore(createAuthApi(fetcher));
      const host = <AuthRouteHost />;
      createRoot(document.getElementById("root")).render(
        <StrictMode><BrowserRouter><AuthProvider store={store} fallback={host}>{host}</AuthProvider></BrowserRouter></StrictMode>
      );
      window.usersTest = {
        calls, unexpected, snapshot: () => store.getSnapshot(),
        finish(tag, reply) {
          const pending = waiting.get(tag);
          if (!pending) throw new Error("Missing deferred request");
          waiting.delete(tag);
          try { pending.resolve(response(reply)); } catch (error) { pending.reject(error); }
        },
        navigate(path) { history.pushState(null, "", path); dispatchEvent(new PopStateEvent("popstate")); },
        revalidate: () => store.revalidate(),
        login: () => store.login({ username: "reviewer", password: ${JSON.stringify(secret)} }),
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
    ].find((path) => existsSync(path));
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
async function heading(page, name) {
  await page.getByRole("heading", { name, exact: true }).waitFor();
}
async function pathIs(page, path) {
  await page.waitForFunction((path) => location.pathname === path, path);
}
async function finish(page, tag, reply) {
  await page.evaluate(({ tag, reply }) => window.usersTest.finish(tag, reply), {
    tag,
    reply,
  });
  await settle(page);
}
async function calls(page, path = null) {
  return page.evaluate(
    (path) =>
      window.usersTest.calls.filter(
        (call) => path === null || call.url === path,
      ),
    path,
  );
}
async function navigate(page, path) {
  await page.evaluate((path) => window.usersTest.navigate(path), path);
  await settle(page);
}
async function withUsers(path, scenario, run) {
  const page = await browser.newPage();
  const errors = [],
    logs = [],
    network = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => logs.push(message.text()));
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
      window.usersScenario = scenario;
      window.storageTouches = 0;
      for (const key of ["localStorage", "sessionStorage"])
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
    assert.deepEqual(
      await page.evaluate(() => window.usersTest.unexpected),
      [],
    );
    assert.equal(await page.evaluate(() => window.storageTouches), 0);
    assert.ok(!logs.some((line) => line.includes(secret)));
    assert.ok(!(await page.locator("body").innerText()).includes(secret));
    for (const call of await calls(page)) {
      assert.equal(call.credentials, "include");
      assert.equal(call.cache, "no-store");
      assert.equal(call.headers.authorization, undefined);
      if (call.url.startsWith("/api/users")) {
        assert.equal(call.method, "GET");
        assert.equal(call.body, undefined);
        assert.equal(call.headers["x-csrf-token"], undefined);
        assert.ok(!call.url.includes("includeAccess"));
      }
    }
  } finally {
    await page.close();
  }
}

test("real users route import graph includes B-1 and excludes mock services/shared Shell", () => {
  const inputs = Object.keys(bundle.metafile.inputs).map((path) =>
    path.replaceAll("\\", "/"),
  );
  assert.ok(inputs.some((path) => path.endsWith("users/usersApi.ts")));
  assert.ok(inputs.some((path) => path.endsWith("users/useUserRead.ts")));
  assert.ok(
    !inputs.some((path) =>
      /src\/(services(?:\/|\.ts)|ui\.tsx|i18n\.tsx|mocks\/|routes\/(routeConfig|ScreenIndex|DesignSystem))/.test(
        path,
      ),
    ),
  );
});
test("directory renders server fields, totalCount and one StrictMode read without write controls", async () => {
  await withUsers(
    "/users",
    { responses: { [listUrl]: [ok(directory())] } },
    async (page) => {
      await page.getByRole("link", { name: "Directory Target" }).waitFor();
      assert.match(
        await page.locator("body").innerText(),
        /Matching accounts: 41/,
      );
      assert.match(
        await page.getByRole("table", { name: "User accounts" }).innerText(),
        /E42[\s\S]*staff42[\s\S]*Server Role[\s\S]*ACTIVE/,
      );
      assert.equal((await calls(page, listUrl)).length, 1);
      assert.equal((await calls(page, "/api/auth/session")).length, 1);
      assert.equal(
        await page
          .getByRole("button", {
            name: /create|edit|deactivate|reset password|assign role/i,
          })
          .count(),
        0,
      );
      assert.equal(
        await page.getByRole("button", { name: /sort/i }).count(),
        0,
      );
    },
  );
});
test("empty directory is not an authorization error", async () => {
  await withUsers(
    "/users",
    {
      responses: {
        [listUrl]: [ok({ items: [], page: 1, pageSize: 20, totalCount: 0 })],
      },
    },
    async (page) => {
      await page.getByText("No matching accounts.", { exact: true }).waitFor();
      assert.equal(await page.getByRole("alert").count(), 0);
    },
  );
});
for (const [label, reply] of [
  ["403", problem(403)],
  ["503", unavailable],
  ["092 unreadable 403", { ...problem(403), unreadable: true }],
  ["092 unreadable 503", { ...unavailable, unreadable: true }],
  ["500", problem(500, "INTERNAL_SERVER_ERROR")],
  ["network", { network: true }],
  ["invalid-response", ok({ items: [] })],
]) {
  test(`directory ${label} shows safe error, keeps identity, no fallback or revalidation`, async () => {
    await withUsers(
      "/users",
      { responses: { [listUrl]: [reply] } },
      async (page) => {
        await page.getByRole("alert").waitFor();
        assert.equal(await page.getByRole("table").count(), 0);
        assert.equal(
          await page.evaluate(() => window.usersTest.snapshot().status),
          "AUTHENTICATED",
        );
        assert.equal((await calls(page, "/api/auth/session")).length, 1);
        assert.equal((await calls(page, listUrl)).length, 1);
      },
    );
  });
}
for (const path of ["/users", "/users/42"]) {
  for (const restricted of [false, true]) {
    test(`direct URL cannot read users when ${restricted ? "restricted" : "anonymous"}: ${path}`, async () => {
      await withUsers(
        path,
        { sessions: [restricted ? ok(session(true)) : anon] },
        async (page) => {
          await pathIs(page, restricted ? "/change-password" : "/login");
          await heading(page, restricted ? "Change Password" : "Sign In");
          assert.equal(
            (await calls(page)).filter((call) =>
              call.url.startsWith("/api/users"),
            ).length,
            0,
          );
        },
      );
    });
  }
}
test("startup pending and unavailable hide all accounts until explicit session recovery", async () => {
  await withUsers(
    "/users",
    {
      sessions: [{ deferred: "startup" }, ok(session())],
      responses: { [listUrl]: [ok(directory())] },
    },
    async (page) => {
      await page.getByRole("status").waitFor();
      assert.equal(
        (await calls(page)).filter((call) => call.url.startsWith("/api/users"))
          .length,
        0,
      );
      await finish(page, "startup", unavailable);
      await page
        .getByRole("button", { name: "Check session", exact: true })
        .click();
      await page.getByRole("link", { name: "Directory Target" }).waitFor();
      assert.equal((await calls(page, "/api/auth/session")).length, 2);
    },
  );
});

test("server pagination uses totalCount instead of current page length", async () => {
  const page2 = "/api/users?page=2&pageSize=20";
  await withUsers(
    "/users",
    {
      responses: {
        [listUrl]: [ok(directory())],
        [page2]: [ok(directory("Page Two Target", 2))],
      },
    },
    async (page) => {
      await page.getByRole("link", { name: "Directory Target" }).waitFor();
      await page.getByRole("button", { name: "Next page" }).click();
      await page.getByRole("link", { name: "Page Two Target" }).waitFor();
      assert.match(await page.locator("body").innerText(), /Page 2 of 3/);
      assert.equal((await calls(page, page2)).length, 1);
    },
  );
});
test("search, primary branch, department, role, status and page size go to the server", async () => {
  const filtered =
    "/api/users?search=Alice+%26+Bob&branchId=1&departmentId=2&roleId=3&accountStatus=INACTIVE&page=1&pageSize=50";
  await withUsers(
    "/users",
    {
      responses: {
        [listUrl]: [ok(directory())],
        [filtered]: [ok(directory("Filtered Target", 1, 1, 50))],
      },
    },
    async (page) => {
      await page.getByRole("link", { name: "Directory Target" }).waitFor();
      await page
        .getByLabel("Search name, username or email")
        .fill("Alice & Bob");
      await page.getByLabel("Primary branch ID").fill("1");
      await page.getByLabel("Department ID").fill("2");
      await page.getByLabel("Role ID").fill("3");
      await page
        .getByLabel("Account status", { exact: true })
        .selectOption("INACTIVE");
      await page.getByLabel("Page size").selectOption("50");
      await page
        .getByRole("button", { name: "Apply filters", exact: true })
        .click();
      await page.getByRole("link", { name: "Filtered Target" }).waitFor();
      assert.equal(
        await page
          .getByRole("link", { name: "Directory Target", exact: true })
          .count(),
        0,
      );
      assert.equal((await calls(page, filtered)).length, 1);
    },
  );
});
test("invalid numeric filter fails locally without fetching or retaining previous rows", async () => {
  await withUsers(
    "/users",
    { responses: { [listUrl]: [ok(directory())] } },
    async (page) => {
      await page.getByRole("link", { name: "Directory Target" }).waitFor();
      await page.getByLabel("Primary branch ID").fill("9007199254740993");
      await page
        .getByRole("button", { name: "Apply filters", exact: true })
        .click();
      await page.getByRole("alert").waitFor();
      assert.equal(await page.getByRole("table").count(), 0);
      assert.equal((await calls(page, listUrl)).length, 1);
    },
  );
});
test("filter replacement aborts old read and late success cannot replace newer rows", async () => {
  const filtered = "/api/users?search=new&page=1&pageSize=20";
  await withUsers(
    "/users",
    {
      responses: {
        [listUrl]: [{ deferred: "oldList" }],
        [filtered]: [ok(directory("New Rows"))],
      },
    },
    async (page) => {
      await heading(page, "User directory");
      await page.getByLabel("Search name, username or email").fill("new");
      await page
        .getByRole("button", { name: "Apply filters", exact: true })
        .click();
      await page.getByRole("link", { name: "New Rows" }).waitFor();
      assert.equal((await calls(page, listUrl))[0].aborted, true);
      await finish(page, "oldList", ok(directory("Stale Rows")));
      assert.equal(
        await page.getByRole("link", { name: "Stale Rows" }).count(),
        0,
      );
      await page.getByRole("link", { name: "New Rows" }).waitFor();
    },
  );
});
test("a failed refresh never shows successful stale rows", async () => {
  await withUsers(
    "/users",
    { responses: { [listUrl]: [ok(directory()), unavailable] } },
    async (page) => {
      await page.getByRole("link", { name: "Directory Target" }).waitFor();
      await page
        .getByRole("button", { name: "Apply filters", exact: true })
        .click();
      await page.getByRole("alert").waitFor();
      assert.equal(await page.getByRole("table").count(), 0);
    },
  );
});

test("detail 200 remains visible when independent review returns 403", async () => {
  await withUsers(
    "/users/42",
    {
      responses: {
        "/api/users/42": [ok(account())],
        "/api/users/42/effective-access": [problem(403)],
      },
    },
    async (page) => {
      const minimal = page.getByRole("region", {
        name: "Minimal account detail",
        exact: true,
      });
      await minimal.getByText("Minimal Target", { exact: true }).waitFor();
      await page
        .getByRole("region", { name: "Access review", exact: true })
        .getByRole("alert")
        .waitFor();
      assert.match(await minimal.innerText(), /Globally visible department/);
      assert.equal(
        await page.evaluate(() => window.usersTest.snapshot().status),
        "AUTHENTICATED",
      );
      assert.equal((await calls(page, "/api/auth/session")).length, 1);
    },
  );
});
test("detail 403 does not prevent dedicated branch review 200 or disclose redacted fields", async () => {
  await withUsers(
    "/users/42",
    {
      responses: {
        "/api/users/42": [problem(403)],
        "/api/users/42/effective-access": [ok(report())],
      },
    },
    async (page) => {
      await page
        .getByRole("region", { name: "Minimal account detail", exact: true })
        .getByRole("alert")
        .waitFor();
      const review = page.getByRole("region", {
        name: "Access review",
        exact: true,
      });
      await review.getByText("Reviewed Target", { exact: true }).waitFor();
      assert.match(await review.innerText(), /Limited branch report: 1/);
      assert.ok(
        !(await page.locator("body").innerText()).includes(
          "Globally visible department",
        ),
      );
      assert.equal(
        (await calls(page, "/api/users/42/effective-access")).length,
        1,
      );
    },
  );
});
for (const global of [false, true]) {
  test(`server report renders scope, history, grants, effective sources and conditional OWN: global=${global}`, async () => {
    await withUsers(
      "/users/42",
      {
        responses: {
          "/api/users/42": [ok(account())],
          "/api/users/42/effective-access": [ok(report(42, global))],
        },
      },
      async (page) => {
        await page
          .getByRole("table", { name: "Effective permissions", exact: true })
          .waitFor();
        const review = page.getByRole("region", {
          name: "Access review",
          exact: true,
        });
        assert.match(
          await review.innerText(),
          global ? /Global reviewer scope/ : /Limited branch report: 1/,
        );
        assert.match(
          await review
            .getByRole("table", { name: "Assigned roles" })
            .innerText(),
          /Historical Role[\s\S]*false/,
        );
        assert.match(
          await review
            .getByRole("table", { name: "Configured grants" })
            .innerText(),
          /Configured view[\s\S]*false/,
        );
        const effective = await review
          .getByRole("table", { name: "Effective permissions" })
          .innerText();
        assert.match(
          effective,
          /TASK_VIEW[\s\S]*role R2; scope OWN[\s\S]*Resource ownership required/,
        );
        assert.ok(!effective.includes("Configured view"));
        if (!global)
          assert.ok(
            !(await review.innerText()).includes(
              "Globally visible primary branch",
            ),
          );
      },
    );
  });
}
test("review filters abort only the review read; minimal detail remains independent", async () => {
  const filtered =
    "/api/users/42/effective-access?feature=ADMIN&permissionCode=USER_VIEW";
  await withUsers(
    "/users/42",
    {
      responses: {
        "/api/users/42": [ok(account())],
        "/api/users/42/effective-access": [{ deferred: "oldReview" }],
        [filtered]: [
          ok({ ...report(), configuredGrants: [], effectivePermissions: [] }),
        ],
      },
    },
    async (page) => {
      await page.getByText("Minimal Target", { exact: true }).waitFor();
      await page.getByLabel("Feature", { exact: true }).fill("ADMIN");
      await page.getByLabel("Permission code").fill("USER_VIEW");
      await page.getByRole("button", { name: "Apply review filters" }).click();
      await page.getByText("Reviewed Target", { exact: true }).waitFor();
      assert.equal(
        (await calls(page, "/api/users/42/effective-access"))[0].aborted,
        true,
      );
      assert.equal((await calls(page, "/api/users/42")).length, 1);
      await finish(page, "oldReview", ok(report()));
      assert.equal(
        await page
          .getByRole("table", { name: "Effective permissions" })
          .count(),
        0,
      );
      await page.getByText("Minimal Target", { exact: true }).waitFor();
    },
  );
});
test("404 is distinct from branch-scope access denial", async () => {
  await withUsers(
    "/users/42",
    {
      responses: {
        "/api/users/42": [problem(404)],
        "/api/users/42/effective-access": [problem(403)],
      },
    },
    async (page) => {
      await page
        .getByRole("region", { name: "Minimal account detail", exact: true })
        .getByText("The requested account was not found.", { exact: true })
        .waitFor();
      await page
        .getByRole("region", { name: "Access review", exact: true })
        .getByText(/does not confirm whether the account exists/)
        .waitFor();
    },
  );
});
test("invalid route IDs never issue a Users API read", async () => {
  await withUsers("/users/0", {}, async (page) => {
    for (const id of [
      "0",
      "-1",
      "1.5",
      "9007199254740992",
      "9007199254740993",
      "abc",
      "1e3",
    ]) {
      await navigate(page, `/users/${id}`);
      await heading(page, "Invalid user ID");
    }
    assert.equal(
      (await calls(page)).filter((call) => call.url.startsWith("/api/users"))
        .length,
      0,
    );
  });
});
test("maximum safe route ID has validated conversion without rounding", async () => {
  const id = Number.MAX_SAFE_INTEGER;
  await withUsers(
    `/users/${id}`,
    {
      responses: {
        [`/api/users/${id}`]: [ok(account(id))],
        [`/api/users/${id}/effective-access`]: [ok(report(id))],
      },
    },
    async (page) => {
      await page.getByText("Minimal Target", { exact: true }).waitFor();
      await heading(page, `User account ${id}`);
      assert.equal((await calls(page, `/api/users/${id}`)).length, 1);
    },
  );
});
test("target change aborts both old reads and ignores their late data", async () => {
  await withUsers(
    "/users/42",
    {
      responses: {
        "/api/users/42": [{ deferred: "oldDetail" }],
        "/api/users/42/effective-access": [{ deferred: "oldReport" }],
        "/api/users/43": [ok(account(43, "New Target"))],
        "/api/users/43/effective-access": [ok(report(43))],
      },
    },
    async (page) => {
      await heading(page, "User account 42");
      await navigate(page, "/users/43");
      await page.getByText("New Target", { exact: true }).waitFor();
      assert.equal((await calls(page, "/api/users/42"))[0].aborted, true);
      assert.equal(
        (await calls(page, "/api/users/42/effective-access"))[0].aborted,
        true,
      );
      await finish(page, "oldDetail", ok(account(42, "Old Target")));
      await finish(page, "oldReport", ok(report(42)));
      assert.equal(
        await page.getByText("Old Target", { exact: true }).count(),
        0,
      );
      await heading(page, "User account 43");
    },
  );
});
test("unmount aborts reads; late data/error cannot change Profile or navigation", async () => {
  await withUsers(
    "/users/42",
    {
      responses: {
        "/api/users/42": [{ deferred: "detail" }],
        "/api/users/42/effective-access": [{ deferred: "review" }],
      },
    },
    async (page) => {
      await heading(page, "User account 42");
      await page.getByRole("link", { name: "Profile", exact: true }).click();
      await heading(page, "Profile");
      await finish(page, "detail", ok(account()));
      await finish(page, "review", problem(401, "SESSION_INVALID"));
      await pathIs(page, "/profile");
      assert.equal((await calls(page, "/api/auth/session")).length, 1);
      assert.equal(
        await page.getByText("Minimal Target", { exact: true }).count(),
        0,
      );
    },
  );
});

test("directory 401 checks session once and requires explicit Users reload, without retry loop", async () => {
  await withUsers(
    "/users",
    {
      sessions: [ok(session()), ok(session())],
      responses: { [listUrl]: [anon, ok(directory("Recovered Rows"))] },
    },
    async (page) => {
      await page
        .getByRole("button", { name: "Reload users", exact: true })
        .waitFor();
      assert.equal(await page.getByRole("table").count(), 0);
      assert.equal((await calls(page, "/api/auth/session")).length, 2);
      assert.equal((await calls(page, listUrl)).length, 1);
      await settle(page);
      assert.equal((await calls(page, listUrl)).length, 1);
      await page
        .getByRole("button", { name: "Reload users", exact: true })
        .click();
      await page.getByRole("link", { name: "Recovered Rows" }).waitFor();
      assert.equal((await calls(page, "/api/auth/session")).length, 2);
    },
  );
});
test("concurrent detail/review 401 share one revalidation and never restore account data", async () => {
  await withUsers(
    "/users/42",
    {
      sessions: [ok(session()), ok(session())],
      responses: {
        "/api/users/42": [anon],
        "/api/users/42/effective-access": [anon],
      },
    },
    async (page) => {
      await page
        .getByRole("button", { name: "Reload users", exact: true })
        .waitFor();
      assert.equal((await calls(page, "/api/auth/session")).length, 2);
      assert.equal(
        (await calls(page)).filter((call) => call.url.startsWith("/api/users"))
          .length,
        2,
      );
      assert.equal(
        await page
          .getByRole("region", { name: "Minimal account detail" })
          .count(),
        0,
      );
    },
  );
});
test("401 recovery that confirms invalid session redirects Login and clears all Users data", async () => {
  await withUsers(
    "/users",
    { sessions: [ok(session()), anon], responses: { [listUrl]: [anon] } },
    async (page) => {
      await pathIs(page, "/login");
      await heading(page, "Sign In");
      assert.equal(
        await page.evaluate(() => window.usersTest.snapshot().status),
        "EXPIRED_INVALID",
      );
      assert.equal((await calls(page, "/api/auth/session")).length, 2);
      assert.equal((await calls(page, listUrl)).length, 1);
    },
  );
});
test("503 during 401 recovery needs explicit session check and never automatically retries Users", async () => {
  await withUsers(
    "/users",
    {
      sessions: [ok(session()), unavailable, ok(session())],
      responses: { [listUrl]: [anon, ok(directory())] },
    },
    async (page) => {
      await page
        .getByRole("button", { name: "Check session", exact: true })
        .click();
      await page
        .getByRole("button", { name: "Reload users", exact: true })
        .waitFor();
      assert.equal((await calls(page, "/api/auth/session")).length, 3);
      assert.equal((await calls(page, listUrl)).length, 1);
      await page
        .getByRole("button", { name: "Reload users", exact: true })
        .click();
      await page.getByRole("link", { name: "Directory Target" }).waitFor();
    },
  );
});
test("PASSWORD_CHANGE_REQUIRED checks session before applying restricted routing", async () => {
  await withUsers(
    "/users",
    {
      sessions: [ok(session()), ok(session(true))],
      responses: { [listUrl]: [problem(403, "PASSWORD_CHANGE_REQUIRED")] },
    },
    async (page) => {
      await pathIs(page, "/change-password");
      await heading(page, "Change Password");
      assert.equal((await calls(page, "/api/auth/session")).length, 2);
      assert.equal((await calls(page, listUrl)).length, 1);
      assert.equal(await page.getByRole("table").count(), 0);
    },
  );
});
for (const nextId of ["7", "8"]) {
  test(`revalidation/identity replacement masks and aborts old Users reads: next identity=${nextId}`, async () => {
    await withUsers(
      "/users",
      {
        sessions: [ok(session()), { deferred: "session" }],
        responses: {
          [listUrl]: [
            { deferred: "old" },
            ok(directory("Fresh Identity Rows")),
          ],
        },
      },
      async (page) => {
        await heading(page, "User directory");
        await page.evaluate(() => {
          void window.usersTest.revalidate();
        });
        await settle(page);
        assert.equal(
          await page
            .getByRole("heading", { name: "User directory", exact: true })
            .count(),
          0,
        );
        assert.equal((await calls(page, listUrl))[0].aborted, true);
        await finish(page, "session", ok(session(false, nextId)));
        await page.getByRole("link", { name: "Fresh Identity Rows" }).waitFor();
        await finish(page, "old", ok(directory("Old Identity Rows")));
        assert.equal(
          await page.getByRole("link", { name: "Old Identity Rows" }).count(),
          0,
        );
        assert.equal(
          await page.evaluate(
            () => window.usersTest.snapshot().session.user.id,
          ),
          nextId,
        );
      },
    );
  });
}
test("a stale Users 401 cannot trigger recovery after login identity replacement", async () => {
  await withUsers(
    "/users",
    {
      responses: {
        [listUrl]: [{ deferred: "old" }, ok(directory("Post-login Rows"))],
      },
    },
    async (page) => {
      await heading(page, "User directory");
      await page.evaluate(() => window.usersTest.login());
      await page.getByRole("link", { name: "Post-login Rows" }).waitFor();
      await finish(page, "old", anon);
      await page.getByRole("link", { name: "Post-login Rows" }).waitFor();
      assert.equal((await calls(page, "/api/auth/session")).length, 1);
      assert.equal((await calls(page, "/api/auth/login")).length, 1);
    },
  );
});
test("Login and Logout navigation remain unchanged around real Users route", async () => {
  await withUsers(
    "/login",
    { sessions: [anon], responses: { [listUrl]: [ok(directory())] } },
    async (page) => {
      await heading(page, "Sign In");
      await page.getByLabel("Username or email").fill("reviewer");
      await page.getByLabel("Password", { exact: true }).fill(secret);
      await page.getByRole("button", { name: "Sign in", exact: true }).click();
      await pathIs(page, "/dashboard");
      await heading(page, "HotelOps workspace");
      await navigate(page, "/users");
      await page.getByRole("link", { name: "Directory Target" }).waitFor();
      await page.getByRole("link", { name: "Profile", exact: true }).click();
      await heading(page, "Profile");
      await page.getByRole("button", { name: "Logout", exact: true }).click();
      await pathIs(page, "/login");
      await heading(page, "Sign In");
      assert.equal((await calls(page, "/api/auth/login")).length, 1);
      assert.equal((await calls(page, "/api/auth/logout")).length, 1);
    },
  );
});
test("Change Password success still navigates Profile and retains Users route access", async () => {
  await withUsers(
    "/change-password",
    {
      sessions: [ok(session(true))],
      responses: { [listUrl]: [ok(directory())] },
    },
    async (page) => {
      await heading(page, "Change Password");
      await page.getByLabel("Current password", { exact: true }).fill(secret);
      await page
        .getByLabel("New password", { exact: true })
        .fill(`${secret}-next`);
      await page
        .getByLabel("Confirm new password", { exact: true })
        .fill(`${secret}-next`);
      await page
        .getByRole("button", { name: "Change password", exact: true })
        .click();
      await pathIs(page, "/profile");
      await heading(page, "Profile");
      await navigate(page, "/users");
      await page.getByRole("link", { name: "Directory Target" }).waitFor();
      assert.equal((await calls(page, "/api/auth/change-password")).length, 1);
    },
  );
});

test("a review 401 immediately removes previously confirmed minimal detail during recovery", async () => {
  await withUsers(
    "/users/42",
    {
      sessions: [ok(session()), { deferred: "sessionCheck" }],
      responses: {
        "/api/users/42": [ok(account())],
        "/api/users/42/effective-access": [{ deferred: "review401" }],
      },
    },
    async (page) => {
      await page.getByText("Minimal Target", { exact: true }).waitFor();
      await finish(page, "review401", anon);
      assert.equal(
        await page.getByText("Minimal Target", { exact: true }).count(),
        0,
      );
      assert.equal(
        await page
          .getByRole("region", { name: "Minimal account detail", exact: true })
          .count(),
        0,
      );
      await finish(page, "sessionCheck", ok(session()));
      await page
        .getByRole("button", { name: "Reload users", exact: true })
        .waitFor();
      assert.equal(
        await page.getByText("Minimal Target", { exact: true }).count(),
        0,
      );
      assert.equal((await calls(page, "/api/auth/session")).length, 2);
      assert.equal((await calls(page, "/api/users/42")).length, 1);
    },
  );
});

test("leading-zero decimal route ID is checked and converted to a positive safe integer", async () => {
  await withUsers(
    "/users/00042",
    {
      responses: {
        "/api/users/42": [ok(account())],
        "/api/users/42/effective-access": [ok(report())],
      },
    },
    async (page) => {
      await page.getByText("Minimal Target", { exact: true }).waitFor();
      await heading(page, "User account 42");
      assert.equal((await calls(page, "/api/users/42")).length, 1);
    },
  );
});

test("092 unreadable directory 401 checks session once and requires explicit Users reload, without retry loop", async () => {
  await withUsers(
    "/users",
    {
      sessions: [ok(session()), ok(session())],
      responses: {
        [listUrl]: [
          { ...anon, unreadable: true },
          ok(directory("Recovered Rows")),
        ],
      },
    },
    async (page) => {
      await page
        .getByRole("button", { name: "Reload users", exact: true })
        .waitFor();
      assert.equal(await page.getByRole("table").count(), 0);
      assert.equal((await calls(page, "/api/auth/session")).length, 2);
      assert.equal((await calls(page, listUrl)).length, 1);
      await settle(page);
      assert.equal((await calls(page, listUrl)).length, 1);
      await page
        .getByRole("button", { name: "Reload users", exact: true })
        .click();
      await page.getByRole("link", { name: "Recovered Rows" }).waitFor();
      assert.equal((await calls(page, "/api/auth/session")).length, 2);
    },
  );
});
