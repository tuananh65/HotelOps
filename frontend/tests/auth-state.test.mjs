import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { mkdir } from "node:fs/promises";
import React, { StrictMode } from "react";
import { renderToString } from "react-dom/server";
import { existsSync } from "node:fs";
import { chromium } from "@playwright/test";

await mkdir(".test-output/auth-state", { recursive: true });
await build({
  entryPoints: [
    "src/auth/authStore.ts",
    "src/auth/AuthProvider.tsx",
    "src/auth/authApi.ts",
  ],
  bundle: true,
  platform: "node",
  format: "esm",
  jsx: "automatic",
  external: ["react", "react/jsx-runtime"],
  outdir: ".test-output/auth-state",
});
const { createAuthStore } =
  await import("../.test-output/auth-state/authStore.js");
const { AuthProvider, useAuth } =
  await import("../.test-output/auth-state/AuthProvider.js");
const { createAuthApi } = await import("../.test-output/auth-state/authApi.js");
const dto = (mustChangePassword = false, name = "User") => ({
  user: { id: "123", username: "user", name },
  expiresAt: "2026-11-06T08:30:00Z",
  absoluteExpiresAt: "2026-11-06T20:00:00Z",
  mustChangePassword,
  csrfToken: "PRIVATE-CSRF-TOKEN",
});
const ok = (data, status = 200) => ({ ok: true, status, data });
const fail = (status, code) => ({
  ok: false,
  error: {
    kind: "http",
    status,
    problem: {
      status,
      code,
      detail: "PRIVATE-PASSWORD",
      csrfToken: "PRIVATE-CSRF-TOKEN",
    },
  },
});
const network = () => ({ ok: false, error: { kind: "network" } });
const credentials = { username: "user", password: "PRIVATE-PASSWORD" };
const password = {
  currentPassword: "PRIVATE-PASSWORD",
  newPassword: "NEW-PRIVATE-PASSWORD",
  confirmNewPassword: "NEW-PRIVATE-PASSWORD",
};
const deferred = () => {
  let resolve;
  const promise = new Promise((r) => {
    resolve = r;
  });
  return { promise, resolve };
};
function harness(...replies) {
  const calls = [];
  const call = async (method, ...args) => {
    calls.push({ method, args });
    assert.ok(replies.length, "unexpected call/retry");
    const next = replies.shift();
    return typeof next === "function" ? next() : next;
  };
  const api = {
    getSession: (...args) => call("getSession", ...args),
    login: (...args) => call("login", ...args),
    logout: (...args) => call("logout", ...args),
    changePassword: (...args) => call("changePassword", ...args),
    getCsrf() {
      assert.fail("store must delegate CSRF lifecycle to accepted AuthApi");
    },
    getSessionSnapshot() {
      assert.fail("store must not use transport cache as server evidence");
    },
  };
  return { store: createAuthStore(api), calls };
}

for (const restricted of [false, true]) {
  test(`startup restores ${restricted ? "restricted" : "normal"} session without token`, async () => {
    const { store, calls } = harness(ok(dto(restricted)));
    assert.equal(store.getSnapshot().status, "INITIALIZING");
    const result = await store.initialize();
    assert.equal(result.ok, true);
    assert.equal(
      store.getSnapshot().status,
      restricted ? "RESTRICTED" : "AUTHENTICATED",
    );
    assert.equal(result.data.csrfToken, undefined);
    assert.equal(calls[0].method, "getSession");
  });
}

test("startup 401 is anonymous, not an assumed prior session expiry", async () => {
  const { store } = harness(fail(401, "AUTHENTICATION_REQUIRED"));
  assert.equal((await store.initialize()).ok, false);
  assert.equal(store.getSnapshot().status, "ANONYMOUS");
});

for (const reply of [fail(503, "AUTH_SERVICE_UNAVAILABLE"), network()]) {
  test(`startup ${reply.error.kind} unavailable is not anonymous`, async () => {
    const { store } = harness(reply);
    await store.initialize();
    assert.equal(store.getSnapshot().status, "ERROR_UNAVAILABLE");
    assert.equal(store.getSnapshot().session, null);
  });
}

test("initialize and revalidate share one pending GET; initialize is idempotent", async () => {
  const pending = deferred();
  const { store, calls } = harness(() => pending.promise);
  const first = store.initialize();
  assert.equal(first, store.initialize());
  assert.equal(first, store.revalidate());
  await Promise.resolve();
  assert.equal(calls.length, 1);
  pending.resolve(ok(dto()));
  await first;
  assert.equal(first, store.initialize());
  assert.equal(calls.length, 1);
});

