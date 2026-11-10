import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";
import ts from "typescript";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
const cache = path.resolve("node_modules/.cache/fe05-fast015");
fs.mkdirSync(cache, { recursive: true });
function compile(source, name, replace = (s) => s) {
  const text = fs.readFileSync(source, "utf8");
  const js = ts.transpileModule(text, {
    compilerOptions: {
      target: ts.ScriptTarget.ES2022,
      module: ts.ModuleKind.ESNext,
      jsx: ts.JsxEmit.ReactJSX,
    },
  }).outputText;
  fs.writeFileSync(path.join(cache, name), replace(js));
  return pathToFileURL(path.join(cache, name)).href;
}
compile("src/api/dev1/client.ts", "transport.mjs", (s) =>
  s.replaceAll("import.meta.env", "({})"),
);
const { ReinspectionCandidateClient, decodeDetails, candidateError } =
  await import(
    compile(
      "src/features/inspectionReinspection/candidateClient.ts",
      "candidate.mjs",
      (s) => s.replace("../../api/dev1/client", "./transport.mjs"),
    )
  );
const { ApproveInspectionReviewCandidate } = await import(
  compile(
    "src/features/inspectionApprove/ApproveInspectionReviewCandidate.tsx",
    "approve.mjs",
  )
);
const initial = {
  inspectionId: 7,
  reworkId: 8,
  originalInspectionId: 2,
  originalChecklistVersionId: 3,
  assignedChecklistVersionId: 4,
  inspectionRevision: '"0000000000000001"',
  reworkRevision: '"0000000000000002"',
  bindingRevision: '"0000000000000003"',
  criteria: [
    {
      itemId: 11,
      criterion: "Floor clean",
      passCondition: "Dry",
      isRequired: true,
      isActive: true,
    },
  ],
  defects: [
    {
      defectId: 9,
      description: "Wet floor",
      originalCriterion: "Floor clean",
      remediationNote: "Repaired",
      fixed: null,
      recordedBy: null,
      recordedAt: null,
    },
  ],
  candidateStorageAvailable: true,
  lifecycleConclusionAvailable: false,
};
const client = new ReinspectionCandidateClient();
const originalFetch = globalThis.fetch;
function response(value, status = 200, etag) {
  return new Response(JSON.stringify(value), {
    status,
    headers: {
      "Content-Type": "application/json",
      ...(etag ? { ETag: etag } : {}),
    },
  });
}
async function intercept(handler, run) {
  globalThis.fetch = handler;
  try {
    await run();
  } finally {
    globalThis.fetch = originalFetch;
  }
}
test("typed client list -> detail -> save -> reload uses server revisions, CSRF and cookie transport", async () => {
  let stored = structuredClone(initial);
  const calls = [];
  await intercept(
    async (url, init) => {
      calls.push({ url, init });
      assert.equal(init.credentials, "include");
      assert.equal(init.cache, "no-store");
      if (url.endsWith("/auth/csrf"))
        return response({
          headerName: "X-CSRF-TOKEN",
          csrfToken: "synthetic-csrf",
        });
      if (init.method === "PUT") {
        assert.equal(init.headers["If-Match"], initial.inspectionRevision);
        assert.equal(init.headers["X-CSRF-TOKEN"], "synthetic-csrf");
        const body = JSON.parse(init.body);
        assert.equal(body.bindingRevision, initial.bindingRevision);
        assert.equal(body.reworkRevision, initial.reworkRevision);
        stored = {
          ...stored,
          inspectionRevision: '"0000000000000004"',
          bindingRevision: '"0000000000000005"',
          defects: [
            {
              ...stored.defects[0],
              fixed: false,
              recordedBy: 12,
              recordedAt: "2026-11-10T00:00:00Z",
            },
          ],
        };
        return response(stored, 200, stored.inspectionRevision);
      }
      if (url.endsWith("candidate"))
        return response([
          {
            inspectionId: 7,
            reworkId: 8,
            originalInspectionId: 2,
            roomId: 6,
            assignedChecklistVersionId: 4,
          },
        ]);
      return response(stored);
    },
    async () => {
      assert.equal((await client.list(1))[0].inspectionId, 7);
      const source = await client.read(1, 7, 8);
      const saved = await client.save(1, source, [
        { defectId: 9, fixed: false },
      ]);
      assert.equal(saved.inspectionRevision, '"0000000000000004"');
      const loaded = await client.read(1, 7, 8);
      assert.equal(loaded.defects[0].fixed, false);
      assert.equal(loaded.originalChecklistVersionId, 3);
      assert.equal(loaded.assignedChecklistVersionId, 4);
      assert.equal(calls.filter((c) => c.init.method === "PUT").length, 1);
    },
  );
});
for (const status of [401, 403, 412, 503])
  test(`server ${status} is propagated without retry`, async () => {
    let mutations = 0;
    await intercept(
      async (url, init) => {
        if (url.endsWith("/csrf"))
          return response({ headerName: "X-CSRF-TOKEN", csrfToken: "test" });
        if (init.method === "PUT") mutations++;
        return response({ title: "DENIED" }, status);
      },
      async () => {
        await assert.rejects(
          client.save(1, initial, [{ defectId: 9, fixed: true }]),
          (e) => e.status === status,
        );
        assert.equal(mutations, 1);
      },
    );
  });
