import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: ["src/api/dev1/cleaningProgress.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/cleaning-progress.mjs",
  define: { "import.meta.env": "{}" },
});
const api = await import("../.test-output/cleaning-progress.mjs");
const data = {
  id: 42,
  branchId: 7,
  kind: "cleaning",
  state: "IN_PROGRESS",
  percent: 20,
  items: [
    {
      id: 9,
      label: "Clean floor",
      required: true,
      order: 0,
      done: false,
      outcome: null,
      note: null,
    },
  ],
  timeline: [],
  evidence: [],
};
const tag = '"0000000000000001"';
const current = { data, etag: tag };
const input = {
  percent: 100,
  items: [{ id: 9, done: true, note: null }],
  note: "Cleaned",
};
const success = (value = data, etag = tag) =>
  Response.json(value, { headers: { ETag: etag } });
test("UC032 reads exact active route with cookie credentials and strong HEX ETag", async () => {
  globalThis.fetch = async (url, options) => {
    assert.equal(url, "/api/branches/7/cleaning-tasks/42/progress");
    assert.equal(options.credentials, "include");
    assert.equal(options.cache, "no-store");
    return success();
  };
  assert.deepEqual(await api.readCleaningProgress("7", "42"), current);
});
test("UC032 PUT uses fresh identity-bound CSRF and exact If-Match; 100 does not complete", async () => {
  const calls = [];
  globalThis.fetch = async (url, options) => {
    calls.push(url);
    if (url === "/api/auth/csrf")
      return Response.json({
        csrfToken: "interception-only",
        headerName: "X-CSRF-TOKEN",
      });
    assert.equal(options.method, "PUT");
    assert.equal(options.credentials, "include");
    assert.equal(options.headers["If-Match"], tag);
    assert.equal(options.headers["X-CSRF-TOKEN"], "interception-only");
    const sent = JSON.parse(options.body);
    assert.deepEqual(sent, input);
    assert.equal("state" in sent, false);
    assert.equal("actorId" in sent, false);
    return success({ ...data, percent: 100 }, '"0000000000000002"');
  };
  const result = await api.saveCleaningProgress("7", "42", current, {
    ...input,
    actorId: 99,
  });
  assert.equal(result.data.state, "IN_PROGRESS");
  assert.equal(result.data.percent, 100);
  assert.deepEqual(calls, [
    "/api/auth/csrf",
    "/api/branches/7/cleaning-tasks/42/progress",
  ]);
});
test("UC032 rejects weak/Base64/lowercase/list tags and invalid item input before fetch", async () => {
  let count = 0;
  globalThis.fetch = async () => {
    count++;
    throw Error("unexpected");
  };
  for (const etag of [
    'W/"0000000000000001"',
    '"AAAAAAAAAAA="',
    '"abcdef0123456789"',
    "*",
    tag + "," + tag,
  ])
    await assert.rejects(
      api.saveCleaningProgress("7", "42", { ...current, etag }, input),
    );
  for (const invalid of [
    { ...input, percent: 101 },
    { ...input, percent: 1.5 },
    { ...input, items: [{ id: 99, done: true, note: null }] },
    { ...input, items: [input.items[0], input.items[0]] },
    { ...input, note: "line1\nline2" },
  ])
    await assert.rejects(api.saveCleaningProgress("7", "42", current, invalid));
  assert.equal(count, 0);
});
test("UC032 rejects cross-branch/task DTO and invalid ETag without rendering it", async () => {
  for (const value of [
    { ...data, branchId: 8 },
    { ...data, id: 43 },
    { ...data, state: "READY" },
    { ...data, items: null },
  ]) {
    globalThis.fetch = async () => success(value);
    await assert.rejects(
      api.readCleaningProgress("7", "42"),
      (e) => e.code === "INVALID_PROGRESS_RESPONSE",
    );
  }
  globalThis.fetch = async () => success(data, "W/" + tag);
  await assert.rejects(
    api.readCleaningProgress("7", "42"),
    (e) => e.code === "INVALID_PROGRESS_ETAG",
  );
});
test("UC032 propagates backend statuses, performs one PUT and never retries mutation", async () => {
  for (const status of [400, 401, 403, 404, 409, 412, 428, 503]) {
    let puts = 0;
    globalThis.fetch = async (url) =>
      url === "/api/auth/csrf"
        ? Response.json({
            csrfToken: "fixture-only",
            headerName: "X-CSRF-TOKEN",
          })
        : (puts++,
          Response.json(
            { title: status === 412 ? "STALE" : "TEST_ERROR" },
            { status },
          ));
    await assert.rejects(
      api.saveCleaningProgress("7", "42", current, input),
      (e) => e.status === status,
    );
    assert.equal(puts, 1);
  }
});
test("UC032 malformed CSRF prevents PUT; cancellation prevents all mutation", async () => {
  const urls = [];
  globalThis.fetch = async (url) => {
    urls.push(url);
    return Response.json({ csrfToken: "test", headerName: "Wrong-Header" });
  };
  await assert.rejects(
    api.saveCleaningProgress("7", "42", current, input),
    (e) => e.code === "INVALID_CSRF_RESPONSE",
  );
  assert.deepEqual(urls, ["/api/auth/csrf"]);
  const controller = new AbortController();
  controller.abort();
  globalThis.fetch = async (_, options) => {
    options.signal.throwIfAborted();
  };
  await assert.rejects(
    api.readCleaningProgress("7", "42", controller.signal),
    (e) => e.name === "AbortError",
  );
});
test("UC032 file policy rejects wrong size/type/signature before uploading", async () => {
  await assert.rejects(
    api.uploadFile(new File(["text"], "x.exe", { type: "text/plain" })),
  );
  await assert.rejects(
    api.uploadFile(new File(["text"], "x.png", { type: "image/png" })),
  );
  await assert.rejects(
    api.uploadFile(new File([], "x.png", { type: "image/png" })),
  );
  await assert.rejects(
    api.saveCleaningProgress("7", "42", current, {
      ...input,
      photos: Array(6).fill({
        name: "x.png",
        mimeType: "image/png",
        base64: "fixture",
      }),
    }),
  );
});

test("UC032 canceled CSRF bootstrap never starts a PUT", async () => {
  const controller = new AbortController();
  const calls = [];
  globalThis.fetch = async (url) => {
    calls.push(url);
    controller.abort();
    return Response.json({ csrfToken: "fixture", headerName: "X-CSRF-TOKEN" });
  };
  await assert.rejects(
    api.saveCleaningProgress("7", "42", current, input, controller.signal),
    (e) => e.name === "AbortError",
  );
  assert.deepEqual(calls, ["/api/auth/csrf"]);
});
