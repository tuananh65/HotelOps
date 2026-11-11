import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { mkdir } from "node:fs/promises";

await mkdir(".test-output", { recursive: true });
await build({
  entryPoints: ["src/auth/authApi.ts", "src/auth/http.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outdir: ".test-output/auth",
});
const { createAuthApi } = await import("../.test-output/auth/authApi.js");
const { createHttpClient } = await import("../.test-output/auth/http.js");

const login = { username: "test.user", password: " TestPassword!1 " };
const change = {
  currentPassword: " TestPassword!1 ",
  newPassword: "NextPassword!2",
  confirmNewPassword: "NextPassword!2",
};
const bootstrap = (csrfToken = "anonymous-token") => ({
  csrfToken,
  headerName: "X-CSRF-TOKEN",
});
const session = (csrfToken = "session-token", mustChangePassword = false) => ({
  user: { id: "9007199254740993", username: "test.user", name: "Test User" },
  expiresAt: "2026-11-06T08:30:00.1234567Z",
  absoluteExpiresAt: "2026-11-06T20:00:00Z",
  mustChangePassword,
  csrfToken,
});
const json = (body, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json; charset=utf-8" },
  });
const failure = (status, code) =>
  new Response(
    JSON.stringify({ status, code, title: code, traceId: "trace-1" }),
    {
      status,
      headers: { "Content-Type": "application/problem+json" },
    },
  );
function harness(...replies) {
  const calls = [];
  const api = createAuthApi(async (url, init) => {
    calls.push({ url, ...init });
    assert.ok(replies.length, "unexpected request/retry");
    const reply = replies.shift();
    return typeof reply === "function" ? reply(init) : reply;
  });
  return { api, calls };
}
function request(call, url, method, token, body) {
  assert.equal(call.url, url);
  assert.equal(call.method, method);
  assert.equal(call.credentials, "include");
  assert.equal(call.cache, "no-store");
  assert.equal(call.redirect, "error");
  assert.equal(call.headers.get("Authorization"), null);
  assert.equal(call.headers.get("X-CSRF-TOKEN"), token ?? null);
  assert.equal(call.headers.get("Accept"), "application/json");
  assert.equal(
    call.headers.get("Content-Type"),
    body ? "application/json" : null,
  );
  assert.equal(call.body, body ? JSON.stringify(body) : undefined);
}
const deferred = () => {
  let resolve;
  const promise = new Promise((r) => (resolve = r));
  return { promise, resolve };
};

test("anonymous CSRF bootstrap has exact GET contract", async () => {
  const { api, calls } = harness(json(bootstrap()));
  assert.deepEqual(await api.getCsrf(), {
    ok: true,
    status: 200,
    data: bootstrap(),
  });
  request(calls[0], "/api/auth/csrf", "GET");
});

for (const restricted of [false, true]) {
  test(`login 200 ${restricted ? "restricted" : "normal"} replaces anonymous token`, async () => {
    const data = session("login-token", restricted);
    const { api, calls } = harness(
      json(bootstrap()),
      json(data),
      new Response(null, { status: 204 }),
    );
    assert.deepEqual(await api.login(login), { ok: true, status: 200, data });
    assert.deepEqual(api.getSessionSnapshot(), data);
    request(calls[0], "/api/auth/csrf", "GET");
    request(calls[1], "/api/auth/login", "POST", "anonymous-token", login);
    await api.logout();
    request(calls[2], "/api/auth/logout", "POST", "login-token");
  });
}

for (const [status, code] of [
  [400, "VALIDATION_FAILED"],
  [401, "INVALID_CREDENTIALS"],
  [403, "TEMPORARY_PASSWORD_EXPIRED"],
  [409, "AUTHENTICATED_SESSION_EXISTS"],
  [423, "ACCOUNT_LOCKED"],
  [503, "AUTH_SERVICE_UNAVAILABLE"],
]) {
  test(`login ${status} ${code} is typed and never retried`, async () => {
    const { api, calls } = harness(json(bootstrap()), failure(status, code));
    const result = await api.login(login);
    assert.equal(result.ok, false);
    assert.equal(result.error.kind, "http");
    assert.equal(result.error.status, status);
    assert.equal(result.error.problem.code, code);
    assert.equal(api.getSessionSnapshot(), null);
    assert.equal(calls.length, 2);
    request(calls[1], "/api/auth/login", "POST", "anonymous-token", login);
  });
}

