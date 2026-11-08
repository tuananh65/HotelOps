import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";

const session = (restricted = false) => ({
  user: { id: "42", username: "staff", name: "Staff" },
  expiresAt: "2026-11-06T12:00:00Z",
  absoluteExpiresAt: "2026-11-06T20:00:00Z",
  mustChangePassword: restricted,
});
const success = { ok: true, data: session() };
const http = (status, code) => ({
  ok: false,
  error: { kind: "http", status, code },
});
const values = {
  currentPassword: " PRIVATE-Current!1 ",
  newPassword: " PRIVATE-New!2 ",
  confirmNewPassword: " PRIVATE-New!2 ",
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
    import ChangePasswordScreen from "./src/screens/auth/ChangePasswordScreen";
    const scenario = window.changeScenario;
    const initialSession = { ...${JSON.stringify(session())}, mustChangePassword: scenario.restricted ?? false };
    const calls = [];
    let resolve;
    const asApiResult = reply => {
      if (!scenario.realStore) return reply; // Mock-store operations return sanitized AuthResult.
      if (reply.ok) return { ok: true, status: 200, data: { ...reply.data, csrfToken: "PRIVATE-TOKEN" } };
      if (reply.error.kind === "http") return { ok: false, error: {
        kind: "http", status: reply.error.status,
        problem: { status: reply.error.status, code: reply.error.code },
      } }; // A real store consumes ApiResult/ProblemDetails, not a public AuthError.
      return reply;
    };
    const changePassword = request => {
      calls.push({ ...request });
      if (scenario.reject) return Promise.reject(new Error(scenario.reject));
      if (scenario.deferred) return new Promise(r => { resolve = r; });
      return Promise.resolve(asApiResult(scenario.reply));
    };
    const snapshot = Object.freeze({ status: scenario.status ?? (scenario.restricted ? "RESTRICTED" : "AUTHENTICATED"),
      session: scenario.hasSession === false ? null : initialSession, pending: scenario.pending ?? null, error: null });
    const fakeStore = {
      getSnapshot: () => snapshot, subscribe: () => () => {},
      initialize: async () => ({ ok: true, data: initialSession }), changePassword,
      login: () => { throw new Error("Unexpected login"); }, logout: () => { throw new Error("Unexpected logout"); },
      revalidate: () => { throw new Error("Unexpected revalidation"); },
    };
    const store = scenario.realStore ? createAuthStore({
      getSession: async () => ({ ok: true, status: 200, data: { ...initialSession, csrfToken: "PRIVATE-TOKEN" } }),
      changePassword,
    }) : fakeStore;
    let hide;
    function Content() {
      const [visible, setVisible] = useState(true);
      hide = () => setVisible(false);
      return visible ? <ChangePasswordScreen /> : <span id="hidden">Form removed</span>;
    }
    function Location() { return <output id="location">{useLocation().pathname}</output>; }
    const content = <Content />;
    const root = createRoot(document.getElementById("root"));
    root.render(<StrictMode><MemoryRouter initialEntries={["/change-password"]}>
      <Location /><AuthProvider store={store} fallback={scenario.realStore ? null : content}>{content}</AuthProvider>
    </MemoryRouter></StrictMode>);
    window.changeTest = { calls, hide: () => hide(), finish: reply => resolve(asApiResult(reply)), snapshot: () => store.getSnapshot() };
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

async function withChange(scenario, run) {
  const page = await browser.newPage();
  const errors = [];
  const logs = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => logs.push(message.text()));
  await page.route("**/*", (route) => route.abort());
  try {
    await page.setContent('<div id="root"></div>');
    await page.evaluate((scenario) => {
      window.changeScenario = scenario;
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
          throw new Error("Forbidden cookie write");
        },
      });
    }, scenario);
    await page.addScriptTag({ content: bundle.outputFiles[0].text });
    await page
      .getByRole("heading", { name: "Change Password", exact: true })
      .waitFor();
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
    assert.ok(!logs.some((line) => /PRIVATE-|PRIVATE-TOKEN/.test(line)));
  } finally {
    await page.close();
  }
}
async function fill(page, input = values) {
  await page
    .getByLabel("Current password", { exact: true })
    .fill(input.currentPassword);
  await page
    .getByLabel("New password", { exact: true })
    .fill(input.newPassword);
  await page
    .getByLabel("Confirm new password", { exact: true })
    .fill(input.confirmNewPassword);
}
async function submit(page) {
  await page
    .getByRole("button", { name: "Change password", exact: true })
    .click();
}
async function assertCleared(page) {
  for (const field of [
    "Current password",
    "New password",
    "Confirm new password",
  ]) {
    assert.equal(
      await page.getByLabel(field, { exact: true }).inputValue(),
      "",
    );
  }
}