test("login success forwards credentials but exposes only public session", async () => {
  const { store, calls } = harness(ok(dto()));
  const result = await store.login(credentials);
  assert.equal(store.getSnapshot().status, "AUTHENTICATED");
  assert.equal(result.data.user.id, "123");
  assert.deepEqual(calls[0], {
    method: "login",
    args: [credentials, undefined],
  });
  assert.ok(!JSON.stringify(result).includes("PRIVATE"));
});

test("revalidation synchronously removes identity before returning to protected callers", async () => {
  const pending = deferred();
  const { store, calls } = harness(ok(dto()), () => pending.promise);
  await store.initialize();
  const read = store.revalidate();
  const immediate = store.getSnapshot();
  // A same-turn route/action must not see the old authority while GET is queued.
  assert.equal(immediate.status, "INITIALIZING");
  assert.equal(immediate.session, null);
  assert.equal(immediate.pending, "revalidate");
  assert.equal(store.revalidate(), read);
  await Promise.resolve();
  assert.equal(calls.length, 2);
  pending.resolve(ok(dto(false, "Reconfirmed")));
  await read;
  assert.equal(store.getSnapshot().session.user.name, "Reconfirmed");
});

test("synchronous recovery subscriber shares the read without resurrecting identity after logout", async () => {
  const { store, calls } = harness(ok(dto()), ok(undefined, 204));
  await store.initialize();
  let nestedRead;
  let logout;
  const unsubscribe = store.subscribe(() => {
    if (store.getSnapshot().pending !== "revalidate") return;
    nestedRead = store.revalidate();
    logout = store.logout();
  });
  const read = store.revalidate();
  assert.equal(nestedRead, read);
  assert.equal(store.getSnapshot().session, null);
  await logout;
  assert.equal((await read).error.kind, "stale");
  assert.equal(store.getSnapshot().status, "ANONYMOUS");
  assert.deepEqual(
    calls.map((call) => call.method),
    ["getSession", "logout"],
  );
  unsubscribe();
});

test("initialization superseded by a subscriber logout does not dispatch an obsolete GET", async () => {
  const { store, calls } = harness(ok(undefined, 204));
  let logout;
  const unsubscribe = store.subscribe(() => {
    if (store.getSnapshot().pending === "initialize") logout = store.logout();
  });
  const read = store.initialize();
  assert.equal((await read).error.kind, "stale");
  await logout;
  assert.equal(store.getSnapshot().status, "ANONYMOUS");
  assert.deepEqual(
    calls.map((call) => call.method),
    ["logout"],
  );
  unsubscribe();
});

test("login failure is anonymous with sanitized error and no retry", async () => {
  const { store, calls } = harness(fail(401, "INVALID_CREDENTIALS"));
  const result = await store.login(credentials);
  assert.deepEqual(result, {
    ok: false,
    error: { kind: "http", status: 401, code: "INVALID_CREDENTIALS" },
  });
  assert.equal(store.getSnapshot().status, "ANONYMOUS");
  assert.equal(calls.length, 1);
  assert.ok(!JSON.stringify(store.getSnapshot()).includes("PRIVATE"));
});

test("login restricted success does not invent permissions or branch", async () => {
  const { store } = harness(
    ok({ ...dto(true), role: "SYS_ADMIN", branchId: 1 }),
  );
  await store.login(credentials);
  assert.equal(store.getSnapshot().status, "RESTRICTED");
  assert.equal(store.getSnapshot().session.role, undefined);
  assert.equal(store.getSnapshot().session.branchId, undefined);
});

test("logout 204 is confirmed anonymous and drops public identity", async () => {
  const { store, calls } = harness(ok(dto()), ok(undefined, 204));
  await store.initialize();
  assert.deepEqual(await store.logout(), { ok: true, data: undefined });
  assert.deepEqual(store.getSnapshot(), {
    status: "ANONYMOUS",
    session: null,
    pending: null,
    error: null,
  });
  assert.equal(calls[1].method, "logout");
});

