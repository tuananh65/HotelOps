import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";
import ts from "typescript";
const cache = path.resolve("node_modules/.cache/fe05-fast017");
fs.mkdirSync(cache, { recursive: true });
function compile(p, name, replace = (s) => s) {
  const code = ts.transpileModule(fs.readFileSync(p, "utf8"), {
    compilerOptions: {
      target: ts.ScriptTarget.ES2022,
      module: ts.ModuleKind.ESNext,
    },
  }).outputText;
  fs.writeFileSync(path.join(cache, name), replace(code));
  return pathToFileURL(path.join(cache, name)).href;
}
compile("src/api/dev1/client.ts", "transport.mjs", (s) =>
  s.replaceAll("import.meta.env", "({})"),
);
compile(
  "src/features/inspectionReinspection/candidateClient.ts",
  "reinspection.mjs",
  (s) => s.replace("../../api/dev1/client", "./transport.mjs"),
);
const { InspectionAssignmentClient } = await import(
  compile(
    "src/features/inspectionAssignment/client.ts",
    "assignment.mjs",
    (s) =>
      s
        .replace("../../api/dev1/client", "./transport.mjs")
        .replace(
          "../inspectionReinspection/candidateClient",
          "./reinspection.mjs",
        ),
  )
);
const api = new InspectionAssignmentClient();
const operationId = "20261111-0000-4000-8000-000000000019";
const options = {
  taskId: 2,
  roomId: 3,
  taskRevision: '"0000000000000001"',
  cycleRevision: '"0000000000000002"',
  checklistVersionId: 4,
  inspectors: [{ userId: 5, fullName: "Inspector" }],
  responsibleSupervisors: [{ userId: 6, fullName: "Supervisor" }],
  productionMutationAvailable: false,
};
const response = (body, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
const original = globalThis.fetch;
async function intercept(f, run) {
  globalThis.fetch = f;
  try {
    await run();
  } finally {
    globalThis.fetch = original;
  }
}
test("read queue/options uses branch-scoped typed cookie transport", async () => {
  await intercept(
    async (url, init) => {
      assert.equal(init.credentials, "include");
      assert.equal(init.cache, "no-store");
      return response(
        url.endsWith("/2")
          ? options
          : {
              pending: [{ taskId: 2, roomId: 3 }],
              assigned: [
                {
                  inspectionId: 7,
                  roomId: 3,
                  checklistVersionId: 4,
                  inspectionRevision: options.taskRevision,
                },
              ],
            },
      );
    },
    async () => {
      assert.equal((await api.queue(1)).assigned[0].inspectionId, 7);
      assert.equal((await api.options(1, 2)).checklistVersionId, 4);
    },
  );
});
for (const status of [401, 403, 412, 409, 503])
  test(
    "confirm " + status + " preserves identity and never retries",
    async () => {
      let count = 0;
      await intercept(
        async (url, init) => {
          if (url.endsWith("/csrf"))
            return response({
              headerName: "X-CSRF-TOKEN",
              csrfToken: "synthetic",
            });
          count++;
          const body = JSON.parse(init.body);
          assert.equal(init.headers["If-Match"], options.taskRevision);
          assert.equal(body.responsibleSupervisorId, 6);
          assert.equal(body.cycleRevision, options.cycleRevision);
          assert.equal(body.checklistVersionId, 4);
          assert.equal(body.operationId, operationId);
          return response({ title: "DENIED" }, status);
        },
        async () => {
          await assert.rejects(
            api.confirm(
              1,
              options,
              5,
              6,
              "2030-01-01T00:00:00Z",
              "assign",
              true,
              undefined,
              operationId,
            ),
            (e) => e.status === status,
          );
          assert.equal(count, 1);
        },
      );
    },
  );

test("explicit retry carries the same operation UUID and body; no automatic retry", async () => {
  const bodies = [];
  await intercept(
    async (url, init) => {
      if (url.endsWith("/csrf"))
        return response({ headerName: "X-CSRF-TOKEN", csrfToken: "synthetic" });
      bodies.push(init.body);
      throw Error("lost response");
    },
    async () => {
      for (let attempt = 0; attempt < 2; attempt++)
        await assert.rejects(
          api.confirm(
            1,
            options,
            5,
            6,
            "2030-01-01T00:00:00Z",
            "assign",
            true,
            undefined,
            operationId,
          ),
        );
      assert.equal(bodies.length, 2);
      assert.equal(bodies[0], bodies[1]);
      assert.equal(JSON.parse(bodies[0]).operationId, operationId);
      assert.equal("inspectionId" in JSON.parse(bodies[0]), false);
    },
  );
});

for (const key of ["", "not-a-uuid", "00000000-0000-0000-0000-000000000000"])
  test(
    "invalid candidate operation identity rejected before transport: " + key,
    async () => {
      await intercept(
        async () => {
          throw Error("unexpected fetch");
        },
        async () => {
          await assert.rejects(
            api.confirm(
              1,
              options,
              5,
              6,
              "2030-01-01T00:00:00Z",
              "assign",
              true,
              undefined,
              key,
            ),
            (e) => e.status === 400,
          );
        },
      );
    },
  );
test("unknown outcome does not retry", async () => {
  let count = 0;
  await intercept(
    async (url) => {
      if (url.endsWith("/csrf"))
        return response({ headerName: "X-CSRF-TOKEN", csrfToken: "synthetic" });
      count++;
      throw Error("lost");
    },
    async () => {
      await assert.rejects(
        api.confirm(1, options, 5, 6, "2030-01-01T00:00:00Z", "assign", true),
        (e) => e.status === 0,
      );
      assert.equal(count, 1);
    },
  );
});
test("unexpected successful mutation cannot produce fake UI success", async () => {
  await intercept(
    async (url) =>
      url.endsWith("/csrf")
        ? response({ headerName: "X-CSRF-TOKEN", csrfToken: "synthetic" })
        : response({ inspectionId: 99 }),
    async () =>
      assert.rejects(
        api.confirm(1, options, 5, 6, "2030-01-01T00:00:00Z", "assign", true),
        (e) => e.code === "UC041_UNEXPECTED_MUTATION_SUCCESS",
      ),
  );
});
for (const input of [
  [99, 6, true],
  [5, 99, true],
  [5, 6, false],
])
  test(
    "invalid explicit selection/confirmation rejected before transport " +
      input,
    async () => {
      await intercept(
        async () => {
          throw Error("unexpected fetch");
        },
        async () =>
          assert.rejects(
            api.confirm(
              1,
              options,
              input[0],
              input[1],
              "2030-01-01T00:00:00Z",
              "assign",
              input[2],
            ),
            (e) => e.status === 400,
          ),
      );
    },
  );
for (const value of [
  { ...options, taskId: 99 },
  { ...options, taskRevision: "W/" + options.taskRevision },
  { ...options, productionMutationAvailable: true },
])
  test(
    "reject malformed or unaccepted capability response " +
      JSON.stringify(value),
    async () => {
      await intercept(
        async () => response(value),
        async () => assert.rejects(api.options(1, 2), (e) => e.status === 502),
      );
    },
  );