test("render has empty password inputs, autocomplete and no demo credentials", async () => {
  await withChange({ reply: success }, async (page) => {
    await assertCleared(page);
    for (const field of [
      "Current password",
      "New password",
      "Confirm new password",
    ]) {
      assert.equal(
        await page.getByLabel(field, { exact: true }).getAttribute("type"),
        "password",
      );
      assert.equal(
        await page.getByLabel(field, { exact: true }).getAttribute("required"),
        "",
      );
    }
    assert.equal(
      await page
        .getByLabel("Current password", { exact: true })
        .getAttribute("autocomplete"),
      "current-password",
    );
    assert.equal(
      await page
        .getByLabel("New password", { exact: true })
        .getAttribute("autocomplete"),
      "new-password",
    );
    assert.ok(
      !/DemoOnly|HotelOps@2026|demo.viewer/.test(
        await page.locator("body").textContent(),
      ),
    );
  });
});

for (const missing of [
  "all",
  "currentPassword",
  "newPassword",
  "confirmNewPassword",
]) {
  test(`required validation blocks request: ${missing}`, async () => {
    await withChange({ reply: success }, async (page) => {
      const input =
        missing === "all"
          ? { currentPassword: "", newPassword: "", confirmNewPassword: "" }
          : { ...values, [missing]: "" };
      await fill(page, input);
      await submit(page);
      assert.match(await page.getByRole("alert").textContent(), /required/);
      assert.deepEqual(await page.evaluate(() => window.changeTest.calls), []);
      assert.equal(
        await page.locator("#location").textContent(),
        "/change-password",
      );
    });
  });
}

test("confirmation mismatch is rejected before action without trimming either value", async () => {
  await withChange({ reply: success }, async (page) => {
    await fill(page, {
      ...values,
      confirmNewPassword: values.newPassword.slice(1),
    });
    await submit(page);
    assert.match(await page.getByRole("alert").textContent(), /must match/);
    assert.deepEqual(await page.evaluate(() => window.changeTest.calls), []);
  });
});

for (const restricted of [false, true]) {
  test(`${restricted ? "restricted" : "normal"} success preserves all password bytes and navigates`, async () => {
    await withChange({ restricted, reply: success }, async (page) => {
      await fill(page);
      await submit(page);
      await page.waitForFunction(
        () => document.querySelector("#location")?.textContent === "/profile",
      );
      assert.deepEqual(await page.evaluate(() => window.changeTest.calls), [
        values,
      ]);
      await assertCleared(page);
    });
  });
}

test("frontend does not replace backend password policy", async () => {
  await withChange(
    { reply: http(400, "PASSWORD_POLICY_VIOLATION") },
    async (page) => {
      const input = {
        currentPassword: " ",
        newPassword: "x",
        confirmNewPassword: "x",
      };
      await fill(page, input);
      await submit(page);
      assert.deepEqual(await page.evaluate(() => window.changeTest.calls), [
        input,
      ]);
      assert.match(
        await page.getByRole("alert").textContent(),
        /server password policy/,
      );
    },
  );
});

for (const [name, reply, message] of [
  [
    "current password",
    http(400, "CURRENT_PASSWORD_INVALID"),
    /current password is incorrect/,
  ],
  ["policy", http(400, "PASSWORD_POLICY_VIOLATION"), /server password policy/],
  ["validation", http(400, "VALIDATION_FAILED"), /check all password fields/],
  ["401", http(401, "SESSION_INVALID"), /session is no longer valid/],
  ["409", http(409, "AUTH_STATE_CHANGED"), /state changed/],
  ["CSRF", http(400, "CSRF_VALIDATION_FAILED"), /security check/],
  [
    "network",
    { ok: false, error: { kind: "network" } },
    /could not be confirmed/,
  ],
  ["503", http(503, "AUTH_SERVICE_UNAVAILABLE"), /currently unavailable/],
  [
    "cancelled",
    { ok: false, error: { kind: "cancelled" } },
    /could not be confirmed/,
  ],
  ["busy", { ok: false, error: { kind: "busy" } }, /operation is in progress/],
  ["stale", { ok: false, error: { kind: "stale" } }, /state changed/],
  [
    "invalid-response",
    { ok: false, error: { kind: "invalid-response" } },
    /could not be confirmed/,
  ],
]) {
  test(`change-password ${name} displays safe failure without navigation or replay`, async () => {
    await withChange({ reply }, async (page) => {
      await fill(page);
      await submit(page);
      assert.match(await page.getByRole("alert").textContent(), message);
      assert.equal(
        await page.locator("#location").textContent(),
        "/change-password",
      );
      assert.deepEqual(await page.evaluate(() => window.changeTest.calls), [
        values,
      ]);
      await assertCleared(page);
    });
  });
}

