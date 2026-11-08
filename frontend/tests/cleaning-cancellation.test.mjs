import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: ["src/api/dev1/cleaningCancellation.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/fs018/cancellation.mjs",
  define: { "import.meta.env": "{}" },
});
const { cancellationCommand, cancelCleaning, readCancellation } =
  await import("../.test-output/fs018/cancellation.mjs");
const tag = '"0000000000000001"',
  next = '"0000000000000002"',
  key = "12c44400-7ca4-4da1-8990-8d2fc258f211";
const options = {
  id: 12,
  branchId: 7,
  sourceETag: tag,
  capability: "PROVISIONAL_V1",
  blocker: null,
};
test("UC037 requires server capability, confirmation and bounded reason", () => {
  assert.deepEqual(cancellationCommand(options, true, "  No longer needed  "), {
    confirmed: true,
    reason: "No longer needed",
  });
  for (const reason of ["", " ", "x".repeat(501), "x\0y"])
    assert.throws(() => cancellationCommand(options, true, reason));
  assert.throws(() => cancellationCommand(options, false, "reason"));
  assert.throws(() =>
    cancellationCommand(
      { ...options, capability: "UNAVAILABLE" },
      true,
      "reason",
    ),
  );
  assert.throws(() =>
    cancellationCommand({ ...options, sourceETag: "W/" + tag }, true, "reason"),
  );
});
test("UC037 binds branch/task, ETag, operation key and validates receipt", async () => {
  const original = globalThis.fetch;
  try {
    globalThis.fetch = async (url, init) => {
      if (url === "/api/auth/csrf")
        return new Response(
          JSON.stringify({
            headerName: "X-CSRF-TOKEN",
            csrfToken: "synthetic-test",
          }),
        );
      assert.equal(url, "/api/branches/7/cleaning-tasks/12/cancel");
      assert.equal(init.method, "POST");
      assert.equal(init.credentials, "include");
      assert.equal(init.headers["If-Match"], tag);
      assert.equal(init.headers["Idempotency-Key"], key);
      assert.equal(init.headers["X-CSRF-TOKEN"], "synthetic-test");
      assert.deepEqual(JSON.parse(init.body), {
        confirmed: true,
        reason: "No longer needed",
      });
      return new Response(
        JSON.stringify({
          ...options,
          sourceETag: next,
          operationId: key,
          state: "CANCELLED",
          readiness: "PENDING_CLEANING",
          readinessEventId: 9,
          contract: "PROVISIONAL_NOT_ACCEPTED",
        }),
        { headers: { ETag: next, "Content-Type": "application/json" } },
      );
    };
    assert.equal(
      (await cancelCleaning(options, true, "No longer needed", key)).state,
      "CANCELLED",
    );
    globalThis.fetch = async (url) =>
      url === "/api/auth/csrf"
        ? new Response(
            JSON.stringify({
              headerName: "X-CSRF-TOKEN",
              csrfToken: "synthetic-test",
            }),
          )
        : new Response(
            JSON.stringify({
              ...options,
              sourceETag: next,
              operationId: key,
              state: "CANCELLED",
              readiness: "READY",
              readinessEventId: 9,
              contract: "PROVISIONAL_NOT_ACCEPTED",
            }),
            { headers: { ETag: next } },
          );
    await assert.rejects(
      cancelCleaning(options, true, "reason", key),
      /INVALID_CANCELLATION_RECEIPT/,
    );
  } finally {
    globalThis.fetch = original;
  }
});
test("UC037 rejects metadata ETag mismatch and propagates server denial", async () => {
  const original = globalThis.fetch;
  try {
    globalThis.fetch = async () =>
      new Response(JSON.stringify(options), { headers: { ETag: next } });
    await assert.rejects(
      readCancellation("7", "12"),
      /INVALID_CANCELLATION_RESPONSE/,
    );
    for (const status of [403, 409, 412, 503]) {
      globalThis.fetch = async () =>
        new Response(JSON.stringify({ title: "DENIED" }), { status });
      await assert.rejects(
        cancelCleaning(options, true, "reason", key),
        (e) => e.status === status,
      );
    }
  } finally {
    globalThis.fetch = original;
  }
});
