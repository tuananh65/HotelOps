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
  process.env.FS005_LIVE_ENABLED !== "APPROVED_FS005_READ_ONLY" ||
  fixture.database !== "HotelOps_Canonical_20261105_180000_f040001_Fresh" ||
  fixture.owner !== "DEV1_FS004"
)
  throw Error("TRIAL_SCOPE_MISMATCH");
const helper = path.join(directory, "bin/Debug/net10.0/FS004.Verification.dll");
function privateCall(args: string[]) {
  const r = spawnSync("dotnet", [helper, ...args], {
    encoding: "utf8",
    env: { ...process.env, FS004_LIVE_ENABLED: "APPROVED_FS004_LOCAL_TRIAL" },
    windowsHide: true,
    timeout: 30000,
  });
  if (r.status !== 0) throw Error("HELPER_FAILED; private output redacted");
  return JSON.parse(r.stdout.replace(/^\uFEFF/, ""));
}
const accounts = privateCall(["credentials-ipc", "--private-ipc"]);
const before = privateCall(["inspect", "--local-sql-certificate-exception"]);
const report: {
  interception: boolean;
  businessWrites: boolean;
  checks: unknown[];
  tests: unknown[];
} = { interception: false, businessWrites: false, checks: [], tests: [] };
function record(name: string, data: unknown) {
  report.checks.push({ name, data });
  fs.writeFileSync(
    path.join(root, "docs/fs005/LIVE_RESULTS.json"),
    JSON.stringify(report, null, 2) + "\n",
  );
}
async function login(context: BrowserContext, key: string) {
  const csrf = await context.request.get("/api/auth/csrf");
  expect(csrf.status()).toBe(200);
  const token = await csrf.json();
  const r = await context.request.post("/api/auth/login", {
    headers: { "X-CSRF-TOKEN": token.csrfToken },
    data: {
      username: accounts[key].username,
      password: accounts[key].password,
    },
  });
  expect(r.status(), "real fixture login").toBe(200);
  expect((await context.request.get("/api/auth/session")).status()).toBe(200);
}
const b = fixture.branchId,
  task = fixture.taskId;