test("session 200 replaces token; session DTO is detached and strips extensions", async () => {
  const data = session("read-token");
  const { api, calls } = harness(
    json({ ...data, roles: ["SYS_ADMIN"] }),
    new Response(null, { status: 204 }),
  );
  const result = await api.getSession();
  assert.deepEqual(result.data, data);
  result.data.csrfToken = "caller-modification";
  const snapshot = api.getSessionSnapshot();
  snapshot.user.name = "changed";
  assert.equal(api.getSessionSnapshot().user.name, "Test User");
  request(calls[0], "/api/auth/session", "GET");
  await api.logout();
  request(calls[1], "/api/auth/logout", "POST", "read-token");
});

test("session 401 clears last-confirmed session and CSRF", async () => {
  const { api, calls } = harness(
    json(session()),
    failure(401, "SESSION_INVALID"),
    json(bootstrap("fresh")),
    new Response(null, { status: 204 }),
  );
  await api.getSession();
  const result = await api.getSession();
  assert.equal(result.error.status, 401);
  assert.equal(api.getSessionSnapshot(), null);
  await api.logout();
  request(calls[2], "/api/auth/csrf", "GET");
  request(calls[3], "/api/auth/logout", "POST", "fresh");
});

test("session 503 is not authoritative invalidation", async () => {
  const { api } = harness(
    json(session()),
    failure(503, "AUTH_SERVICE_UNAVAILABLE"),
  );
  await api.getSession();
  assert.equal((await api.getSession()).error.status, 503);
  assert.deepEqual(api.getSessionSnapshot(), session());
});

test("logout 204 clears state and next unsafe request bootstraps anew", async () => {
  const { api, calls } = harness(
    json(session()),
    new Response(null, { status: 204 }),
    json(bootstrap("after-logout")),
    json(session("new-login")),
  );
  await api.getSession();
  assert.deepEqual(await api.logout(), {
    ok: true,
    status: 204,
    data: undefined,
  });
  assert.equal(api.getSessionSnapshot(), null);
  await api.login(login);
  request(calls[1], "/api/auth/logout", "POST", "session-token");
  request(calls[2], "/api/auth/csrf", "GET");
  request(calls[3], "/api/auth/login", "POST", "after-logout", login);
});

test("logout CSRF failure is a failed mutation without replay", async () => {
  const { api, calls } = harness(
    json(session()),
    failure(400, "CSRF_VALIDATION_FAILED"),
  );
  await api.getSession();
  const result = await api.logout();
  assert.equal(result.ok, false);
  assert.equal(result.error.problem.code, "CSRF_VALIDATION_FAILED");
  assert.equal(calls.length, 2);
  request(calls[1], "/api/auth/logout", "POST", "session-token");
});

test("logout network failure never claims revocation or reuses old token", async () => {
  const { api, calls } = harness(
    json(session()),
    () => {
      throw new TypeError("sensitive provider response");
    },
    json(bootstrap("revalidated")),
    failure(401, "INVALID_CREDENTIALS"),
  );
  await api.getSession();
  assert.deepEqual(await api.logout(), {
    ok: false,
    error: { kind: "network" },
  });
  assert.equal(calls.length, 2);
  assert.equal(api.getSessionSnapshot(), null); // Unknown, not a logout-success signal.
  await api.login(login);
  request(calls[2], "/api/auth/csrf", "GET");
  request(calls[3], "/api/auth/login", "POST", "revalidated", login);
});

test("change-password replaces restricted session and token", async () => {
  const data = session("fresh-session");
  const { api, calls } = harness(
    json(session("restricted", true)),
    json(data),
    new Response(null, { status: 204 }),
  );
  await api.getSession();
  assert.deepEqual(await api.changePassword(change), {
    ok: true,
    status: 200,
    data,
  });
  request(calls[1], "/api/auth/change-password", "POST", "restricted", change);
  await api.logout();
  request(calls[2], "/api/auth/logout", "POST", "fresh-session");
});