for (const reply of [
  network(),
  fail(503, "AUTH_SERVICE_UNAVAILABLE"),
  { ok: false, error: { kind: "cancelled" } },
]) {
  test(`indeterminate logout ${reply.error.kind} requires GET revalidation without POST retry`, async () => {
    const { store, calls } = harness(
      ok(dto()),
      reply,
      ok(dto(false, "Still active")),
    );
    await store.initialize();
    assert.equal((await store.logout()).ok, false);
    assert.equal(store.getSnapshot().status, "ERROR_UNAVAILABLE");
    assert.equal(store.getSnapshot().session, null);
    assert.equal(calls.length, 2);
    await store.revalidate();
    assert.equal(store.getSnapshot().session.user.name, "Still active");
    assert.deepEqual(
      calls.map((c) => c.method),
      ["getSession", "logout", "getSession"],
    );
  });
}

test("indeterminate logout revalidation can instead confirm invalid session", async () => {
  const { store } = harness(ok(dto()), network(), fail(401, "SESSION_INVALID"));
  await store.initialize();
  await store.logout();
  await store.revalidate();
  assert.equal(store.getSnapshot().status, "EXPIRED_INVALID");
});

test("change-password promotes restricted session and strips replacement token", async () => {
  const { store, calls } = harness(
    ok(dto(true)),
    ok(dto(false, "Replacement")),
  );
  await store.initialize();
  const result = await store.changePassword(password);
  assert.equal(store.getSnapshot().status, "AUTHENTICATED");
  assert.equal(result.data.user.name, "Replacement");
  assert.equal(result.data.csrfToken, undefined);
  assert.deepEqual(calls[1].args, [password, undefined]);
});

test("password validation keeps confirmed restricted identity; conflict requires revalidation", async () => {
  const { store } = harness(
    ok(dto(true)),
    fail(400, "CURRENT_PASSWORD_INVALID"),
    fail(409, "AUTH_STATE_CHANGED"),
  );
  await store.initialize();
  await store.changePassword(password);
  assert.equal(store.getSnapshot().status, "RESTRICTED");
  await store.changePassword(password);
  assert.equal(store.getSnapshot().status, "ERROR_UNAVAILABLE");
  assert.equal(store.getSnapshot().session, null);
});

test("revalidation 401 invalidates an established session; 503 never asserts invalidation", async () => {
  const { store } = harness(
    ok(dto()),
    fail(503, "AUTH_SERVICE_UNAVAILABLE"),
    fail(401, "SESSION_INVALID"),
  );
  await store.initialize();
  await store.revalidate();
  assert.equal(store.getSnapshot().status, "ERROR_UNAVAILABLE");
  await store.revalidate();
  assert.equal(store.getSnapshot().status, "EXPIRED_INVALID");
});

for (const operation of ["logout", "login", "changePassword"]) {
  test(`late session GET cannot overwrite ${operation}`, async () => {
    const old = deferred();
    const isLogout = operation === "logout";
    const { store } = harness(
      ok(dto()),
      () => old.promise,
      isLogout ? ok(undefined, 204) : ok(dto(false, "New identity")),
    );
    await store.initialize();
    const read = store.revalidate();
    await Promise.resolve();
    const mutation =
      operation === "login"
        ? store.login(credentials)
        : operation === "logout"
          ? store.logout()
          : store.changePassword(password);
    await mutation;
    const accepted = store.getSnapshot();
    old.resolve(ok(dto(true, "Old identity")));
    assert.equal((await read).error.kind, "stale");
    assert.equal(store.getSnapshot(), accepted);
  });
}

test("late startup GET after login cannot restore startup identity", async () => {
  const old = deferred();
  const { store } = harness(() => old.promise, ok(dto(false, "Login")));
  const startup = store.initialize();
  await Promise.resolve();
  await store.login(credentials);
  old.resolve(ok(dto(true, "Startup")));
  assert.equal((await startup).error.kind, "stale");
  assert.equal(store.getSnapshot().session.user.name, "Login");
});

test("double mutation and read during mutation are busy, not queued", async () => {
  const pending = deferred();
  const { store, calls } = harness(() => pending.promise);
  const first = store.login(credentials);
  for (const action of [
    () => store.login(credentials),
    () => store.logout(),
    () => store.changePassword(password),
    () => store.revalidate(),
  ]) {
    assert.equal((await action()).error.kind, "busy");
  }
  assert.equal(calls.length, 1);
  pending.resolve(ok(dto()));
  await first;
  assert.equal(store.getSnapshot().pending, null);
});

