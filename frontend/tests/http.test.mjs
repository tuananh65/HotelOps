import { test } from "node:test";
import assert from "node:assert/strict";
import { bundle, json } from "./api-test-helpers.mjs";
const h = await bundle("src/api/http.ts", "http");
test("shared client serializes query values including zero/false and no empty value", async () => {
  let received;
  globalThis.fetch = async (url, options) => {
    received = { url, options };
    return json({ ok: true });
  };
  assert.deepEqual(
    await h.request("/api/rooms", {
      query: { page: 0, active: false, q: "a & b", empty: "" },
    }),
    { ok: true },
  );
  const url = new URL(received.url);
  assert.equal(url.searchParams.get("active"), "false");
  assert.equal(url.searchParams.get("page"), "0");
  assert.equal(url.searchParams.get("q"), "a & b");
  assert.equal(url.searchParams.has("empty"), false);
  assert.equal(received.options.credentials, "include");
});
test("JSON writes include agreed CSRF header; 204 and an empty body succeed", async () => {
  let options;
  h.configureSession(undefined, "csrf");
  globalThis.fetch = async (_url, input) => {
    options = input;
    return new Response(null, { status: 204 });
  };
  assert.equal(
    await h.request("/api/auth/password", {
      method: "POST",
      body: { currentPassword: "current" },
    }),
    undefined,
  );
  assert.equal(options.headers.get("Content-Type"), "application/json");
  assert.equal(options.headers.get("X-CSRF-TOKEN"), "csrf");
  globalThis.fetch = async () => new Response("", { status: 200 });
  assert.equal(await h.request("/empty"), undefined);
});
test("multipart upload leaves boundary generation to the browser", async () => {
  let options;
  globalThis.fetch = async (_url, input) => {
    options = input;
    return json([]);
  };
  const form = new FormData();
  form.append("files", new Blob(["test"]), "a.pdf");
  await h.request("/api/attachments", { method: "POST", body: form });
  assert.equal(options.headers.has("Content-Type"), false);
});
test("field errors retain ASP.NET ProblemDetails and response status", async () => {
  globalThis.fetch = async () =>
    json(
      {
        title: "Validation failed",
        errors: { "Attributes.RoomId": ["Room is required."] },
      },
      400,
    );
  await assert.rejects(
    h.request("/rooms", { method: "POST" }),
    (error) =>
      error.kind === "validation" &&
      error.fieldErrors["Attributes.RoomId"] === "Room is required.",
  );
});
test("401 invalidates a protected session but login failures do not invalidate a session", async () => {
  let count = 0;
  h.setUnauthorizedHandler(() => count++);
  globalThis.fetch = async () => new Response(null, { status: 401 });
  await assert.rejects(h.request("/session"));
  assert.equal(count, 1);
  await assert.rejects(h.request("/login", { public: true }));
  assert.equal(count, 1);
});
test("HTTP errors distinguish permissions, not found, concurrency and server errors", async () => {
  for (const [status, kind] of [
    [403, "forbidden"],
    [404, "notFound"],
    [409, "conflict"],
    [412, "conflict"],
    [422, "validation"],
    [500, "server"],
  ]) {
    globalThis.fetch = async () => new Response(null, { status });
    await assert.rejects(
      h.request("/data"),
      (error) => error.kind === kind && error.status === status,
    );
  }
});
test("network failure on a write never retries or falls back to mock and marks outcome unknown", async () => {
  let count = 0;
  globalThis.fetch = async () => {
    count++;
    throw new TypeError("Network failed");
  };
  await assert.rejects(
    h.request("/post", { method: "POST", body: {} }),
    (error) => error.kind === "network" && error.outcomeUnknown,
  );
  assert.equal(count, 1);
});
test("malformed 200 JSON cannot be reported as a successful write", async () => {
  globalThis.fetch = async () => new Response("<html>error</html>");
  await assert.rejects(
    h.request("/post", { method: "POST", body: {} }),
    (error) => error.outcomeUnknown,
  );
});
test("an aborted screen read remains cancellation and does not clear authentication", async () => {
  const c = new AbortController();
  c.abort();
  globalThis.fetch = async (_url, { signal }) => {
    if (signal.aborted) throw new DOMException("cancelled", "AbortError");
  };
  await assert.rejects(
    h.request("/data", { signal: c.signal }),
    (error) => error.name === "AbortError",
  );
});
test("bearer transport keeps the token in memory and omits cookies", async () => {
  const b = await bundle("src/api/http.ts", "bearer-http", "bearer");
  b.configureSession("access-token");
  let options;
  globalThis.fetch = async (_url, input) => {
    options = input;
    return json({});
  };
  await b.request("/data");
  assert.equal(options.credentials, "omit");
  assert.equal(options.headers.get("Authorization"), "Bearer access-token");
});
test("known System Message codes retain SRS text and server parameters", async () => {
  globalThis.fetch = async () =>
    json(
      {
        code: "MSG16",
        parameters: { item: "Soap", available: 2, quantity: 5 },
      },
      409,
    );
  await assert.rejects(
    h.request("/issue", { method: "POST" }),
    (error) =>
      error.message ===
      "Insufficient stock for Soap: available 2, requested 5.",
  );
});
test("HTTP 202 is pending, and a confirmed rollback is distinguished from an unknown outcome", async () => {
  globalThis.fetch = async () => json({ jobId: "job1" }, 202);
  await assert.rejects(
    h.request("/write", { method: "POST" }),
    (error) => error.outcomeUnknown && error.status === 202,
  );
  globalThis.fetch = async () => json({ code: "MSG06" }, 500);
  await assert.rejects(
    h.request("/write", { method: "POST" }),
    (error) =>
      !error.outcomeUnknown &&
      error.message === "Unable to save. No data has been changed.",
  );
});
test("responses from a previous login are discarded before they reach the screen cache", async () => {
  let release;
  globalThis.fetch = () =>
    new Promise((resolve) => {
      release = resolve;
    });
  const result = h.request("/rooms");
  h.invalidateSessionRequests();
  release(json({ privateData: true }));
  await assert.rejects(result, (error) => error.kind === "unauthenticated");
});
test("an empty JSON error still retains the HTTP status classification", async () => {
  globalThis.fetch = async () => json(null, 403);
  await assert.rejects(
    h.request("/rooms"),
    (error) => error.kind === "forbidden",
  );
});