for (const [status, code] of [
  [400, "VALIDATION_FAILED"],
  [400, "CURRENT_PASSWORD_INVALID"],
  [400, "PASSWORD_POLICY_VIOLATION"],
  [409, "AUTH_STATE_CHANGED"],
]) {
  test(`change-password ${code} does not replay`, async () => {
    const { api, calls } = harness(json(session()), failure(status, code));
    await api.getSession();
    const result = await api.changePassword(change);
    assert.equal(result.error.status, status);
    assert.equal(result.error.problem.code, code);
    assert.equal(calls.length, 2);
    request(
      calls[1],
      "/api/auth/change-password",
      "POST",
      "session-token",
      change,
    );
  });
}

test("ProblemDetails retains known fields/lock parameters and discards unknown extensions", async () => {
  const body = {
    status: 423,
    type: "about:blank",
    title: "ACCOUNT_LOCKED",
    code: "ACCOUNT_LOCKED",
    traceId: "trace",
    parameters: { lockedUntil: "2026-11-06T09:00:00Z", extra: "discard" },
    extra: "discard",
  };
  const { api } = harness(json(bootstrap()), json(body, 423));
  const { extra, ...expected } = body;
  expected.parameters = { lockedUntil: body.parameters.lockedUntil };
  assert.deepEqual((await api.login(login)).error.problem, expected);
});

for (const [name, response] of [
  [
    "HTML",
    () =>
      new Response("<html>private response</html>", {
        headers: { "Content-Type": "text/html" },
      }),
  ],
  [
    "malformed JSON",
    () =>
      new Response("{broken", {
        headers: { "Content-Type": "application/json" },
      }),
  ],
  ["missing fields", () => json({ user: {} })],
  ["wrong types", () => json({ ...session(), mustChangePassword: "false" })],
  [
    "unsafe id number",
    () => json({ ...session(), user: { ...session().user, id: 123 } }),
  ],
  ["invalid date", () => json({ ...session(), expiresAt: "not-a-date" })],
  ["wrong success status", () => json(session(), 201)],
  ["unexpected 204", () => new Response(null, { status: 204 })],
]) {
  test(`session rejects ${name} safely`, async () => {
    const { api } = harness(response());
    assert.deepEqual(await api.getSession(), {
      ok: false,
      error: { kind: "invalid-response" },
    });
    assert.equal(api.getSessionSnapshot(), null);
  });
}

test("non-JSON HTTP failure retains HTTP status without exposing body", async () => {
  const { api } = harness(new Response("private response", { status: 502 }));
  assert.deepEqual(await api.getSession(), {
    ok: false,
    error: { kind: "http", status: 502, problem: null },
  });
});

test("malformed ProblemDetails does not leak arbitrary body", async () => {
  const { api } = harness(
    json({ status: 401, code: { secret: "discard" } }, 401),
  );
  assert.equal((await api.getSession()).error.problem, null);
});

test("cancellation is distinct from network failure and forwards signal", async () => {
  const controller = new AbortController();
  const { api, calls } = harness(() => {
    controller.abort();
    throw new DOMException("Aborted", "AbortError");
  });
  assert.deepEqual(await api.getSession(controller.signal), {
    ok: false,
    error: { kind: "cancelled" },
  });
  assert.equal(calls[0].signal, controller.signal);
});

test("invalid CSRF bootstrap header cannot be used for mutation", async () => {
  const { api, calls } = harness(
    json({ ...bootstrap(), headerName: "Authorization" }),
  );
  assert.equal((await api.login(login)).error.kind, "invalid-response");
  assert.equal(calls.length, 1);
});

test("transport rejects absolute/path escape URLs and unsafe requests without token", async () => {
  const http = createHttpClient(() => {
    assert.fail("must not fetch");
  });
  for (const url of [
    "https://example.invalid/api/auth/session",
    "//example.invalid/api",
    "/api/../auth",
    "/api/%2fescape",
  ]) {
    assert.equal(
      (
        await http({
          method: "GET",
          url,
          expectedStatus: 200,
          decode: (x) => x,
        })
      ).error.kind,
      "invalid-response",
    );
  }
  assert.equal(
    (
      await http({
        method: "POST",
        url: "/api/auth/logout",
        expectedStatus: 204,
        decode: () => undefined,
      })
    ).error.kind,
    "invalid-response",
  );
});