test("stable deep immutable snapshots and subscribe/unsubscribe", async () => {
  const { store } = harness(ok(dto()), ok(undefined, 204));
  const original = store.getSnapshot();
  assert.equal(original, store.getSnapshot());
  let notifications = 0;
  const unsubscribe = store.subscribe(() => {
    notifications++;
  });
  await store.initialize();
  assert.equal(notifications, 2);
  const state = store.getSnapshot();
  assert.ok(Object.isFrozen(state));
  assert.ok(Object.isFrozen(state.session));
  assert.ok(Object.isFrozen(state.session.user));
  assert.throws(() => {
    state.session.user.name = "Changed";
  }, TypeError);
  unsubscribe();
  unsubscribe();
  await store.logout();
  assert.equal(notifications, 2);
});

test("StrictMode setup-cleanup-setup shares initialization and removes subscriptions", async () => {
  // Store-level effect replay simulation; not a mounted DOM StrictMode claim.
  const pending = deferred();
  const { store, calls } = harness(() => pending.promise, ok(undefined, 204));
  let removed = 0;
  let live = 0;
  const cleanup = store.subscribe(() => {
    removed++;
  });
  const first = store.initialize();
  cleanup();
  const unsubscribe = store.subscribe(() => {
    live++;
  });
  assert.equal(store.initialize(), first);
  await Promise.resolve();
  assert.equal(calls.length, 1);
  pending.resolve(ok(dto()));
  await first;
  assert.equal(removed, 0);
  assert.equal(live, 2);
  unsubscribe();
  await store.logout();
  assert.equal(live, 2);
});

test("storage is never accessed and API errors cannot leak arbitrary secrets", async () => {
  const keys = ["localStorage", "sessionStorage"];
  const originals = keys.map((key) =>
    Object.getOwnPropertyDescriptor(globalThis, key),
  );
  try {
    for (const key of keys)
      Object.defineProperty(globalThis, key, {
        configurable: true,
        get() {
          assert.fail("storage used");
        },
      });
    const { store } = harness(
      ok(dto()),
      fail(503, "PRIVATE-CSRF-TOKEN"),
      ok(dto()),
    );
    await store.initialize();
    const result = await store.logout();
    assert.ok(!JSON.stringify(result).includes("PRIVATE"));
    assert.ok(!JSON.stringify(store.getSnapshot()).includes("PRIVATE"));
    await store.revalidate();
    assert.ok(!JSON.stringify(store.getSnapshot()).includes("PRIVATE"));
  } finally {
    keys.forEach((key, i) => {
      if (originals[i]) Object.defineProperty(globalThis, key, originals[i]);
      else delete globalThis[key];
    });
  }
});

test("explicit revalidation recovers initialization failure without rerunning initialize", async () => {
  const { store, calls } = harness(network(), ok(dto()));
  const first = store.initialize();
  await first;
  assert.equal(store.initialize(), first);
  assert.equal(calls.length, 1);
  await store.revalidate();
  assert.equal(store.getSnapshot().status, "AUTHENTICATED");
});

test("unexpected rejected API promise becomes unavailable without exposing exception", async () => {
  const { store } = harness(() => {
    throw new Error("PRIVATE-PASSWORD");
  });
  assert.deepEqual(await store.login(credentials), {
    ok: false,
    error: { kind: "network" },
  });
  assert.equal(store.getSnapshot().status, "ERROR_UNAVAILABLE");
});

function Consumer() {
  const auth = useAuth();
  assert.equal(auth.state.session?.csrfToken, undefined);
  assert.equal(typeof auth.actions.revalidate, "function");
  return React.createElement("span", null, auth.state.status);
}

test("useAuth outside provider throws a clear error", () => {
  assert.throws(
    () => renderToString(React.createElement(Consumer)),
    /useAuth must be used within AuthProvider/,
  );
});

test("provider SSR gates unknown identity and fallback can consume context", () => {
  const { store } = harness();
  const html = renderToString(
    React.createElement(
      StrictMode,
      null,
      React.createElement(
        AuthProvider,
        { store, fallback: React.createElement(Consumer) },
        "BUSINESS-CONTENT",
      ),
    ),
  );
  assert.ok(html.includes("INITIALIZING"));
  assert.ok(!html.includes("BUSINESS-CONTENT"));
});

