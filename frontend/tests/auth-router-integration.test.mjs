import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";

const secret = "PRIVATE-ROUTER-SECRET";
const session = (restricted = false, name = "Confirmed Staff") => ({
  user: { id: "42", username: "staff", name },
  expiresAt: "2026-11-07T12:00:00Z",
  absoluteExpiresAt: "2026-11-07T20:00:00Z",
  mustChangePassword: restricted,
  csrfToken: secret,
});
const ok = (restricted = false, name) => ({ body: session(restricted, name) });
const problem = (status, code) => ({
  status,
  body: { status, code, detail: secret, title: secret, traceId: secret },
});
const anonymous = problem(401, "AUTHENTICATION_REQUIRED");
const unavailable = problem(503, "AUTH_SERVICE_UNAVAILABLE");
const deferred = { deferred: true };

// Every mounted scenario uses the real transport, store, provider and route host.
// Only fetch is replaced. All network requests are intercepted locally.
const bundle = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `
      import React, { StrictMode } from "react";
      import { createRoot } from "react-dom/client";
      import { BrowserRouter } from "react-router-dom";
      import { AuthProvider } from "./src/auth/AuthProvider";
      import { AuthRouteHost } from "./src/auth/AuthRouteHost";
      import { createAuthStore } from "./src/auth/authStore";
      import { createAuthApi } from "./src/auth/authApi";
      const scenario = window.routerScenario;
      const calls = [], waiting = [];
      const queues = {
        "/api/auth/session": [...(scenario.sessions ?? [${JSON.stringify(anonymous)}])],
        "/api/auth/login": [scenario.login ?? ${JSON.stringify(ok())}],
        "/api/auth/logout": [scenario.logout ?? { status: 204 }],
        "/api/auth/change-password": [scenario.password ?? ${JSON.stringify(ok())}],
      };
      function response(reply) {
        if (reply.network) throw new Error(${JSON.stringify(secret)});
        if (reply.cancelled) throw new DOMException(${JSON.stringify(secret)}, "AbortError");
        return new Response(reply.status === 204 ? null : JSON.stringify(reply.body), {
          status: reply.status ?? 200, headers: { "Content-Type": "application/json" },
        });
      }
      const fetcher = async (url, init) => {
        calls.push({ url, method: init.method, credentials: init.credentials, cache: init.cache,
          headers: Object.fromEntries(new Headers(init.headers)), body: init.body });
        if (url === "/api/auth/csrf") return response({ body: { csrfToken: "anonymous-token", headerName: "X-CSRF-TOKEN" } });
        const reply = queues[url]?.shift();
        if (!reply) throw new Error("Unexpected request");
        if (reply.deferred) return new Promise((resolve, reject) => waiting.push({ url, resolve, reject }));
        return response(reply);
      };
      const store = createAuthStore(createAuthApi(fetcher));
      const host = <AuthRouteHost />;
      createRoot(document.getElementById("root")).render(
        <StrictMode><BrowserRouter><AuthProvider store={store} fallback={host}>{host}</AuthProvider></BrowserRouter></StrictMode>
      );
      window.routerTest = {
        calls, snapshot: () => store.getSnapshot(),
        finish(url, reply) {
          const index = waiting.findIndex(item => item.url === url);
          if (index < 0) throw new Error("No pending request");
          const item = waiting.splice(index, 1)[0];
          try { item.resolve(response(reply)); } catch (error) { item.reject(error); }
        },
        navigate(path) { history.pushState(null, "", path); dispatchEvent(new PopStateEvent("popstate")); },
        revalidate: () => store.revalidate(),
        // Deliberately force overlapping operations to verify real store epoch protection.
        login: () => store.login({ username: "staff", password: ${JSON.stringify(secret)} }),
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

const entry = await build({
  entryPoints: ["src/main.tsx"],
  outdir: ".test-output/router-memory-only",
  bundle: true,
  write: false,
  metafile: true,
  format: "iife",
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
      new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))),
  );
}
async function withRouter(path, scenario, run, actualEntry = false) {
  const page = await browser.newPage();
  const errors = [],
    logs = [],
    unexpectedNetwork = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => logs.push(message.text()));
  await page.route("**/*", (route) => {
    if (route.request().resourceType() === "document")
      return route.fulfill({
        contentType: "text/html",
        body: '<div id="root"></div>',
      });
    unexpectedNetwork.push(route.request().url());
    return route.abort();
  });
  try {
    await page.goto(`http://hotelops.test${path}`);
    await page.evaluate((scenario) => {
      window.routerScenario = scenario;
      window.storageReads = 0;
      for (const key of ["localStorage", "sessionStorage"])
        Object.defineProperty(window, key, {
          configurable: true,
          get() {
            window.storageReads++;
            throw new Error("Forbidden storage");
          },
        });
      window.entryCalls = [];
      window.fetch = async (url, init) => {
        window.entryCalls.push({ url, method: init.method });
        return new Response(
          JSON.stringify({ status: 401, code: "AUTHENTICATION_REQUIRED" }),
          {
            status: 401,
            headers: { "Content-Type": "application/json" },
          },
        );
      };
    }, scenario);
    await page.addScriptTag({
      content: actualEntry
        ? entry.outputFiles.find((file) => file.path.endsWith(".js")).text
        : bundle.outputFiles[0].text,
    });
    await settle(page);
    await run(page);
    await settle(page);
    assert.deepEqual(errors, []);
    assert.deepEqual(unexpectedNetwork, []);
    assert.equal(await page.evaluate(() => window.storageReads), 0);
    assert.ok(!logs.some((line) => line.includes(secret)));
    assert.ok(!(await page.locator("body").innerText()).includes(secret));
    if (!actualEntry) {
      for (const call of await page.evaluate(() => window.routerTest.calls)) {
        assert.match(call.url, /^\/api\/auth\//);
        assert.equal(call.credentials, "include");
        assert.equal(call.cache, "no-store");
        assert.equal(call.headers.authorization, undefined);
        if (call.method === "POST") assert.ok(call.headers["x-csrf-token"]);
        else assert.equal(call.method, "GET");
      }
    }
  } finally {
    await page.close();
  }
}
async function pathIs(page, path) {
  await page.waitForFunction((path) => location.pathname === path, path);
}
async function heading(page, name) {
  await page.getByRole("heading", { name, exact: true }).waitFor();
}
async function calls(page, url) {
  return page.evaluate(
    (url) => window.routerTest.calls.filter((call) => call.url === url),
    url,
  );
}
async function finish(page, url, reply) {
  await page.evaluate(
    ({ url, reply }) => window.routerTest.finish(url, reply),
    { url, reply },
  );
  await settle(page);
}
async function login(page) {
  await page.getByLabel("Username or email").fill(" staff ");
  await page.getByLabel("Password", { exact: true }).fill(secret);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
}
async function password(page) {
  await page.getByLabel("Current password", { exact: true }).fill(secret);
  await page.getByLabel("New password", { exact: true }).fill(`${secret}-next`);
  await page
    .getByLabel("Confirm new password", { exact: true })
    .fill(`${secret}-next`);
  await page
    .getByRole("button", { name: "Change password", exact: true })
    .click();
}
async function remember(page, selector) {
  await page.evaluate((selector) => {
    window.originalNode = document.querySelector(selector);
    window.removedOriginal = false;
    window.nodeObserver = new MutationObserver((records) => {
      for (const record of records)
        for (const node of record.removedNodes)
          if (
            node === window.originalNode ||
            node.contains?.(window.originalNode)
          )
            window.removedOriginal = true;
    });
    window.nodeObserver.observe(document.getElementById("root"), {
      childList: true,
      subtree: true,
    });
  }, selector);
}
async function sameNode(page, selector) {
  assert.equal(
    await page.evaluate(
      (selector) =>
        window.originalNode === document.querySelector(selector) &&
        window.originalNode.isConnected &&
        !window.removedOriginal,
      selector,
    ),
    true,
  );
}