test("empty collection is valid", async () => {
  await intercept(
    async () => response([]),
    async () => assert.deepEqual(await client.list(1), []),
  );
});
for (const decisions of [
  [],
  [{ defectId: 9, fixed: null }],
  [{ defectId: 10, fixed: true }],
  [
    { defectId: 9, fixed: true },
    { defectId: 9, fixed: false },
  ],
])
  test(
    "incomplete, foreign or duplicate decisions stop before HTTP " +
      JSON.stringify(decisions),
    async () => {
      await intercept(
        async () => {
          throw new Error("unexpected fetch");
        },
        async () =>
          assert.rejects(
            client.save(1, initial, decisions),
            (e) => e.status === 400,
          ),
      );
    },
  );
test("read rejects foreign identity", async () => {
  await intercept(
    async () => response({ ...initial, inspectionId: 99 }),
    async () =>
      assert.rejects(client.read(1, 7, 8), (e) => e.code === "SOURCE_MISMATCH"),
  );
});
test("invalid CSRF blocks mutation", async () => {
  let n = 0;
  await intercept(
    async () => {
      n++;
      return response({ headerName: "wrong", csrfToken: "test" });
    },
    async () => {
      await assert.rejects(
        client.save(1, initial, [{ defectId: 9, fixed: true }]),
        (e) => e.code === "INVALID_CSRF_RESPONSE",
      );
      assert.equal(n, 1);
    },
  );
});
test("response ETag must match decoded root revision", async () => {
  await intercept(
    async (url) =>
      url.endsWith("/csrf")
        ? response({ headerName: "X-CSRF-TOKEN", csrfToken: "test" })
        : response(initial, 200, '"0000000000000099"'),
    async () =>
      assert.rejects(
        client.save(1, initial, [{ defectId: 9, fixed: true }]),
        (e) => e.code === "REVISION_RESPONSE_MISMATCH",
      ),
  );
});
test("bind rejects foreign returned source", async () => {
  await intercept(
    async (url) =>
      url.endsWith("/csrf")
        ? response({ headerName: "X-CSRF-TOKEN", csrfToken: "test" })
        : response({ ...initial, reworkId: 99 }),
    async () =>
      assert.rejects(
        client.bind(
          1,
          7,
          8,
          initial.inspectionRevision,
          initial.reworkRevision,
        ),
        (e) => e.code === "SOURCE_MISMATCH",
      ),
  );
});
test("decoder rejects weak revision, duplicate defects and enabled lifecycle", () => {
  for (const value of [
    { ...initial, bindingRevision: "W/" + initial.bindingRevision },
    { ...initial, defects: [initial.defects[0], initial.defects[0]] },
    { ...initial, lifecycleConclusionAvailable: true },
  ])
    assert.throws(() => decodeDetails(value));
});
test("network unknown result is never automatically resubmitted", async () => {
  let n = 0;
  await intercept(
    async (url) => {
      if (url.endsWith("/csrf"))
        return response({ headerName: "X-CSRF-TOKEN", csrfToken: "test" });
      n++;
      throw new Error("connection lost");
    },
    async () => {
      await assert.rejects(
        client.save(1, initial, [{ defectId: 9, fixed: true }]),
        (e) => e.status === 0 && candidateError(e).includes("Reload"),
      );
      assert.equal(n, 1);
    },
  );
});
test("Approve review keeps mutation disabled even when mandatory PASS is true", () => {
  const html = renderToStaticMarkup(
    React.createElement(ApproveInspectionReviewCandidate, {
      source: {
        inspectionId: 7,
        inspectionRevision: initial.inspectionRevision,
        mandatoryCriteriaPassed: true,
        assignedChecklistVersionId: 4,
      },
    }),
  );
  assert.match(html, /disabled=""/);
  assert.match(html, /data-operation-id="[0-9a-f-]{36}"/);
  assert.match(html, /data-inspection-revision/);
  assert.doesNotMatch(html, /<form/);
});