test("double submission is synchronously excluded with pending controls", async () => {
  await withChange({ deferred: true }, async (page) => {
    await fill(page);
    await page.locator("form").evaluate((form) => {
      form.dispatchEvent(
        new Event("submit", { bubbles: true, cancelable: true }),
      );
      form.dispatchEvent(
        new Event("submit", { bubbles: true, cancelable: true }),
      );
    });
    assert.deepEqual(await page.evaluate(() => window.changeTest.calls), [
      values,
    ]);
    assert.equal(
      await page
        .getByRole("button", { name: "Changing password…" })
        .isDisabled(),
      true,
    );
    assert.equal(
      await page.getByLabel("Current password", { exact: true }).isDisabled(),
      true,
    );
    assert.equal(await page.locator("form").getAttribute("aria-busy"), "true");
    await page.evaluate((reply) => window.changeTest.finish(reply), success);
    await page.waitForFunction(
      () => document.querySelector("#location")?.textContent === "/profile",
    );
  });
});

for (const scenario of [
  { hasSession: false },
  { status: "ANONYMOUS" },
  { pending: "logout" },
]) {
  test(`unconfirmed or pending session cannot submit: ${JSON.stringify(scenario)}`, async () => {
    await withChange({ ...scenario, reply: success }, async (page) => {
      assert.equal(
        await page.locator("button[type=submit]").isDisabled(),
        true,
      );
      await page
        .locator("form")
        .evaluate((form) =>
          form.dispatchEvent(
            new Event("submit", { bubbles: true, cancelable: true }),
          ),
        );
      assert.deepEqual(await page.evaluate(() => window.changeTest.calls), []);
    });
  });
}

test("raw errors and rejected exceptions cannot echo credentials or tokens", async () => {
  for (const scenario of [
    {
      reply: {
        ok: false,
        error: {
          kind: "http",
          status: 500,
          code: values.currentPassword,
          detail: "PRIVATE-TOKEN",
          message: values.newPassword,
        },
      },
    },
    { reject: `${values.currentPassword} PRIVATE-TOKEN` },
  ]) {
    await withChange(scenario, async (page) => {
      await fill(page);
      await submit(page);
      const error = await page.getByRole("alert").textContent();
      assert.ok(!error.includes("PRIVATE"));
      assert.equal(
        await page.locator("#location").textContent(),
        "/change-password",
      );
      await assertCleared(page);
    });
  }
});

for (const reply of [success, http(409, "AUTH_STATE_CHANGED")]) {
  test(`late response after unmount cannot navigate or update errors: ok=${reply.ok}`, async () => {
    await withChange({ deferred: true }, async (page) => {
      await fill(page);
      await submit(page);
      await page.evaluate(() => window.changeTest.hide());
      await page.locator("#hidden").waitFor();
      await page.evaluate((value) => window.changeTest.finish(value), reply);
      await page.evaluate(() => new Promise((r) => setTimeout(r, 0)));
      assert.equal(
        await page.locator("#location").textContent(),
        "/change-password",
      );
      assert.equal(await page.getByRole("alert").count(), 0);
    });
  });
}

test("component import graph excludes mocks and shared storage initializers", () => {
  const paths = Object.keys(bundle.metafile.inputs).map((path) =>
    path.replaceAll("\\", "/"),
  );
  assert.ok(paths.some((path) => path.endsWith("/ChangePasswordScreen.tsx")));
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

for (const restricted of [false, true]) {
  for (const reply of [success, http(400, "CURRENT_PASSWORD_INVALID")]) {
    test(`real store/provider records mutation unmount limitation: restricted=${restricted}, ok=${reply.ok}`, async () => {
      await withChange(
        { realStore: true, restricted, deferred: true },
        async (page) => {
          await fill(page);
          await submit(page);
          await page.waitForFunction(
            () => window.changeTest.snapshot().status === "INITIALIZING",
          );
          assert.equal(
            await page
              .getByRole("heading", { name: "Change Password", exact: true })
              .count(),
            0,
          );
          await page.evaluate(
            (value) => window.changeTest.finish(value),
            reply,
          );
          const expected =
            reply.ok || !restricted ? "AUTHENTICATED" : "RESTRICTED";
          await page.waitForFunction(
            (status) => window.changeTest.snapshot().status === status,
            expected,
          );
          await page
            .getByRole("heading", { name: "Change Password", exact: true })
            .waitFor();
          assert.equal(
            await page.locator("#location").textContent(),
            "/change-password",
          );
          assert.equal(await page.getByRole("alert").count(), 0); // Original form's response is ignored after unmount.
          await assertCleared(page);
          assert.deepEqual(await page.evaluate(() => window.changeTest.calls), [
            values,
          ]);
        },
      );
    });
  }
}