test("no auth operation uses sensitive browser storage", async () => {
  const originals = [
    Object.getOwnPropertyDescriptor(globalThis, "localStorage"),
    Object.getOwnPropertyDescriptor(globalThis, "sessionStorage"),
  ];
  try {
    for (const key of ["localStorage", "sessionStorage"])
      Object.defineProperty(globalThis, key, {
        configurable: true,
        get() {
          assert.fail("storage accessed");
        },
      });
    const { api } = harness(
      json(bootstrap()),
      json(session()),
      json(session()),
      json(session("changed")),
      new Response(null, { status: 204 }),
    );
    assert.equal((await api.getCsrf()).ok, true);
    assert.equal((await api.login(login)).ok, true);
    assert.equal((await api.getSession()).ok, true);
    assert.equal((await api.changePassword(change)).ok, true);
    assert.equal((await api.logout()).ok, true);
  } finally {
    ["localStorage", "sessionStorage"].forEach((key, i) => {
      if (originals[i]) Object.defineProperty(globalThis, key, originals[i]);
      else delete globalThis[key];
    });
  }
});

test("stale CSRF read cannot overwrite token after login identity replacement", async () => {
  const old = deferred();
  const { api, calls } = harness(
    json(bootstrap()),
    () => old.promise,
    json(session("new-identity")),
    new Response(null, { status: 204 }),
  );
  await api.getCsrf();
  const pending = api.getCsrf();
  await api.login(login);
  old.resolve(json(bootstrap("old-anonymous")));
  assert.equal((await pending).error.kind, "stale");
  await api.logout();
  request(calls[3], "/api/auth/logout", "POST", "new-identity");
});

test("stale session response cannot resurrect state after logout", async () => {
  const old = deferred();
  const { api } = harness(
    json(session()),
    () => old.promise,
    new Response(null, { status: 204 }),
  );
  await api.getSession();
  const pending = api.getSession();
  await api.logout();
  old.resolve(json(session("old-session")));
  assert.equal((await pending).error.kind, "stale");
  assert.equal(api.getSessionSnapshot(), null);
});

test("latest read wins even if older session 401 arrives last", async () => {
  const old = deferred();
  const { api } = harness(() => old.promise, json(session("latest")));
  const pending = api.getSession();
  await api.getSession();
  old.resolve(failure(401, "SESSION_INVALID"));
  assert.equal((await pending).error.kind, "stale");
  assert.equal(api.getSessionSnapshot().csrfToken, "latest");
});

test("identity mutation excludes concurrent reads/mutations without queued POST replay", async () => {
  const loginReply = deferred();
  const { api, calls } = harness(json(bootstrap()), () => loginReply.promise);
  await api.getCsrf();
  const pending = api.login(login);
  for (const operation of [
    () => api.logout(),
    () => api.login(login),
    () => api.changePassword(change),
    () => api.getSession(),
    () => api.getCsrf(),
  ]) {
    assert.deepEqual(await operation(), { ok: false, error: { kind: "busy" } });
  }
  loginReply.resolve(json(session()));
  assert.equal((await pending).ok, true);
  assert.equal(calls.length, 2);
});

test("stale session cannot replace change-password identity token", async () => {
  const old = deferred();
  const { api, calls } = harness(
    json(session("before", true)),
    () => old.promise,
    json(session("after")),
    new Response(null, { status: 204 }),
  );
  await api.getSession();
  const pending = api.getSession();
  await api.changePassword(change);
  old.resolve(json(session("stale", true)));
  assert.equal((await pending).error.kind, "stale");
  await api.logout();
  request(calls[3], "/api/auth/logout", "POST", "after");
});

test("change-password rejects an unexpectedly restricted success response", async () => {
  const { api } = harness(json(bootstrap()), json(session("invalid", true)));
  assert.equal(
    (await api.changePassword(change)).error.kind,
    "invalid-response",
  );
  assert.equal(api.getSessionSnapshot(), null);
});

