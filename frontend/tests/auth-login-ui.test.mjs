import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";

// Bundle the actual component/provider; mock only the store boundary. No application server.
const bundle = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `
      import React, { StrictMode, useState } from "react";
      import { createRoot } from "react-dom/client";
      import { MemoryRouter, useLocation } from "react-router-dom";
      import { AuthProvider } from "./src/auth/AuthProvider";
      import SignInScreen from "./src/screens/auth/SignInScreen";
      const scenario = window.loginScenario;
      const calls = [];
      let resolve;
      const snapshot = Object.freeze({ status: "ANONYMOUS", session: null, pending: scenario.pending ?? null, error: null });
      const store = {
        getSnapshot: () => snapshot, subscribe: () => () => {},
        initialize: async () => ({ ok: false, error: { kind: "http", status: 401 } }),
        login: request => {
          calls.push({ ...request });
          if (scenario.reject) return Promise.reject(new Error(scenario.reject));
          if (scenario.deferred) return new Promise(r => { resolve = r; });
          return Promise.resolve(scenario.reply);
        },
        logout: () => { throw new Error("Unexpected logout"); },
        changePassword: () => { throw new Error("Unexpected password change"); },
        revalidate: () => { throw new Error("Unexpected revalidation"); },
      };
      function Location() { return <output id="location">{useLocation().pathname}</output>; }
      let hide;
      function Content() {
        const [visible, setVisible] = useState(true);
        hide = () => setVisible(false);
        return visible ? <SignInScreen /> : <span id="hidden">Login removed</span>;
      }
      const root = createRoot(document.getElementById("root"));
      root.render(<StrictMode><MemoryRouter initialEntries={["/login"]}>
        <Location /><AuthProvider store={store}><Content /></AuthProvider>
      </MemoryRouter></StrictMode>);
      window.loginTest = { calls, finish: result => resolve(result), hide: () => hide() };
    `,
  },
  bundle: true,
  write: false,
  metafile: true,
  platform: "browser",
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
after(async () => {
  await browser?.close();
});

const success = (restricted = false) => ({
  ok: true,
  data: {
    user: { id: "42", username: "staff", name: "Staff" },
    expiresAt: "2026-11-06T12:00:00Z",
    absoluteExpiresAt: "2026-11-06T20:00:00Z",
    mustChangePassword: restricted,
  },
});
const http = (status, code) => ({
  ok: false,
  error: { kind: "http", status, code },
});
const credential = { username: " staff ", password: " PrivatePassword!1 " };

async function withLogin(scenario, run) {
  const page = await browser.newPage();
  const errors = [];
  const logs = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => logs.push(message.text()));
  await page.route("**/*", (route) => route.abort());
  try {
    await page.setContent('<div id="root"></div>');
    await page.evaluate((scenario) => {
      window.loginScenario = scenario;
      window.storageReads = 0;
      for (const key of ["localStorage", "sessionStorage"])
        Object.defineProperty(window, key, {
          configurable: true,
          get() {
            window.storageReads++;
            throw new Error("Forbidden browser storage access");
          },
        });
    }, scenario);
    await page.addScriptTag({ content: bundle.outputFiles[0].text });
    await page.locator("#login-username").waitFor();
    // Let StrictMode effect setup/cleanup/setup settle before interactions.
    await page.evaluate(
      () =>
        new Promise((r) =>
          requestAnimationFrame(() => requestAnimationFrame(r)),
        ),
    );
    await run(page);
    assert.equal(await page.evaluate(() => window.storageReads), 0);
    assert.deepEqual(errors, []);
    assert.ok(
      !logs.some(
        (value) =>
          value.includes("PrivatePassword") || value.includes("PRIVATE-TOKEN"),
      ),
    );
  } finally {
    await page.close();
  }
}
async function fill(
  page,
  username = credential.username,
  password = credential.password,
) {
  await page.getByLabel("Username or email").fill(username);
  await page.getByLabel("Password", { exact: true }).fill(password);
}
async function submit(page) {
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
}

