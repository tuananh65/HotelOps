import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: ["src/api/dev1/cleaningAssignmentCommand.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/fs008/assign.mjs",
  define: { "import.meta.env": "{}" },
});
const { submitAssignment } = await import("../.test-output/fs008/assign.mjs");
const draft = {
    assigneeId: 3,
    dueAt: "2027-01-01T00:00:00Z",
    reason: "reason",
    adoptUnassignedLegacy: false,
  },
  tag = '"0000000000000001"',
  key = "12c44400-7ca4-4da1-8990-8d2fc258f211";
test("UC029 wire preparation preserves stable GUID, original tag, cookie and fresh CSRF, never retries unavailable publisher", async () => {
  let calls = 0;
  globalThis.fetch = async (url, o) => {
    assert.equal(o.credentials, "include");
    if (url.endsWith("/auth/csrf"))
      return Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "fresh" });
    calls++;
    assert.equal(o.method, "POST");
    assert.equal(o.headers["If-Match"], tag);
    assert.equal(o.headers["Idempotency-Key"], key);
    assert.equal(o.headers["X-CSRF-TOKEN"], "fresh");
    return Response.json({ title: "UNAVAILABLE" }, { status: 503 });
  };
  await assert.rejects(
    submitAssignment("1", "2", draft, tag, key),
    (e) => e.status === 503,
  );
  assert.equal(calls, 1);
});
test("UC029 rejects invented revision/key and adoption before network", async () => {
  globalThis.fetch = () => {
    throw Error("must not fetch");
  };
  await assert.rejects(submitAssignment("1", "2", draft, "W/" + tag, key));
  await assert.rejects(submitAssignment("1", "2", draft, tag, "bad"));
  await assert.rejects(
    submitAssignment(
      "1",
      "2",
      { ...draft, adoptUnassignedLegacy: true },
      tag,
      key,
    ),
  );
});