test("provider SSR renders known identity and gates unavailable state", async () => {
  const { store } = harness(ok(dto()), network());
  await store.initialize();
  const render = () =>
    renderToString(
      React.createElement(
        AuthProvider,
        { store, fallback: React.createElement(Consumer) },
        React.createElement(Consumer),
      ),
    );
  assert.ok(render().includes("AUTHENTICATED"));
  await store.revalidate();
  const html = renderToString(
    React.createElement(
      AuthProvider,
      { store, fallback: React.createElement(Consumer) },
      "BUSINESS-CONTENT",
    ),
  );
  assert.ok(html.includes("ERROR_UNAVAILABLE"));
  assert.ok(!html.includes("BUSINESS-CONTENT"));
  assert.ok(!html.includes("PRIVATE"));
});

for (const kind of ["invalid-response", "stale", "busy"]) {
  test(`transport ${kind} cannot grant identity or imply successful logout`, async () => {
    const { store } = harness(ok(dto()), { ok: false, error: { kind } });
    await store.initialize();
    assert.equal((await store.logout()).error.kind, kind);
    assert.equal(store.getSnapshot().status, "ERROR_UNAVAILABLE");
    assert.equal(store.getSnapshot().session, null);
  });
}

test("cancellation forwards signal and explicit GET recovers", async () => {
  const controller = new AbortController();
  const { store, calls } = harness(
    { ok: false, error: { kind: "cancelled" } },
    ok(dto()),
  );
  await store.revalidate(controller.signal);
  assert.equal(calls[0].args[0], controller.signal);
  assert.equal(store.getSnapshot().status, "ERROR_UNAVAILABLE");
  await store.revalidate();
  assert.equal(store.getSnapshot().status, "AUTHENTICATED");
});

test("an obsolete read finishing cannot clear a post-mutation read flight", async () => {
  const old = deferred();
  const current = deferred();
  const { store, calls } = harness(
    () => old.promise,
    ok(dto()),
    () => current.promise,
  );
  const startup = store.initialize();
  await Promise.resolve();
  await store.login(credentials);
  const latest = store.revalidate();
  await Promise.resolve();
  old.resolve(fail(401, "SESSION_INVALID"));
  assert.equal((await startup).error.kind, "stale");
  assert.equal(store.revalidate(), latest);
  current.resolve(ok(dto(false, "Latest")));
  await latest;
  assert.equal(calls.length, 3);
  assert.equal(store.getSnapshot().session.user.name, "Latest");
});

test("store works with accepted AuthApi and fetch contract without public token exposure", async () => {
  const old = deferred();
  const calls = [];
  const json = (data) =>
    new Response(JSON.stringify(data), {
      headers: { "Content-Type": "application/json" },
    });
  const replies = [
    () => old.promise,
    json({ csrfToken: "anonymous", headerName: "X-CSRF-TOKEN" }),
    json(dto()),
    new Response(null, { status: 204 }),
  ];
  const api = createAuthApi(async (url, init) => {
    calls.push({ url, ...init });
    assert.ok(replies.length);
    const reply = replies.shift();
    return typeof reply === "function" ? reply() : reply;
  });
  const store = createAuthStore(api);
  const startup = store.initialize();
  await Promise.resolve();
  assert.equal((await store.login(credentials)).ok, true);
  old.resolve(json(dto(true, "Old")));
  assert.equal((await startup).error.kind, "stale");
  assert.equal(store.getSnapshot().status, "AUTHENTICATED");
  assert.ok(!JSON.stringify(store.getSnapshot()).includes("PRIVATE"));
  assert.equal((await store.logout()).ok, true);
  assert.equal(calls[2].headers.get("X-CSRF-TOKEN"), "anonymous");
  assert.equal(calls[3].headers.get("X-CSRF-TOKEN"), "PRIVATE-CSRF-TOKEN");
  assert.deepEqual(
    calls.map((c) => [c.url, c.method]),
    [
      ["/api/auth/session", "GET"],
      ["/api/auth/csrf", "GET"],
      ["/api/auth/login", "POST"],
      ["/api/auth/logout", "POST"],
    ],
  );
});

