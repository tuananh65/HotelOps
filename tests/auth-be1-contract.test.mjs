import { test } from "node:test";
import assert from "node:assert/strict";
import { bundle, json } from "./api-test-helpers.mjs";

const backend = (mustChangePassword = false) => ({
  user: { id: "16", username: "sysadmin", name: "Test Admin" },
  expiresAt: "2099-01-01T00:00:00Z",
  absoluteExpiresAt: "2099-01-02T00:00:00Z",
  mustChangePassword,
  csrfToken: "authenticated-csrf",
});

test("BE1 login fetches CSRF, sends a secure cookie request, maps session without grants", async () => {
  const m = await bundle("src/api/modules.ts", "be1-login");
  const calls = [];
  globalThis.fetch = async (url, opts) => {
    calls.push({ url, opts });
    if (String(url).endsWith("/api/auth/csrf"))
      return json({ csrfToken: "anonymous-csrf", headerName: "X-CSRF-TOKEN" });
    if (String(url).endsWith("/api/auth/login")) return json(backend(true));
    throw new Error("unexpected request " + url);
  };
  const dto = await m.authService.login({ username: "sysadmin", password: "test-only" });
  assert.equal(calls.length, 2);
  assert.equal(calls[0].opts.method, "GET");
  assert.equal(calls[0].opts.credentials, "include");
  assert.equal(calls[1].opts.method, "POST");
  assert.equal(calls[1].opts.credentials, "include");
  assert.equal(calls[1].opts.headers.get("X-CSRF-TOKEN"), "anonymous-csrf");
  assert.deepEqual(JSON.parse(calls[1].opts.body), { username: "sysadmin", password: "test-only" });
  assert.equal(dto.principal.username, "sysadmin");
  assert.deepEqual(dto.principal.grants, []);
  assert.equal(dto.mustChangePassword, true);
});

test("BE1 restores session and submits all three change-password fields", async () => {
  const m = await bundle("src/api/modules.ts", "be1-session");
  const calls = [];
  globalThis.fetch = async (url, opts) => { calls.push({ url, opts }); return json(backend()); };
  const restored = await m.authService.session();
  assert.equal(restored.principal.id, "16");
  assert.equal(calls[0].opts.method, "GET");
  const changed = await m.authService.changePassword({
    currentPassword: "old-password",
    newPassword: "New!pass123",
    confirmNewPassword: "New!pass123",
  });
  assert.equal(changed.principal.id, "16");
  assert.ok(String(calls[1].url).endsWith("/api/auth/change-password"));
  assert.deepEqual(JSON.parse(calls[1].opts.body), {
    currentPassword: "old-password",
    newPassword: "New!pass123",
    confirmNewPassword: "New!pass123",
  });
});

test("BE1 error code INVALID_CREDENTIALS is presented on failed login", async () => {
  const m = await bundle("src/api/modules.ts", "be1-invalid");
  globalThis.fetch = async url => String(url).endsWith("/api/auth/csrf")
    ? json({ csrfToken: "t", headerName: "X-CSRF-TOKEN" })
    : json({ code: "INVALID_CREDENTIALS", title: "INVALID_CREDENTIALS", status: 401 }, 401);
  await assert.rejects(m.authService.login({ username: "missing", password: "wrong" }),
    err => err.status === 401 && err.message === "Invalid username/email or password.");
});
