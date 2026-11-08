import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";

const identity = {
  user: {
    id: "9007199254740993",
    username: "staff.user",
    name: "Confirmed Staff",
  },
  expiresAt: "2026-11-06T12:00:00Z",
  absoluteExpiresAt: "2026-11-06T20:00:00Z",
  mustChangePassword: false,
};
const bundle = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `
    import React, { StrictMode, useState } from "react";
    import { createRoot } from "react-dom/client";
    import { MemoryRouter, useLocation } from "react-router-dom";
    import { AuthProvider } from "./src/auth/AuthProvider";
    import { createAuthStore } from "./src/auth/authStore";
    import ProfileScreen from "./src/screens/auth/ProfileScreen";
    const scenario = window.profileScenario;
    const session = scenario.session ?? ${JSON.stringify(identity)};
    const calls = [];
    let resolve;
    const result = () => {
      calls.push("logout");
      if (scenario.reject) return Promise.reject(new Error(scenario.reject));
      if (scenario.deferred) return new Promise(r => { resolve = r; });
      return Promise.resolve(scenario.reply);
    };
    const snapshot = Object.freeze({ status: scenario.status ?? "AUTHENTICATED",
      session: scenario.hasSession === false ? null : session, pending: scenario.pending ?? null,
      error: scenario.externalError ?? null });
    const fakeStore = {
      getSnapshot: () => snapshot, subscribe: () => () => {},
      initialize: async () => ({ ok: true, data: session }),
      logout: result,
      login: () => { throw new Error("Unexpected login"); },
      changePassword: () => { throw new Error("Unexpected changePassword"); },
      revalidate: () => { throw new Error("Unexpected revalidation"); },
    };
    const realStore = scenario.realStore ? createAuthStore({
      getSession: async () => ({ ok: true, status: 200, data: { ...session, csrfToken: "PRIVATE-TOKEN" } }),
      logout: result,
    }) : null;
    const store = realStore ?? fakeStore;
    let hide;
    function Content() {
      const [visible, setVisible] = useState(true);
      hide = () => setVisible(false);
      return visible ? <ProfileScreen /> : <span id="hidden">Profile removed</span>;
    }
    function Location() { return <output id="location">{useLocation().pathname}</output>; }
    const content = <Content />;
    const root = createRoot(document.getElementById("root"));
    root.render(<StrictMode><MemoryRouter initialEntries={["/profile"]}>
      <Location /><AuthProvider store={store} fallback={scenario.realStore ? null : content}>{content}</AuthProvider>
    </MemoryRouter></StrictMode>);
    window.profileTest = { calls, finish: value => resolve(value), hide: () => hide(), snapshot: () => store.getSnapshot() };
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
after(async () => {
  await browser?.close();
});

const success = { ok: true, status: 204, data: undefined };
const fail = (status, code) => ({
  ok: false,
  error: { kind: "http", status, code, problem: { status, code } },
});

async function withProfile(scenario, run) {
  const page = await browser.newPage();
  const errors = [];
  const logs = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => logs.push(message.text()));
  await page.route("**/*", (route) => route.abort());
  try {
    await page.setContent('<div id="root"></div>');
    await page.evaluate((scenario) => {
      window.profileScenario = scenario;
      window.storageReads = 0;
      window.cookieWrites = 0;
      for (const key of ["localStorage", "sessionStorage"])
        Object.defineProperty(window, key, {
          configurable: true,
          get() {
            window.storageReads++;
            throw new Error("Forbidden browser storage");
          },
        });
      Object.defineProperty(document, "cookie", {
        configurable: true,
        get() {
          return "";
        },
        set() {
          window.cookieWrites++;
          throw new Error("Forbidden cookie mutation");
        },
      });
    }, scenario);
    await page.addScriptTag({ content: bundle.outputFiles[0].text });
    await page.getByRole("heading", { name: "Profile", exact: true }).waitFor();
    await page.evaluate(
      () =>
        new Promise((r) =>
          requestAnimationFrame(() => requestAnimationFrame(r)),
        ),
    );
    await run(page);
    assert.deepEqual(errors, []);
    assert.equal(await page.evaluate(() => window.storageReads), 0);
    assert.equal(await page.evaluate(() => window.cookieWrites), 0);
    assert.ok(
      !logs.some((line) => /PRIVATE-TOKEN|PRIVATE-PASSWORD/.test(line)),
    );
  } finally {
    await page.close();
  }
}

test("confirmed profile renders only PublicSession fields and correct expiry", async () => {
  await withProfile({}, async (page) => {
    assert.equal(
      await page.getByRole("heading", { name: identity.user.name }).count(),
      1,
    );
    assert.deepEqual(await page.locator("dt").allTextContents(), [
      "ID",
      "Username",
      "Session expires",
      "Password change required",
    ]);
    assert.deepEqual(await page.locator("dd").allTextContents(), [
      identity.user.id,
      identity.user.username,
      identity.expiresAt,
      "No",
    ]);
    assert.equal(
      await page.locator("time").getAttribute("datetime"),
      identity.expiresAt,
    );
    assert.equal(
      await page
        .getByRole("link", { name: "Change password" })
        .getAttribute("href"),
      "/change-password",
    );
    assert.deepEqual(await page.evaluate(() => window.profileTest.calls), []);
  });
});

test("restricted identity displays password-change flag without inferring roles", async () => {
  await withProfile(
    {
      status: "RESTRICTED",
      session: { ...identity, mustChangePassword: true },
    },
    async (page) => {
      assert.equal((await page.locator("dd").allTextContents()).at(-1), "Yes");
    },
  );
});

test("extra email/branch/department/roles/permissions/token are never rendered", async () => {
  await withProfile(
    {
      session: {
        ...identity,
        email: "PRIVATE-EMAIL",
        branch: "PRIVATE-BRANCH",
        department: "PRIVATE-DEPARTMENT",
        roles: ["PRIVATE-ROLE"],
        permissions: ["PRIVATE-PERMISSION"],
        csrfToken: "PRIVATE-TOKEN",
      },
    },
    async (page) => {
      assert.ok(
        !(await page.locator("body").textContent()).includes("PRIVATE"),
      );
      assert.ok(
        !/Email|Branch|Department|Roles|Permissions/.test(
          await page.locator("dl").textContent(),
        ),
      );
    },
  );
});

for (const scenario of [
  { hasSession: false },
  { status: "RESTRICTED", hasSession: false },
  { status: "ANONYMOUS" },
  { status: "INITIALIZING" },
  { status: "EXPIRED_INVALID" },
  { status: "ERROR_UNAVAILABLE" },
  { pending: "login" },
]) {
  test(`null/unverified profile is hidden: ${JSON.stringify(scenario)}`, async () => {
    await withProfile(scenario, async (page) => {
      assert.equal(await page.locator("dl").count(), 0);
      const text = await page.locator("body").textContent();
      assert.ok(!text.includes(identity.user.name));
      assert.ok(!text.includes(identity.user.id));
      assert.ok(!text.includes(identity.user.username));
      assert.match(text, /No confirmed profile/);
      assert.deepEqual(await page.evaluate(() => window.profileTest.calls), []);
      assert.equal(await page.locator("#location").textContent(), "/profile");
    });
  });
}

test("accepted logout success navigates to login only after completion", async () => {
  await withProfile({ deferred: true }, async (page) => {
    await page.getByRole("button", { name: "Logout", exact: true }).click();
    assert.equal(await page.locator("#location").textContent(), "/profile");
    assert.equal(await page.locator("dl").count(), 0);
    await page.evaluate((reply) => window.profileTest.finish(reply), success);
    await page.waitForFunction(
      () => document.querySelector("#location")?.textContent === "/login",
    );
    assert.deepEqual(await page.evaluate(() => window.profileTest.calls), [
      "logout",
    ]);
  });
});

for (const [name, reply, expected] of [
  [
    "network/timeout",
    { ok: false, error: { kind: "network" } },
    /could not be confirmed/,
  ],
  ["503", fail(503, "AUTH_SERVICE_UNAVAILABLE"), /currently unavailable/],
  [
    "cancelled",
    { ok: false, error: { kind: "cancelled" } },
    /could not be confirmed/,
  ],
  ["401", fail(401, "SESSION_INVALID"), /session is invalid/],
  ["CSRF", fail(400, "CSRF_VALIDATION_FAILED"), /security check/],
  ["403", fail(403, "FORBIDDEN"), /could not be confirmed/],
  ["busy", { ok: false, error: { kind: "busy" } }, /operation is in progress/],
  ["stale", { ok: false, error: { kind: "stale" } }, /state changed/],
  [
    "invalid-response",
    { ok: false, error: { kind: "invalid-response" } },
    /could not be confirmed/,
  ],
]) {
  test(`logout ${name} is not success, hides identity and never auto-replays`, async () => {
    await withProfile({ reply }, async (page) => {
      await page.getByRole("button", { name: "Logout", exact: true }).click();
      assert.match(await page.getByRole("alert").textContent(), expected);
      assert.equal(await page.locator("#location").textContent(), "/profile");
      assert.deepEqual(await page.evaluate(() => window.profileTest.calls), [
        "logout",
      ]);
      assert.equal(await page.locator("dl").count(), 0);
      assert.ok(
        !(await page.locator("body").textContent()).includes(
          identity.user.name,
        ),
      );
    });
  });
}

test("errors and rejected exceptions cannot expose secrets", async () => {
  for (const scenario of [
    {
      reply: {
        ok: false,
        error: {
          kind: "http",
          status: 500,
          code: "PRIVATE-TOKEN",
          detail: "PRIVATE-PASSWORD",
        },
      },
    },
    { reject: "PRIVATE-TOKEN PRIVATE-PASSWORD" },
  ]) {
    await withProfile(scenario, async (page) => {
      await page.getByRole("button", { name: "Logout", exact: true }).click();
      const text = await page.getByRole("alert").textContent();
      assert.ok(!/PRIVATE/.test(text));
      assert.match(text, /could not be confirmed/);
      assert.equal(await page.locator("#location").textContent(), "/profile");
    });
  }
});

test("double click is synchronously excluded and loading disables the action", async () => {
  await withProfile({ deferred: true }, async (page) => {
    await page
      .getByRole("button", { name: "Logout", exact: true })
      .evaluate((button) => {
        button.click();
        button.click();
      });
    assert.deepEqual(await page.evaluate(() => window.profileTest.calls), [
      "logout",
    ]);
    const button = page.getByRole("button", { name: "Signing out…" });
    assert.equal(await button.isDisabled(), true);
    assert.equal(await button.getAttribute("aria-busy"), "true");
    assert.equal(await page.locator("dl").count(), 0);
    await page.evaluate(
      (reply) => window.profileTest.finish(reply),
      fail(503, "AUTH_SERVICE_UNAVAILABLE"),
    );
    await page.getByRole("alert").waitFor();
  });
});

for (const status of [401, 403]) {
  test(`external ${status} does not trigger automatic logout or navigation`, async () => {
    await withProfile(
      { externalError: { kind: "http", status, code: "FORBIDDEN" } },
      async (page) => {
        assert.deepEqual(
          await page.evaluate(() => window.profileTest.calls),
          [],
        );
        assert.equal(await page.locator("#location").textContent(), "/profile");
      },
    );
  });
}

for (const reply of [success, fail(503, "AUTH_SERVICE_UNAVAILABLE")]) {
  test(`logout response after component unmount is ignored: ok=${reply.ok}`, async () => {
    await withProfile({ deferred: true }, async (page) => {
      await page.getByRole("button", { name: "Logout", exact: true }).click();
      await page.evaluate(() => window.profileTest.hide());
      await page.locator("#hidden").waitFor();
      await page.evaluate((value) => window.profileTest.finish(value), reply);
      await page.evaluate(
        () => new Promise((resolve) => setTimeout(resolve, 0)),
      );
      assert.equal(await page.locator("#location").textContent(), "/profile");
      assert.equal(await page.getByRole("alert").count(), 0);
    });
  });
}

test("component import graph excludes mock services and storage-initializing shared UI", () => {
  const paths = Object.keys(bundle.metafile.inputs).map((path) =>
    path.replaceAll("\\", "/"),
  );
  assert.ok(paths.some((path) => path.endsWith("/ProfileScreen.tsx")));
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

for (const reply of [success, { ok: false, error: { kind: "network" } }]) {
  test(`real AuthStore/provider integration documents unmount limitation: ok=${reply.ok}`, async () => {
    await withProfile({ realStore: true, deferred: true }, async (page) => {
      await page.getByRole("button", { name: "Logout", exact: true }).click();
      await page.waitForFunction(
        () => window.profileTest.snapshot().status === "INITIALIZING",
      );
      assert.equal(
        await page
          .getByRole("heading", { name: "Profile", exact: true })
          .count(),
        0,
      );
      await page.evaluate((value) => window.profileTest.finish(value), reply);
      const expected = reply.ok ? "ANONYMOUS" : "ERROR_UNAVAILABLE";
      await page.waitForFunction(
        (status) => window.profileTest.snapshot().status === status,
        expected,
      );
      await page.evaluate(
        () =>
          new Promise((r) =>
            requestAnimationFrame(() => requestAnimationFrame(r)),
          ),
      );
      assert.equal(await page.locator("#location").textContent(), "/profile");
      assert.equal(await page.locator("dl").count(), 0);
      if (!reply.ok) assert.equal(await page.getByRole("alert").count(), 0); // Default provider fallback hides component error UI too.
      assert.deepEqual(await page.evaluate(() => window.profileTest.calls), [
        "logout",
      ]);
    });
  });
}