test("logout 503 and unexpected 200 never claim successful revocation", async () => {
  for (const reply of [failure(503, "AUTH_SERVICE_UNAVAILABLE"), json({})]) {
    const { api, calls } = harness(json(session()), reply);
    await api.getSession();
    assert.equal((await api.logout()).ok, false);
    assert.equal(calls.length, 2);
  }
});

test("bootstrap network failure prevents POST and releases mutation exclusion", async () => {
  const { api, calls } = harness(
    () => {
      throw new TypeError("offline");
    },
    json(bootstrap()),
    json(session()),
  );
  assert.equal((await api.login(login)).error.kind, "network");
  assert.equal(calls.length, 1);
  assert.equal((await api.login(login)).ok, true);
  assert.equal(calls.length, 3); // Explicit caller retry, never automatic.
});

test("unsafe cancellation is reported without retry or revocation claim", async () => {
  const controller = new AbortController();
  const { api, calls } = harness(json(session()), () => {
    controller.abort();
    throw new DOMException("Aborted", "AbortError");
  });
  await api.getSession();
  assert.deepEqual(await api.logout(controller.signal), {
    ok: false,
    error: { kind: "cancelled" },
  });
  assert.equal(calls.length, 2);
  assert.equal(calls[1].signal, controller.signal);
});

test("CSRF values unsuitable for HTTP headers are rejected without POST", async () => {
  for (const csrfToken of ["", "line\r\nbreak", "non-ascii-☃", "white space"]) {
    const { api, calls } = harness(json(bootstrap(csrfToken)));
    assert.equal((await api.login(login)).error.kind, "invalid-response");
    assert.equal(calls.length, 1);
  }
});

test("transport identifies decoder failures and AbortError without leaking exceptions", async () => {
  const http = createHttpClient(async () => json({}));
  const input = {
    method: "GET",
    url: "/api/auth/session",
    expectedStatus: 200,
    decode() {
      throw new Error("private data");
    },
  };
  assert.equal((await http(input)).error.kind, "invalid-response");
  const aborted = createHttpClient(async () => {
    throw new DOMException("Aborted", "AbortError");
  });
  assert.equal((await aborted(input)).error.kind, "cancelled");
});

for (const sessionFirst of [true, false]) {
  for (const sessionReturnsFirst of [true, false]) {
    test(`overlapping session/CSRF reads preserve identity: session starts ${sessionFirst ? "first" : "last"}, returns ${sessionReturnsFirst ? "first" : "last"}`, async () => {
      const sessionReply = deferred();
      const csrfReply = deferred();
      const replies = sessionFirst
        ? [() => sessionReply.promise, () => csrfReply.promise]
        : [() => csrfReply.promise, () => sessionReply.promise];
      const { api, calls } = harness(
        ...replies,
        new Response(null, { status: 204 }),
      );
      let pendingSession;
      let pendingCsrf;
      if (sessionFirst) {
        pendingSession = api.getSession();
        pendingCsrf = api.getCsrf();
      } else {
        pendingCsrf = api.getCsrf();
        pendingSession = api.getSession();
      }
      if (sessionReturnsFirst) {
        sessionReply.resolve(json(session("session-read")));
        assert.equal((await pendingSession).ok, true);
        csrfReply.resolve(json(bootstrap("csrf-read")));
        assert.equal((await pendingCsrf).ok, true);
      } else {
        csrfReply.resolve(json(bootstrap("csrf-read")));
        assert.equal((await pendingCsrf).ok, true);
        sessionReply.resolve(json(session("session-read")));
        assert.equal((await pendingSession).ok, true);
      }
      assert.deepEqual(api.getSessionSnapshot(), session("session-read"));
      request(calls[sessionFirst ? 0 : 1], "/api/auth/session", "GET");
      request(calls[sessionFirst ? 1 : 0], "/api/auth/csrf", "GET");
      await api.logout();
      // Newest-started successful token read wins, independent of response order.
      request(
        calls[2],
        "/api/auth/logout",
        "POST",
        sessionFirst ? "csrf-read" : "session-read",
      );
    });
  }
}