const base = `/api/branches/${b}/cleaning-monitor`;
// Inclusive dates cover the retained FS004 task without changing/reseeding its schedule.
const range = "fromDate=2026-11-05&toDate=2026-11-06";
test.afterEach(async ({}, info) => {
  report.tests.push({ name: info.title, status: info.status });
  record("test-result", info.status);
});
test.afterAll(() => {
  const after = privateCall(["inspect", "--local-sql-certificate-exception"]);
  expect(after).toEqual(before);
  record("unchanged-SQL-business-state", after);
});
test("FS005 real authentication, branch scope, filters, paging and SQL-backed detail", async ({
  page,
  browser,
}) => {
  const anonymous = await browser.newContext();
  try {
    expect((await anonymous.request.get(base)).status()).toBe(401);
  } finally {
    await anonymous.close();
  }
  await login(page.context(), "owner");
  expect((await page.request.get(`${base}?${range}`)).status()).toBe(403);
  const progress = await page.request.get(
    `/api/branches/${b}/cleaning-tasks/${task}/progress`,
  );
  expect(progress.status()).toBe(200);
  expect((await progress.json()).percent).toBe(before.percent);
  await page.context().clearCookies();
  await login(page.context(), "admin");
  const r = await page.request.get(`${base}?${range}&pageSize=1`);
  expect(r.status()).toBe(200);
  const list = await r.json();
  expect(list.total).toBeGreaterThan(0);
  expect(list.items).toHaveLength(1);
  const detail = await page.request.get(
    `${base}/cleaning/${task}?timelinePage=1&timelinePageSize=20`,
  );
  expect(detail.status()).toBe(200);
  const d = await detail.json();
  expect(d.item.id).toBe(task);
  expect(d.item.progressPercent).toBe(before.percent);
  expect(d.progressCount).toBe(before.logCount);
  expect(d.auditCount).toBe(before.auditCount);
  expect(d.item.recordedStatus).toBe(before.status ?? before.Status);
  for (const suffix of [
    `floorId=${d.item.floorId}`,
    `status=${d.item.recordedStatus}`,
    "kind=cleaning",
    "overdue=true",
    ...(d.item.priorityId ? [`priorityId=${d.item.priorityId}`] : []),
  ]) {
    const response = await page.request.get(`${base}?${range}&${suffix}`);
    expect(response.status()).toBe(200);
    const body = await response.json();
    if (suffix !== "overdue=true")
      expect(body.items.some((x: { id: number }) => x.id === task)).toBe(true);
    else
      expect(
        body.items.every((x: { isOverdue: boolean }) => x.isOverdue === true),
      ).toBe(true);
  }
  expect(
    (
      await page.request.get(`${base}?${range}&status=INVALID%20STATE`)
    ).status(),
  ).toBe(400);
  const own = await page.request.get(
    `${base}?${range}&assigneeId=${accounts.owner.userId}&kind=cleaning`,
  );
  expect(own.status()).toBe(200);
  expect(
    (await own.json()).items.some((x: { id: number }) => x.id === task),
  ).toBe(true);
  const search = await page.request.get(
    `${base}?${range}&search=${encodeURIComponent(d.item.taskNumber)}`,
  );
  expect(search.status()).toBe(200);
  expect(
    (await search.json()).items.some((x: { id: number }) => x.id === task),
  ).toBe(true);
  const empty = await page.request.get(
    `${base}?${range}&search=FS005_NO_MATCH_8391`,
  );
  expect(empty.status()).toBe(200);
  expect((await empty.json()).total).toBe(0);
  const later = await page.request.get(`${base}?${range}&page=2&pageSize=1`);
  expect(later.status()).toBe(200);
  const second = await later.json();
  expect(second.page).toBe(2);
  if (list.total > 1) {
    expect(second.items).toHaveLength(1);
    expect(second.items[0].id).not.toBe(list.items[0].id);
  }
  expect(
    (await page.request.get(`${base}?${range}&pageSize=101`)).status(),
  ).toBe(400);
  expect(
    (await page.request.get(`${base}/cleaning/9223372036854775807`)).status(),
  ).toBe(404);
  expect(
    (
      await page.request.get(
        `/api/branches/${fixture.foreignBranchId}/cleaning-monitor?${range}`,
      )
    ).status(),
  ).toBe(403);
  expect(
    (
      await page.request.get(
        `/api/branches/${fixture.foreignBranchId}/cleaning-monitor/cleaning/${task}`,
      )
    ).status(),
  ).toBe(403);
  await page.goto(`/cleaning/tasks?branchId=${b}&${range}`);
  await expect(
    page.getByRole("link", { name: d.item.taskNumber, exact: true }),
  ).toBeVisible();
  await page
    .getByRole("link", { name: d.item.taskNumber, exact: true })
    .click();
  await expect(
    page.getByRole("heading", {
      name: `${d.item.taskNumber} · ${d.item.roomNumber}`,
    }),
  ).toBeVisible();
  await expect(
    page.getByText(d.item.assigneeName, { exact: true }),
  ).toBeVisible();
  await page.goto(
    `/room-readiness?branchId=${b}&search=${encodeURIComponent(d.item.roomNumber)}`,
  );
  await page
    .getByRole("link", { name: "Xem công việc vệ sinh", exact: true })
    .click();
  await expect(page).toHaveURL(/\/cleaning\/tasks\?/);
  // Retained trial schedule is yesterday; select dates through the actual UI.
  await page.getByLabel("Từ ngày", { exact: true }).fill("2026-11-05");
  await page.getByLabel("Đến ngày", { exact: true }).fill("2026-11-06");
  await page.getByRole("button", { name: "Tải dữ liệu", exact: true }).click();
  await page
    .getByRole("link", { name: d.item.taskNumber, exact: true })
    .click();
  await expect(
    page.getByRole("heading", {
      name: `${d.item.taskNumber} · ${d.item.roomNumber}`,
    }),
  ).toBeVisible();
  record("live-read-acceptance", {
    anonymous: 401,
    ownMonitor: 403,
    foreignList: 403,
    foreignDetail: 403,
    missing: 404,
    invalidPage: 400,
    task,
    branch: b,
    total: list.total,
    percent: d.item.progressPercent,
    progressCount: d.progressCount,
    auditCount: d.auditCount,
    readinessNavigation: true,
    uc032Read: true,
  });
});