test("entry and auth-only import graphs exclude prototype/storage initializers", () => {
  for (const graph of [entry, bundle]) {
    const inputs = Object.keys(graph.metafile.inputs).map((path) =>
      path.replaceAll("\\", "/"),
    );
    assert.ok(inputs.some((path) => path.endsWith("auth/AuthRouteHost.tsx")));
    assert.ok(
      !inputs.some((path) =>
        /src\/(services(?:\/|\.ts)|ui\.tsx|i18n\.tsx|mock|routes\/(routeConfig|ScreenIndex|DesignSystem))/.test(
          path,
        ),
      ),
      inputs.join("\n"),
    );
  }
  const source = readFileSync("src/main.tsx", "utf8");
  assert.match(source, /fallback=\{host\}>\{host\}/);
});

test("actual main entry mounts provider once and renders anonymous login without mock storage", async () => {
  await withRouter(
    "/login",
    {},
    async (page) => {
      await heading(page, "Sign In");
      assert.deepEqual(await page.evaluate(() => window.entryCalls), [
        { url: "/api/auth/session", method: "GET" },
      ]);
    },
    true,
  );
});

test("startup GET pending fails closed and StrictMode initializes once", async () => {
  await withRouter("/dashboard", { sessions: [deferred] }, async (page) => {
    await page.getByRole("status").waitFor();
    assert.equal(await page.getByRole("heading").count(), 0);
    assert.equal((await calls(page, "/api/auth/session")).length, 1);
    await finish(page, "/api/auth/session", anonymous);
    await pathIs(page, "/login");
    await heading(page, "Sign In");
  });
});