test("login render has empty required fields and no demo credentials", async () => {
  await withLogin({ reply: success() }, async (page) => {
    assert.equal(await page.getByLabel("Username or email").inputValue(), "");
    assert.equal(
      await page.getByLabel("Password", { exact: true }).inputValue(),
      "",
    );
    assert.equal(
      await page.getByLabel("Username or email").getAttribute("required"),
      "",
    );
    assert.equal(
      await page
        .getByLabel("Password", { exact: true })
        .getAttribute("required"),
      "",
    );
    assert.equal(
      await page.getByLabel("Password", { exact: true }).getAttribute("type"),
      "password",
    );
    const text = await page.locator("body").textContent();
    assert.ok(
      !/DemoOnly|HotelOps@2026|demo.viewer|Local demo accounts/.test(text),
    );
    assert.equal(
      await page
        .getByRole("link", { name: "Forgot password?" })
        .getAttribute("href"),
      "/forgot-password",
    );
  });
});

for (const [username, password] of [
  ["", ""],
  ["   ", "password"],
  ["staff", ""],
]) {
  test(`required validation prevents login with ${JSON.stringify([username, password])}`, async () => {
    await withLogin({ reply: success() }, async (page) => {
      await fill(page, username, password);
      await submit(page);
      assert.match(await page.getByRole("alert").textContent(), /required/);
      assert.equal(await page.evaluate(() => window.loginTest.calls.length), 0);
      assert.equal(await page.locator("#location").textContent(), "/login");
    });
  });
}

for (const restricted of [false, true]) {
  test(`login ${restricted ? "restricted" : "normal"} navigates correctly and preserves password bytes`, async () => {
    await withLogin({ reply: success(restricted) }, async (page) => {
      await fill(page);
      await submit(page);
      const expected = restricted ? "/change-password" : "/dashboard";
      await page.waitForFunction(
        (path) => document.querySelector("#location")?.textContent === path,
        expected,
      );
      assert.deepEqual(await page.evaluate(() => window.loginTest.calls), [
        { username: "staff", password: credential.password },
      ]);
      assert.equal(
        await page.getByLabel("Password", { exact: true }).inputValue(),
        "",
      );
    });
  });
}

for (const [name, reply, message] of [
  ["401", http(401, "INVALID_CREDENTIALS"), /Invalid username or password/],
  [
    "403",
    http(403, "TEMPORARY_PASSWORD_EXPIRED"),
    /temporary password has expired/,
  ],
  ["409", http(409, "AUTHENTICATED_SESSION_EXISTS"), /session already exists/],
  ["423", http(423, "ACCOUNT_LOCKED"), /account is locked/],
  ["400", http(400, "VALIDATION_FAILED"), /check your username and password/],
  ["503", http(503, "AUTH_SERVICE_UNAVAILABLE"), /currently unavailable/],
  [
    "network",
    { ok: false, error: { kind: "network" } },
    /could not be confirmed/,
  ],
  ["busy", { ok: false, error: { kind: "busy" } }, /operation is in progress/],
  ["stale", { ok: false, error: { kind: "stale" } }, /state changed/],
  [
    "invalid-response",
    { ok: false, error: { kind: "invalid-response" } },
    /currently unavailable/,
  ],
  [
    "cancelled",
    { ok: false, error: { kind: "cancelled" } },
    /could not be confirmed/,
  ],
  ["CSRF", http(400, "CSRF_VALIDATION_FAILED"), /security check/],
]) {
  test(`login ${name} displays safe error without navigation or automatic replay`, async () => {
    await withLogin({ reply }, async (page) => {
      await fill(page);
      await submit(page);
      assert.match(await page.getByRole("alert").textContent(), message);
      assert.equal(await page.locator("#location").textContent(), "/login");
      assert.equal(await page.evaluate(() => window.loginTest.calls.length), 1);
      assert.equal(
        await page.getByLabel("Password", { exact: true }).inputValue(),
        "",
      );
      if (name === "423")
        assert.ok(
          !/\d{1,2}:\d{2}/.test(await page.getByRole("alert").textContent()),
        );
    });
  });
}

