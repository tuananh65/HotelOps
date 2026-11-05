import { test, expect, type BrowserContext } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
const root = path.resolve(".."),
  directory = path.join(root, "scripts/verification/FS004");
const fixture = JSON.parse(
  fs
    .readFileSync(path.join(directory, ".private/fixture.json"), "utf8")
    .replace(/^\uFEFF/, ""),
);
if (
  process.env.FS006_LIVE_ENABLED !== "APPROVED_FS006_READ_ONLY" ||
  fixture.database !== "HotelOps_Canonical_20261105_180000_f040001_Fresh" ||
  fixture.owner !== "DEV1_FS004"
)
  throw Error("TRIAL_SCOPE_MISMATCH");
function privateCall(args: string[]) {
  const child = spawnSync(
    "dotnet",
    [path.join(directory, "bin/Debug/net10.0/FS004.Verification.dll"), ...args],
    {
      env: { ...process.env, FS004_LIVE_ENABLED: "APPROVED_FS004_LOCAL_TRIAL" },
      encoding: "utf8",
      windowsHide: true,
      timeout: 30000,
    },
  );
  if (child.status !== 0) throw Error("HELPER_FAILED; private output redacted");
  return JSON.parse(child.stdout.replace(/^\uFEFF/, ""));
}
const accounts = privateCall(["credentials-ipc", "--private-ipc"]),
  before = privateCall(["inspect", "--local-sql-certificate-exception"]);
const report: {
  mode: string;
  interception: boolean;
  businessWrites: boolean;
  writeStatus: string;
  checks: unknown[];
  tests: unknown[];
} = {
  mode: "REAL_HTTPS_API_SQL_READ_ONLY",
  interception: false,
  businessWrites: false,
  writeStatus: "LIVE_WRITE_BLOCKED_APPROVAL",
  checks: [],
  tests: [],
};
function record(name: string, data: unknown) {
  report.checks.push({ name, data });
  fs.writeFileSync(
    path.join(root, "docs/fs006/LIVE_RESULTS.json"),
    JSON.stringify(report, null, 2) + "\n",
  );
}
async function login(context: BrowserContext, key: string) {
  const csrf = await context.request.get("/api/auth/csrf");
  expect(csrf.status()).toBe(200);
  const token = await csrf.json();
  const response = await context.request.post("/api/auth/login", {
    headers: { "X-CSRF-TOKEN": token.csrfToken },
    data: {
      username: accounts[key].username,
      password: accounts[key].password,
    },
  });
  expect(response.status(), "existing valid fixture authentication").toBe(200);
  expect((await context.request.get("/api/auth/session")).status()).toBe(200);
}
const b = fixture.branchId,
  task = fixture.taskId,
  url = `/api/branches/${b}/cleaning-tasks/${task}/assignment-options`;
test.afterEach(async ({}, info) => {
  report.tests.push({ name: info.title, status: info.status });
  record("test-result", info.status);
});
test.afterAll(() => {
  const after = privateCall(["inspect", "--local-sql-certificate-exception"]);
  expect(after).toEqual(before);
  record("unchanged-retained-task-SQL", after);
});
test("FS006 assignment planning real GET: auth, scope, DTO, paging, filters and blocked writes", async ({
  page,
  browser,
}) => {
  const anon = await browser.newContext();
  try {
    expect((await anon.request.get(url)).status()).toBe(401);
  } finally {
    await anon.close();
  }
  await login(page.context(), "owner");
  expect((await page.request.get(url)).status()).toBe(403);
  await page.context().clearCookies();
  await login(page.context(), "admin");
  const r = await page.request.get(`${url}?page=1&pageSize=20`);
  expect(r.status()).toBe(200);
  const data = await r.json();
  expect(data.task.id).toBe(task);
  expect(data.task.branchId).toBe(b);
  expect(data.task.recordedStatus).toBe(before.status);
  expect(r.headers().etag).toMatch(/^"[0-9A-F]{16}"$/);
  expect(data.total).toBeGreaterThan(0);
  const other = await page.request.get(
    `/api/branches/${fixture.foreignBranchId}/cleaning-tasks/${task}/assignment-options`,
  );
  expect(other.status()).toBe(403);
  expect(
    (
      await page.request.get(
        `/api/branches/${b}/cleaning-tasks/9223372036854775807/assignment-options`,
      )
    ).status(),
  ).toBe(404);
  expect((await page.request.get(`${url}?pageSize=101`)).status()).toBe(400);
  const second = await page.request.get(`${url}?page=2&pageSize=1`);
  expect(second.status()).toBe(200);
  expect((await second.json()).filters.page).toBe(2);
  const empty = await page.request.get(`${url}?search=FS006_NO_MATCH_49382`);
  expect(empty.status()).toBe(200);
  expect((await empty.json()).total).toBe(0);
  await page.goto(`/cleaning/assignment?branchId=${b}&taskId=${task}`);
  await expect(
    page.getByRole("heading", {
      name: `${data.task.number} · ${data.task.roomNumber}`,
    }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Lưu phân công", exact: true }),
  ).toBeDisabled();
  await expect(
    page.getByRole("button", { name: "Kiểm tra bản nháp", exact: true }),
  ).toBeDisabled();
  await page
    .getByLabel("Tìm nhân viên", { exact: true })
    .fill("FS006_NO_MATCH_49382");
  await page
    .getByRole("button", { name: "Tải phương án", exact: true })
    .click();
  await expect(
    page.getByText("Không có ứng viên phù hợp.", { exact: true }),
  ).toBeVisible();
  record("planning-real-GET", {
    anonymous: 401,
    missingPermission: 403,
    foreignBranch: 403,
    missingTask: 404,
    invalidPage: 400,
    valid: 200,
    total: data.total,
    paging: true,
    searchEmpty: true,
    etag: true,
    uiRead: true,
    mutationDisabled: true,
  });
});
test("FS006 preserves real Monitor/UC032 reads and does not offer mock create", async ({
  page,
}) => {
  await login(page.context(), "owner");
  const progress = await page.request.get(
    `/api/branches/${b}/cleaning-tasks/${task}/progress`,
  );
  expect(progress.status()).toBe(200);
  expect((await progress.json()).percent).toBe(before.percent);
  await page.context().clearCookies();
  await login(page.context(), "admin");
  const r = await page.request.get(
    `/api/branches/${b}/cleaning-monitor/cleaning/${task}?timelinePageSize=20`,
  );
  expect(r.status()).toBe(200);
  const detail = await r.json();
  expect(detail.item.progressPercent).toBe(before.percent);
  expect(detail.progressCount).toBe(before.logCount);
  expect(detail.auditCount).toBe(before.auditCount);
  await page.goto(`/cleaning/tasks/${task}?branchId=${b}&monitor=1`);
  await page
    .getByRole("link", { name: "Xem phương án phân công", exact: true })
    .click();
  await expect(
    page.getByRole("heading", {
      name: `${detail.item.taskNumber} · ${detail.item.roomNumber}`,
    }),
  ).toBeVisible();
  await page.goto(`/cleaning/tasks/new?branchId=${b}`);
  await expect(
    page.getByRole("button", { name: "Tạo công việc", exact: true }),
  ).toBeDisabled();
  record("read-regression", {
    uc032Read: 200,
    monitorRead: 200,
    percent: before.percent,
    logCount: before.logCount,
    auditCount: before.auditCount,
    detailToPlanning: true,
    createBlocked: true,
  });
});
