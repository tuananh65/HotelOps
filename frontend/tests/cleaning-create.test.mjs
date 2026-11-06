import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: ["src/api/dev1/cleaningCreate.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/fs008/create.mjs",
  define: { "import.meta.env": "{}" },
});
const { cleaningOptions, createCleaning } =
  await import("../.test-output/fs008/create.mjs");
const input = {
  roomId: 3,
  cleaningTypeId: 4,
  priorityId: 5,
  dueAt: "2027-01-01T00:00:00Z",
  instruction: null,
};
test("create uses shared cookie transport and fresh CSRF, no client actor or retries", async () => {
  const calls = [];
  globalThis.fetch = async (url, o) => {
    calls.push({ url, o });
    assert.equal(o.credentials, "include");
    if (url.endsWith("/auth/csrf"))
      return Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "fresh" });
    assert.equal(o.method, "POST");
    assert.equal(o.headers["X-CSRF-TOKEN"], "fresh");
    assert.deepEqual(JSON.parse(o.body), input);
    return Response.json(
      {
        id: 6,
        branchId: 7,
        roomId: 3,
        taskNumber: "CL-6",
        state: "PENDING_ASSIGNMENT",
      },
      { status: 201 },
    );
  };
  assert.equal((await createCleaning("7", input)).id, 6);
  assert.equal(calls.length, 2);
});
test("create conflicts fail once without unsafe automatic retry", async () => {
  let posts = 0;
  globalThis.fetch = async (url) => {
    if (url.endsWith("/auth/csrf"))
      return Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "fresh" });
    posts++;
    return Response.json({ title: "DUPLICATE" }, { status: 409 });
  };
  await assert.rejects(createCleaning("7", input), (e) => e.status === 409);
  assert.equal(posts, 1);
});
test("create refuses invalid inputs and malformed CSRF before business POST", async () => {
  globalThis.fetch = () => {
    throw Error("must not fetch");
  };
  await assert.rejects(createCleaning("7", { ...input, roomId: 0 }));
  globalThis.fetch = async () => Response.json({ csrfToken: "bad" });
  await assert.rejects(createCleaning("7", input), /INVALID_CSRF_RESPONSE/);
});
test("options verifies exact room and branch and references", async () => {
  globalThis.fetch = async () =>
    Response.json({
      roomId: 3,
      roomNumber: "101",
      branchId: 8,
      cleaningTypes: [],
      priorities: [],
      cleaningItemCount: 2,
    });
  await assert.rejects(cleaningOptions("7", "3"), /INVALID_CREATE_OPTIONS/);
});