for (const restricted of [false, true]) {
  test(`startup 200 redirects login according to confirmed session: restricted=${restricted}`, async () => {
    await withRouter("/login", { sessions: [ok(restricted)] }, async (page) => {
      await pathIs(page, restricted ? "/change-password" : "/dashboard");
      await heading(
        page,
        restricted ? "Change Password" : "HotelOps workspace",
      );
      assert.equal((await calls(page, "/api/auth/session")).length, 1);
    });
  });
  test(`confirmed profile shows only public identity: restricted=${restricted}`, async () => {
    await withRouter(
      "/profile",
      { sessions: [ok(restricted)] },
      async (page) => {
        await heading(page, "Confirmed Staff");
        await page
          .getByRole("link", { name: "Change password", exact: true })
          .waitFor();
        assert.ok(
          !(
            "csrfToken" in
            (await page.evaluate(() => window.routerTest.snapshot().session))
          ),
        );
      },
    );
  });
}

for (const route of [
  "/profile",
  "/change-password",
  "/dashboard",
  "/users",
  "/roles",
  "/permissions",
  "/rooms/42",
  "/workdays",
]) {
  test(`anonymous direct URL is guarded: ${route}`, async () => {
    await withRouter(route, {}, async (page) => {
      await pathIs(page, "/login");
      await heading(page, "Sign In");
      assert.equal(
        await page
          .getByText("This business screen is not integrated yet.")
          .count(),
        0,
      );
    });
  });
}
test("restricted business URL cannot render business placeholder", async () => {
  await withRouter("/users", { sessions: [ok(true)] }, async (page) => {
    await pathIs(page, "/change-password");
    await heading(page, "Change Password");
    assert.equal(
      await page
        .getByText("This business screen is not integrated yet.")
        .count(),
      0,
    );
  });
});
test("authenticated business URL shows only unintegrated placeholder", async () => {
  await withRouter("/rooms/42", { sessions: [ok()] }, async (page) => {
    await heading(page, "HotelOps workspace");
    assert.equal(
      await page
        .getByText("This business screen is not integrated yet.")
        .count(),
      1,
    );
  });
});

for (const [label, failure] of [
  ["503", unavailable],
  ["network", { network: true }],
]) {
  test(`startup unavailable stays unknown until explicit recovery: ${label}`, async () => {
    await withRouter(
      "/dashboard",
      { sessions: [failure, ok()] },
      async (page) => {
        await page.getByRole("button", { name: "Check session" }).waitFor();
        assert.equal(await page.getByRole("heading").count(), 0);
        assert.equal((await calls(page, "/api/auth/session")).length, 1);
        await page.getByRole("button", { name: "Check session" }).click();
        await heading(page, "HotelOps workspace");
        assert.equal((await calls(page, "/api/auth/session")).length, 2);
      },
    );
  });
}

