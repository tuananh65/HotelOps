import { test, expect } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
const root = path.resolve(".."),
  dir = path.join(root, "scripts/verification/FS013");
if (process.env.FS013_LIVE_ENABLED !== "APPROVED_FS013_LOCAL_TRIAL")
  throw Error("OPT_IN_REQUIRED");
const fixture = JSON.parse(
  fs
    .readFileSync(path.join(dir, ".private/fixture.json"), "utf8")
    .replace(/^\uFEFF/, ""),
);
if (
  fixture.owner !== "DEV1_FS013" ||
  fixture.database !== "HotelOps_Canonical_20261106_f130001_Fresh"
)
  throw Error("SCOPE_MISMATCH");
function helper(mode: string, extra: string[] = []) {
  const r = spawnSync(
    "dotnet",
    [
      path.join(dir, "bin/Debug/net10.0/FS013.Verification.dll"),
      mode,
      ...extra,
    ],
    { env: process.env, encoding: "utf8", timeout: 60000, windowsHide: true },
  );
  if (r.status !== 0) throw Error("PRIVATE_HELPER_FAILED");
  return JSON.parse(r.stdout);
}
const accounts = helper("credentials-ipc", ["--private-ipc"]);
test("FS013 disabled candidate retains real 503/no mutation and disabled React Save", async ({
  page,
}) => {
  const csrf = await (await page.request.get("/api/auth/csrf")).json();
  expect(
    (
      await page.request.post("/api/auth/login", {
        headers: { "X-CSRF-TOKEN": csrf.csrfToken },
        data: {
          username: accounts.admin.username,
          password: accounts.admin.password,
        },
      })
    ).status(),
  ).toBe(200);
  const before = {
    source: helper("lifecycle-inspect", ["--local-sql-certificate-exception"]),
    notifications: helper("candidate-inspect", [
      "--local-sql-certificate-exception",
    ]),
  };
  const task = before.source.tasks.find(
    (x: any) =>
      x.status === "PENDING" &&
      !before.source.assignments.some(
        (a: any) => a.cleaningTaskId === x.cleaningTaskId,
      ),
  );
  expect(task).toBeTruthy();
  const options = await page.request.get(
    `/api/branches/${fixture.branchId}/cleaning-tasks/${task.cleaningTaskId}/assignment-options`,
  );
  expect(options.status()).toBe(200);
  expect((await options.json()).assignmentCapability).toBe("UNAVAILABLE");
  const fresh = await (await page.request.get("/api/auth/csrf")).json();
  const response = await page.request.post(
    `/api/branches/${fixture.branchId}/cleaning-tasks/${task.cleaningTaskId}/assign`,
    {
      headers: {
        "X-CSRF-TOKEN": fresh.csrfToken,
        "If-Match": options.headers().etag,
        "Idempotency-Key": crypto.randomUUID(),
      },
      data: {
        assigneeId: accounts.owner.userId,
        dueAt: new Date(Date.now() + 3600000).toISOString(),
        reason: "FS013 real default-off proof",
        adoptUnassignedLegacy: false,
      },
    },
  );
  expect(response.status()).toBe(503);
  await page.goto(
    `/cleaning/assignment?branchId=${fixture.branchId}&taskId=${task.cleaningTaskId}`,
  );
  await expect(page.locator("select[name=assigneeId]")).toBeVisible();
  await expect(page.locator("form button[type=button]")).toBeDisabled();
  const after = {
    source: helper("lifecycle-inspect", ["--local-sql-certificate-exception"]),
    notifications: helper("candidate-inspect", [
      "--local-sql-certificate-exception",
    ]),
  };
  expect(after).toEqual(before);
  fs.writeFileSync(
    path.join(root, "docs/fs013/DEFAULT_OFF_RESULTS.json"),
    JSON.stringify(
      {
        status: "PASS_REAL_BROWSER_API_SQL_DEFAULT_OFF",
        assignment: 503,
        capability: "UNAVAILABLE",
        reactSaveDisabled: true,
        mutation: false,
        sharedProductionActivation: false,
      },
      null,
      2,
    ) + "\n",
  );
});