for (const newerReturnsFirst of [true, false]) {
  test(`two session reads use latest-started freshness: newer returns ${newerReturnsFirst ? "first" : "last"}`, async () => {
    const older = deferred();
    const newer = deferred();
    const { api, calls } = harness(
      () => older.promise,
      () => newer.promise,
      new Response(null, { status: 204 }),
    );
    const oldRead = api.getSession();
    const newRead = api.getSession();
    const olderData = {
      ...session("older"),
      user: { ...session().user, name: "Older" },
    };
    const newerData = {
      ...session("newer"),
      user: { ...session().user, name: "Newer" },
    };
    if (newerReturnsFirst) {
      newer.resolve(json(newerData));
      assert.equal((await newRead).ok, true);
      older.resolve(json(olderData));
      assert.equal((await oldRead).error.kind, "stale");
    } else {
      older.resolve(json(olderData));
      assert.equal((await oldRead).error.kind, "stale");
      assert.equal(api.getSessionSnapshot(), null);
      newer.resolve(json(newerData));
      assert.equal((await newRead).ok, true);
    }
    assert.deepEqual(api.getSessionSnapshot(), newerData);
    await api.logout();
    request(calls[2], "/api/auth/logout", "POST", "newer");
  });
}

test("a newer failed session read still supersedes an older success", async () => {
  const older = deferred();
  const { api } = harness(
    () => older.promise,
    failure(503, "AUTH_SERVICE_UNAVAILABLE"),
  );
  const oldRead = api.getSession();
  assert.equal((await api.getSession()).error.status, 503);
  older.resolve(json(session("older")));
  assert.equal((await oldRead).error.kind, "stale");
  assert.equal(api.getSessionSnapshot(), null);
});

test("two CSRF reads use latest-started freshness without changing session identity", async () => {
  const older = deferred();
  const { api, calls } = harness(
    json(session()),
    () => older.promise,
    json(bootstrap("newest")),
    new Response(null, { status: 204 }),
  );
  await api.getSession();
  const oldRead = api.getCsrf();
  assert.equal((await api.getCsrf()).ok, true);
  older.resolve(json(bootstrap("older")));
  assert.equal((await oldRead).error.kind, "stale");
  assert.deepEqual(api.getSessionSnapshot(), session());
  await api.logout();
  request(calls[3], "/api/auth/logout", "POST", "newest");
});

test("late CSRF started before a session 401 cannot reinstall the old token", async () => {
  const older = deferred();
  const { api, calls } = harness(
    json(session()),
    () => older.promise,
    failure(401, "SESSION_INVALID"),
    json(bootstrap("fresh")),
    new Response(null, { status: 204 }),
  );
  await api.getSession();
  const oldRead = api.getCsrf();
  assert.equal((await api.getSession()).error.status, 401);
  older.resolve(json(bootstrap("older")));
  assert.equal((await oldRead).ok, true); // Valid read, but cannot install its old token.
  assert.equal(api.getSessionSnapshot(), null);
  await api.logout();
  request(calls[3], "/api/auth/csrf", "GET");
  request(calls[4], "/api/auth/logout", "POST", "fresh");
});

for (const operation of ["logout", "change-password"]) {
  test(`late CSRF read cannot replace state after ${operation}`, async () => {
    const older = deferred();
    const isLogout = operation === "logout";
    const { api, calls } = harness(
      json(session("before")),
      () => older.promise,
      isLogout ? new Response(null, { status: 204 }) : json(session("after")),
      ...(isLogout ? [json(bootstrap("fresh"))] : []),
      new Response(null, { status: 204 }),
    );
    await api.getSession();
    const oldRead = api.getCsrf();
    assert.equal(
      (await (isLogout ? api.logout() : api.changePassword(change))).ok,
      true,
    );
    older.resolve(json(bootstrap("older")));
    assert.equal((await oldRead).error.kind, "stale");
    assert.deepEqual(
      api.getSessionSnapshot(),
      isLogout ? null : session("after"),
    );
    await api.logout();
    request(
      calls.at(-1),
      "/api/auth/logout",
      "POST",
      isLogout ? "fresh" : "after",
    );
  });
}