for (const delayed of [false, true]) {
  for (const restricted of [false, true]) {
    test(`login success actual transport: deferred=${delayed}, restricted=${restricted}`, async () => {
      await withRouter(
        "/login",
        { login: delayed ? deferred : ok(restricted) },
        async (page) => {
          await heading(page, "Sign In");
          await remember(page, "form");
          await login(page);
          if (delayed) {
            await page.waitForFunction(
              () => window.routerTest.snapshot().pending === "login",
            );
            await settle(page);
            await sameNode(page, "form");
            assert.equal(
              await page.getByLabel("Username or email").inputValue(),
              " staff ",
            );
            await finish(page, "/api/auth/login", ok(restricted));
          }
          await pathIs(page, restricted ? "/change-password" : "/dashboard");
          await heading(
            page,
            restricted ? "Change Password" : "HotelOps workspace",
          );
          const posts = await calls(page, "/api/auth/login");
          assert.equal(posts.length, 1);
          assert.equal(posts[0].method, "POST");
          assert.equal(posts[0].credentials, "include");
          assert.equal(posts[0].headers["x-csrf-token"], "anonymous-token");
          assert.deepEqual(JSON.parse(posts[0].body), {
            username: "staff",
            password: secret,
          });
        },
      );
    });
  }
  test(`login 401 preserves local error and DOM form: deferred=${delayed}`, async () => {
    const failure = problem(401, "INVALID_CREDENTIALS");
    await withRouter(
      "/login",
      { login: delayed ? deferred : failure },
      async (page) => {
        await heading(page, "Sign In");
        await remember(page, "form");
        await login(page);
        if (delayed) {
          await settle(page);
          await sameNode(page, "form");
          // Synchronous ref/disabled control also excludes duplicate POSTs.
          await page.locator("form").evaluate((form) => form.requestSubmit());
          await finish(page, "/api/auth/login", failure);
        }
        await page
          .getByRole("alert")
          .filter({ hasText: "Invalid username or password." })
          .waitFor();
        await sameNode(page, "form");
        assert.equal((await calls(page, "/api/auth/login")).length, 1);
        await pathIs(page, "/login");
      },
    );
  });
  test(`logout success hides identity while pending and redirects: deferred=${delayed}`, async () => {
    await withRouter(
      "/profile",
      { sessions: [ok()], logout: delayed ? deferred : { status: 204 } },
      async (page) => {
        await heading(page, "Confirmed Staff");
        await remember(page, "section.panel");
        await page.getByRole("button", { name: "Logout", exact: true }).click();
        if (delayed) {
          await settle(page);
          await sameNode(page, "section.panel");
          assert.equal(
            await page
              .getByRole("heading", { name: "Confirmed Staff" })
              .count(),
            0,
          );
          await finish(page, "/api/auth/logout", { status: 204 });
        }
        await pathIs(page, "/login");
        await heading(page, "Sign In");
        const posts = await calls(page, "/api/auth/logout");
        assert.equal(posts.length, 1);
        assert.equal(posts[0].method, "POST");
        assert.equal(posts[0].headers["x-csrf-token"], secret);
      },
    );
  });
  for (const restricted of [false, true]) {
    test(`password success preserves form pending and accepts fresh session: deferred=${delayed}, restricted=${restricted}`, async () => {
      await withRouter(
        "/change-password",
        {
          sessions: [ok(restricted)],
          password: delayed ? deferred : ok(false, "Fresh Staff"),
        },
        async (page) => {
          await heading(page, "Change Password");
          await remember(page, "form");
          await password(page);
          if (delayed) {
            await settle(page);
            await sameNode(page, "form");
            await finish(
              page,
              "/api/auth/change-password",
              ok(false, "Fresh Staff"),
            );
          }
          await pathIs(page, "/profile");
          await heading(page, "Fresh Staff");
          const posts = await calls(page, "/api/auth/change-password");
          assert.equal(posts.length, 1);
          assert.equal(posts[0].method, "POST");
          assert.equal(posts[0].headers["x-csrf-token"], secret);
          assert.deepEqual(JSON.parse(posts[0].body), {
            currentPassword: secret,
            newPassword: `${secret}-next`,
            confirmNewPassword: `${secret}-next`,
          });
        },
      );
    });
  }
  test(`password policy error remains visible without remount: deferred=${delayed}`, async () => {
    const failure = problem(400, "PASSWORD_POLICY_VIOLATION");
    await withRouter(
      "/change-password",
      { sessions: [ok(true)], password: delayed ? deferred : failure },
      async (page) => {
        await heading(page, "Change Password");
        await remember(page, "form");
        await password(page);
        if (delayed) {
          await settle(page);
          await sameNode(page, "form");
          await finish(page, "/api/auth/change-password", failure);
        }
        await page
          .getByRole("alert")
          .filter({ hasText: "server password policy" })
          .waitFor();
        await sameNode(page, "form");
        await pathIs(page, "/change-password");
        assert.equal(
          (await calls(page, "/api/auth/change-password")).length,
          1,
        );
      },
    );
  });
}