test(
  "mounted StrictMode provider initializes once, keeps its store and releases subscriptions offline",
  { timeout: 30000 },
  async () => {
    // Existing Playwright dependency + local browser only; no server, HTTP trial or application router.
    const executablePath =
      process.env.HOTELOPS_TEST_BROWSER ||
      [
        "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
        "C:/Program Files/Google/Chrome/Application/chrome.exe",
      ].find((path) => existsSync(path));
    const browser = await chromium.launch({
      headless: true,
      ...(executablePath ? { executablePath } : {}),
    });
    try {
      const page = await browser.newPage();
      const errors = [];
      page.on("pageerror", (error) => errors.push(error.message));
      await page.route("**/*", (route) => route.abort());
      await page.setContent('<div id="root"></div>');
      const bundle = await build({
        stdin: {
          resolveDir: process.cwd(),
          loader: "tsx",
          contents: `
        import React, { StrictMode, useEffect, useRef } from "react";
        import { createRoot } from "react-dom/client";
        import { AuthProvider, useAuth } from "./src/auth/AuthProvider";
        import { createAuthStore } from "./src/auth/authStore";
        const metrics = { reads: 0, initializeCalls: 0, subscriptions: 0, mounts: 0, alternateReads: 0, stableActions: true };
        let resolve;
        const session = ${JSON.stringify(dto())};
        const api = { getSession: () => { metrics.reads++; return new Promise(r => { resolve = r; }); },
          logout: async () => ({ ok: true, status: 204, data: undefined }) };
        const store = createAuthStore(api);
        const originalInitialize = store.initialize;
        store.initialize = () => { metrics.initializeCalls++; return originalInitialize(); };
        const originalSubscribe = store.subscribe;
        store.subscribe = listener => { metrics.subscriptions++; const off = originalSubscribe(listener); let live = true;
          return () => { if (live) { live = false; metrics.subscriptions--; off(); } }; };
        const alternate = createAuthStore({ getSession: async () => { metrics.alternateReads++; return { ok: true, status: 200, data: session }; } });
        function Consumer() {
          const { state, actions } = useAuth();
          const first = useRef(actions);
          metrics.stableActions &&= first.current === actions;
          useEffect(() => { metrics.mounts++; }, []);
          return <span id="business">{state.status}:{state.session?.user.name}</span>;
        }
        function Recovery() { const { state } = useAuth(); return <span id="fallback">{state.status}</span>; }
        const root = createRoot(document.getElementById("root"));
        const render = value => root.render(<StrictMode><AuthProvider store={value} fallback={<Recovery />}><Consumer /></AuthProvider></StrictMode>);
        render(store);
        window.authTest = { metrics, finish: () => resolve({ ok: true, status: 200, data: session }),
          rerender: () => render(alternate), unmount: () => root.unmount(), logout: () => store.logout(),
          snapshot: () => store.getSnapshot() };
      `,
        },
        bundle: true,
        write: false,
        format: "iife",
        platform: "browser",
        jsx: "automatic",
        define: { "process.env.NODE_ENV": '"development"' },
      });
      await page.addScriptTag({ content: bundle.outputFiles[0].text });
      await page.waitForFunction(
        () =>
          window.authTest.metrics.initializeCalls >= 2 &&
          window.authTest.metrics.reads === 1,
      );
      assert.equal(await page.locator("#business").count(), 0);
      assert.equal(
        await page.locator("#fallback").textContent(),
        "INITIALIZING",
      );
      assert.equal(
        await page.evaluate(() => window.authTest.metrics.mounts),
        0,
      );
      await page.evaluate(() => window.authTest.finish());
      await page.waitForFunction(
        () =>
          document.querySelector("#business")?.textContent ===
          "AUTHENTICATED:User",
      );
      await page.evaluate(() => window.authTest.rerender());
      // Flush a browser rendering frame after rerendering with a different store prop.
      await page.evaluate(
        () =>
          new Promise((r) =>
            requestAnimationFrame(() => requestAnimationFrame(r)),
          ),
      );
      assert.deepEqual(
        await page.evaluate(() => ({
          reads: window.authTest.metrics.reads,
          subscriptions: window.authTest.metrics.subscriptions,
          alternateReads: window.authTest.metrics.alternateReads,
          stableActions: window.authTest.metrics.stableActions,
        })),
        { reads: 1, subscriptions: 1, alternateReads: 0, stableActions: true },
      );
      assert.ok(
        !(
          await page.evaluate(() => JSON.stringify(window.authTest.snapshot()))
        ).includes("PRIVATE"),
      );
      await page.evaluate(() => window.authTest.unmount());
      assert.equal(
        await page.evaluate(() => window.authTest.metrics.subscriptions),
        0,
      );
      await page.evaluate(() => window.authTest.logout());
      assert.equal(await page.locator("#business").count(), 0);
      assert.deepEqual(errors, []);
    } finally {
      await browser.close();
    }
  },
);
