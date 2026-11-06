import { test, expect } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";

const root = path.resolve("..");
const dir = path.join(root, "scripts/verification/FS012");
const helper = path.join(dir, "bin/Debug/net10.0/FS012.Verification.dll");
if (process.env.FS012_LIVE_ENABLED !== "APPROVED_FS012_LOCAL_TRIAL")
  throw Error("ISOLATED_OPTIN_REQUIRED");
const call = (args: string[]) => {
  const result = spawnSync("dotnet", [helper, ...args], {
    encoding: "utf8",
    timeout: 60000,
    windowsHide: true,
  });
  if (result.status !== 0) throw Error("PRIVATE_HELPER_FAILED: " + args[0]);
  return JSON.parse(result.stdout.replace(/^\uFEFF/, ""));
};
const fixture = JSON.parse(
  fs
    .readFileSync(path.join(dir, ".private/fixture.json"), "utf8")
    .replace(/^\uFEFF/, ""),
);
if (
  fixture.owner !== "DEV1_FS012" ||
  fixture.database !== "HotelOps_Canonical_20261106_f120001_Fresh"
)
  throw Error("TARGET_MISMATCH");
const accounts = call(["credentials-ipc", "--private-ipc"]);

test("FS012 durable replay survives actual owned API and gateway process restart", async ({
  page,
}) => {
  const request = page.request;
  const csrf = async () =>
    (await (await request.get("/api/auth/csrf")).json()).csrfToken;
  expect(
    (
      await request.post("/api/auth/login", {
        headers: { "X-CSRF-TOKEN": await csrf() },
        data: {
          username: accounts.admin.username,
          password: accounts.admin.password,
        },
      })
    ).status(),
  ).toBe(200);
  const source = call([
    "lifecycle-inspect",
    "--local-sql-certificate-exception",
  ]);
  const room = source.rooms.find(
    (r: any) => !source.tasks.some((t: any) => t.roomId === r.roomId),
  );
  expect(room).toBeTruthy();
  const options = await (
    await request.get(
      `/api/branches/${fixture.branchId}/cleaning-task-options?roomId=${room.roomId}`,
    )
  ).json();
  const create = await request.post(
    `/api/branches/${fixture.branchId}/cleaning-tasks`,
    {
      headers: { "X-CSRF-TOKEN": await csrf() },
      data: {
        roomId: room.roomId,
        cleaningTypeId: options.cleaningTypes[0].id,
        priorityId: options.priorities[0].id,
        dueAt: new Date(Date.now() + 3600000).toISOString(),
      },
    },
  );
  expect(create.status()).toBe(201);
  const task = await create.json();
  const original = create.headers().etag;
  const key = crypto.randomUUID();
  const command = {
    assigneeId: accounts.owner.userId,
    dueAt: new Date(Date.now() + 3600000).toISOString(),
    reason: "FS012 actual application restart",
    adoptUnassignedLegacy: false,
  };
  const assign = async () =>
    request.post(
      `/api/branches/${fixture.branchId}/cleaning-tasks/${task.id}/assign`,
      {
        headers: {
          "X-CSRF-TOKEN": await csrf(),
          "If-Match": original,
          "Idempotency-Key": key,
        },
        data: command,
      },
    );
  const first = await assign();
  expect(first.status()).toBe(200);
  const result = await first.json();
  const before = call([
    "candidate-inspect",
    "--local-sql-certificate-exception",
  ]);
  const sourceBefore = call([
    "lifecycle-inspect",
    "--local-sql-certificate-exception",
  ]);
  for (const script of [
    "Stop-LiveEnvironment.ps1",
    "Start-LiveEnvironment.ps1",
  ]) {
    const restarted = spawnSync(
      "powershell",
      [
        "-NoProfile",
        "-File",
        path.join(dir, script),
        ...(script.startsWith("Start") ? ["-StartApprovedTrial"] : []),
      ],
      { cwd: root, encoding: "utf8", windowsHide: true, timeout: 60000 },
    );
    if (restarted.status !== 0) throw Error("OWNED_RESTART_FAILED " + script);
  }
  const replay = await assign();
  expect(replay.status()).toBe(200);
  expect(await replay.json()).toEqual(result);
  const after = call([
    "candidate-inspect",
    "--local-sql-certificate-exception",
  ]);
  expect(after).toEqual(before);
  expect(
    call(["lifecycle-inspect", "--local-sql-certificate-exception"]),
  ).toEqual(sourceBefore);
  const rows = after.publications.filter((p: any) => p.operationKey === key);
  expect(rows).toHaveLength(1);
  fs.writeFileSync(
    path.join(root, "docs/fs012/ACTUAL_RESTART_RESULTS.json"),
    JSON.stringify(
      {
        level: "LIVE_API_SQL",
        passed: true,
        ownedApiGatewayViteRestart: true,
        originalIfMatchPreserved: true,
        durableResultEqual: true,
        notificationRowsUnchanged: true,
        publicationCount: rows.length,
        databaseReset: false,
      },
      null,
      2,
    ) + "\n",
  );
});