for (const [label, failure] of [
  ["network", { network: true }],
  ["503", unavailable],
  ["CSRF", problem(400, "CSRF_VALIDATION_FAILED")],
  ["cancellation", { cancelled: true }],
]) {
  test(`logout indeterminate outcome offers GET recovery without POST replay: ${label}`, async () => {
    await withRouter(
      "/profile",
      { sessions: [ok(), ok()], logout: deferred },
      async (page) => {
        await heading(page, "Confirmed Staff");
        await remember(page, "section.panel");
        await page.getByRole("button", { name: "Logout", exact: true }).click();
        await settle(page);
        await sameNode(page, "section.panel");
        await finish(page, "/api/auth/logout", failure);
        await page.getByRole("button", { name: "Check session" }).waitFor();
        await sameNode(page, "section.panel");
        assert.equal(
          await page.getByRole("heading", { name: "Confirmed Staff" }).count(),
          0,
        );
        assert.match(
          await page.locator("body").innerText(),
          /not confirmed|could not be confirmed/,
        );
        await pathIs(page, "/profile");
        await page.getByRole("button", { name: "Check session" }).click();
        await heading(page, "Confirmed Staff");
        assert.equal((await calls(page, "/api/auth/logout")).length, 1);
        assert.equal((await calls(page, "/api/auth/session")).length, 2);
      },
    );
  });
}

for (const path of ["/dev/screens", "/dev/design-system"]) {
  for (const initial of [anonymous, ok(), ok(true)]) {
    test(`prototype unavailable regardless of identity: ${path}, ${initial.body.mustChangePassword ?? "anonymous"}`, async () => {
      await withRouter(
        `${path}?demo=true`,
        { sessions: [initial] },
        async (page) => {
          await heading(page, "Prototype unavailable");
          assert.equal(await page.locator("a[data-screen-id]").count(), 0);
        },
      );
    });
  }
}
test("forgot password shows instruction only after identifier confirmation without recovery request", async () => {
  await withRouter("/forgot-password", {}, async (page) => {
    await heading(page, "Forgot Password");
    const identifier = page.getByRole("textbox", { name: "Username or email" });
    const instruction =
      "For password assistance, contact your Administrator through your usual internal support channel.";
    assert.equal(await identifier.count(), 1);
    assert.equal(await page.getByText(instruction, { exact: true }).count(), 0);

    await page.getByRole("button", { name: "Show instructions" }).click();
    await page
      .getByRole("alert")
      .filter({ hasText: "Enter your username or email." })
      .waitFor();
    assert.equal(await page.getByText(instruction, { exact: true }).count(), 0);

    const identifierLogs = [];
    page.on("console", (message) => identifierLogs.push(message.text()));
    await identifier.fill("private-identifier@example.test");
    await page.getByRole("button", { name: "Show instructions" }).click();
    await page.getByRole("status").filter({ hasText: instruction }).waitFor();
    assert.equal(await identifier.inputValue(), "");
    assert.ok(
      !identifierLogs.join(" ").includes("private-identifier@example.test"),
    );
    assert.ok(
      !JSON.stringify(
        await page.evaluate(() => window.routerTest.calls),
      ).includes("private-identifier@example.test"),
    );
    assert.ok(
      !(await page.locator("body").innerText()).includes(
        "private-identifier@example.test",
      ),
    );
    assert.deepEqual(
      (await page.evaluate(() => window.routerTest.calls)).map(
        (call) => call.url,
      ),
      ["/api/auth/session"],
    );
  });
});