test("server detail/code and rejected exception cannot echo credential or token", async () => {
  for (const scenario of [
    {
      reply: {
        ok: false,
        error: {
          kind: "http",
          status: 500,
          code: credential.password,
          detail: "PRIVATE-TOKEN",
          message: credential.password,
        },
      },
    },
    { reject: `${credential.password} PRIVATE-TOKEN` },
  ]) {
    await withLogin(scenario, async (page) => {
      await fill(page);
      await submit(page);
      const error = await page.getByRole("alert").textContent();
      assert.ok(!error.includes(credential.password));
      assert.ok(!error.includes("PRIVATE-TOKEN"));
      assert.match(error, /currently unavailable/);
      assert.equal(await page.locator("#location").textContent(), "/login");
    });
  }
});

test("double submit is excluded synchronously and loading disables credentials", async () => {
  await withLogin({ deferred: true }, async (page) => {
    await fill(page);
    await page.locator("form").evaluate((form) => {
      form.dispatchEvent(
        new Event("submit", { bubbles: true, cancelable: true }),
      );
      form.dispatchEvent(
        new Event("submit", { bubbles: true, cancelable: true }),
      );
    });
    assert.equal(await page.evaluate(() => window.loginTest.calls.length), 1);
    assert.equal(
      await page.getByRole("button", { name: "Signing in…" }).isDisabled(),
      true,
    );
    assert.equal(
      await page.getByLabel("Password", { exact: true }).isDisabled(),
      true,
    );
    assert.equal(await page.locator("form").getAttribute("aria-busy"), "true");
    await page.evaluate((reply) => window.loginTest.finish(reply), success());
    await page.waitForFunction(
      () => document.querySelector("#location")?.textContent === "/dashboard",
    );
  });
});

test("another pending store action disables login and cannot be bypassed by submit event", async () => {
  await withLogin({ pending: "login", reply: success() }, async (page) => {
    assert.equal(
      await page.getByRole("button", { name: "Signing in…" }).isDisabled(),
      true,
    );
    await page
      .locator("form")
      .evaluate((form) =>
        form.dispatchEvent(
          new Event("submit", { bubbles: true, cancelable: true }),
        ),
      );
    assert.equal(await page.evaluate(() => window.loginTest.calls.length), 0);
  });
});

test("explicit resubmit after failure is a user action, not an automatic retry", async () => {
  await withLogin({ reply: http(401, "INVALID_CREDENTIALS") }, async (page) => {
    await fill(page);
    await submit(page);
    await page.getByRole("alert").waitFor();
    assert.equal(await page.evaluate(() => window.loginTest.calls.length), 1);
    await page
      .getByLabel("Password", { exact: true })
      .fill(credential.password);
    await submit(page);
    assert.equal(await page.evaluate(() => window.loginTest.calls.length), 2);
    assert.equal(await page.locator("#location").textContent(), "/login");
  });
});

test("late login result after component unmount cannot navigate", async () => {
  await withLogin({ deferred: true }, async (page) => {
    await fill(page);
    await submit(page);
    await page.evaluate(() => window.loginTest.hide());
    await page.locator("#hidden").waitFor();
    await page.evaluate((reply) => window.loginTest.finish(reply), success());
    await page.evaluate(() => new Promise((resolve) => setTimeout(resolve, 0)));
    assert.equal(await page.locator("#location").textContent(), "/login");
  });
});

test("component bundle has no mock service/shared UI/storage initializer dependency", () => {
  const paths = Object.keys(bundle.metafile.inputs).map((path) =>
    path.replaceAll("\\", "/"),
  );
  assert.ok(paths.some((path) => path.endsWith("/SignInScreen.tsx")));
  assert.ok(
    !paths.some(
      (path) =>
        path.endsWith("/services.ts") ||
        path.endsWith("/ui.tsx") ||
        path.endsWith("/i18n.tsx") ||
        path.includes("/mocks/"),
    ),
  );
});