const savedSource = {
  ...initial,
  defects: initial.defects.map((d) => ({
    ...d,
    fixed: false,
    recordedBy: 12,
    recordedAt: "2026-11-10T00:00:00Z",
  })),
};
const operation = "c351c7f0-9542-4d02-9047-ac21eb46a907";
const proposal = {
  operationId: operation,
  inspectionId: 7,
  reworkId: 8,
  actorUserId: 12,
  originalChecklistVersionId: 3,
  assignedChecklistVersionId: 4,
  proposedReworkState: "FAILED",
  inspectionRevision: '"0000000000000006"',
  bindingRevision: '"0000000000000007"',
  reworkRevision: initial.reworkRevision,
  proposalRevision: '"0000000000000008"',
  notFixedDefectIds: [9],
  createdAt: "2026-11-10T00:00:00Z",
  replayed: false,
  lifecycleApplied: false,
  nextRoundCreationAvailable: false,
};
test("typed Review/Confirm sends exact saved source, operation and selected identity with no lifecycle flag", async () => {
  let posts = 0;
  await intercept(
    async (url, init) => {
      if (url.endsWith("/csrf"))
        return response({ headerName: "X-CSRF-TOKEN", csrfToken: "test" });
      posts++;
      assert.equal(init.method, "POST");
      assert.equal(init.headers["If-Match"], savedSource.inspectionRevision);
      const body = JSON.parse(init.body);
      assert.equal(body.operationId, operation);
      assert.equal(body.confirmed, true);
      assert.deepEqual(body.selectedDefectIds, [9]);
      assert.equal(body.bindingRevision, savedSource.bindingRevision);
      assert.equal(body.reworkRevision, savedSource.reworkRevision);
      assert.equal(body.fixed, undefined);
      return response(proposal, 200, proposal.inspectionRevision);
    },
    async () => {
      const result = await client.prepareConclusion(
        1,
        savedSource,
        operation,
        true,
      );
      assert.equal(result.proposedReworkState, "FAILED");
      assert.equal(result.lifecycleApplied, false);
      assert.equal(posts, 1);
    },
  );
});
test("proposal reload verifies exact source and persisted result", async () => {
  const postSource = {
    ...savedSource,
    inspectionRevision: proposal.inspectionRevision,
    bindingRevision: proposal.bindingRevision,
  };
  await intercept(
    async () => response({ proposal }),
    async () =>
      assert.equal(
        (await client.readConclusion(1, postSource)).operationId,
        operation,
      ),
  );
});
test("old source cannot silently adopt another revision on proposal reload", async () => {
  await intercept(
    async () => response({ proposal }),
    async () =>
      assert.rejects(
        client.readConclusion(1, savedSource),
        (e) => e.status === 412,
      ),
  );
});
test("unreviewed proposal response is explicitly null", async () => {
  await intercept(
    async () => response({ proposal: null }),
    async () => assert.equal(await client.readConclusion(1, savedSource), null),
  );
});
for (const status of [401, 403, 409, 412, 503])
  test("conclusion " + status + " never retries operation", async () => {
    let n = 0;
    await intercept(
      async (url) => {
        if (url.endsWith("/csrf"))
          return response({ headerName: "X-CSRF-TOKEN", csrfToken: "test" });
        n++;
        return response({ title: "DENIED" }, status);
      },
      async () => {
        await assert.rejects(
          client.prepareConclusion(1, savedSource, operation, true),
          (e) => e.status === status,
        );
        assert.equal(n, 1);
      },
    );
  });
test("unknown conclusion outcome has one attempt and requires reload", async () => {
  let n = 0;
  await intercept(
    async (url) => {
      if (url.endsWith("/csrf"))
        return response({ headerName: "X-CSRF-TOKEN", csrfToken: "test" });
      n++;
      throw Error("lost response");
    },
    async () => {
      await assert.rejects(
        client.prepareConclusion(1, savedSource, operation, true),
        (e) => e.status === 0,
      );
      assert.equal(n, 1);
    },
  );
});
test("confirmation and saved complete decisions are required before transport", async () => {
  await intercept(
    async () => {
      throw Error("unexpected fetch");
    },
    async () => {
      await assert.rejects(
        client.prepareConclusion(1, savedSource, operation, false),
        (e) => e.status === 400,
      );
      await assert.rejects(
        client.prepareConclusion(1, initial, operation, true),
        (e) => e.status === 400,
      );
    },
  );
});
test("proposal response cannot claim real lifecycle, different defects/version or outcome", () => {
  for (const x of [
    { ...proposal, lifecycleApplied: true },
    { ...proposal, notFixedDefectIds: [] },
    { ...proposal, assignedChecklistVersionId: 99 },
    { ...proposal, proposedReworkState: "COMPLETED" },
  ])
    assert.throws(() => decodeProposal(x, savedSource));
});