test("late GET cannot replace completed login session", async () => {
  await withRouter(
    "/login",
    { sessions: [anonymous, deferred], login: ok(false, "New Identity") },
    async (page) => {
      await heading(page, "Sign In");
      await page.evaluate(() => {
        void window.routerTest.revalidate();
      });
      await settle(page);
      await page.evaluate(() => {
        void window.routerTest.login();
      });
      await pathIs(page, "/dashboard");
      await finish(page, "/api/auth/session", ok(true, "Old Identity"));
      assert.equal(
        await page.evaluate(
          () => window.routerTest.snapshot().session.user.name,
        ),
        "New Identity",
      );
      await pathIs(page, "/dashboard");
      assert.equal((await calls(page, "/api/auth/login")).length, 1);
    },
  );
});

test("revalidation 401 removes confirmed identity and guards back/forward history", async () => {
  await withRouter("/profile", { sessions: [ok(), deferred] }, async (page) => {
    await heading(page, "Confirmed Staff");
    await page.evaluate(() => window.routerTest.navigate("/dashboard"));
    await heading(page, "HotelOps workspace");
    await page.evaluate(() => {
      void window.routerTest.revalidate();
    });
    await settle(page);
    assert.equal(
      await page.getByRole("heading", { name: "HotelOps workspace" }).count(),
      0,
    );
    await finish(page, "/api/auth/session", anonymous);
    await pathIs(page, "/login");
    await page.goBack();
    await pathIs(page, "/login");
    await heading(page, "Sign In");
    await page.goForward();
    await pathIs(page, "/login");
    await heading(page, "Sign In");
    assert.equal(
      await page.getByRole("heading", { name: "Confirmed Staff" }).count(),
      0,
    );
  });
});

test("late login response after user navigation cannot hijack informational route", async () => {
  await withRouter("/login", { login: deferred }, async (page) => {
    await heading(page, "Sign In");
    await login(page);
    await page.getByRole("link", { name: "Forgot password?" }).click();
    await pathIs(page, "/forgot-password");
    await finish(page, "/api/auth/login", ok());
    await pathIs(page, "/forgot-password");
    await heading(page, "Forgot Password");
    assert.equal((await calls(page, "/api/auth/login")).length, 1);
  });
});

test("an unresolved logout remains pending with identity hidden and no revocation claim", async () => {
  await withRouter(
    "/profile",
    { sessions: [ok()], logout: deferred },
    async (page) => {
      await heading(page, "Confirmed Staff");
      await page.getByRole("button", { name: "Logout", exact: true }).click();
      await settle(page);
      await pathIs(page, "/profile");
      assert.equal(
        await page.getByRole("heading", { name: "Confirmed Staff" }).count(),
        0,
      );
      assert.equal(
        await page.getByRole("button", { name: /Signing out/ }).isDisabled(),
        true,
      );
      assert.equal(
        await page.evaluate(() => window.routerTest.snapshot().pending),
        "logout",
      );
      assert.equal((await calls(page, "/api/auth/logout")).length, 1);
      assert.ok(
        !/successfully|revoked/i.test(await page.locator("body").innerText()),
      );
    },
  );
});

test("session GET 403 is unavailable rather than proof of anonymous or deleted identity", async () => {
  await withRouter(
    "/profile",
    { sessions: [ok(), problem(403, "FORBIDDEN"), ok()] },
    async (page) => {
      await heading(page, "Confirmed Staff");
      await page.evaluate(() => window.routerTest.revalidate());
      await page.getByRole("button", { name: "Check session" }).waitFor();
      assert.equal(
        await page.evaluate(() => window.routerTest.snapshot().status),
        "ERROR_UNAVAILABLE",
      );
      await pathIs(page, "/profile");
      assert.equal(
        await page.getByRole("heading", { name: "Confirmed Staff" }).count(),
        0,
      );
      await page.getByRole("button", { name: "Check session" }).click();
      await heading(page, "Confirmed Staff");
      assert.equal((await calls(page, "/api/auth/logout")).length, 0);
    },
  );
});
