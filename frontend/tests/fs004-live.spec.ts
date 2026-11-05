import {
  test,
  expect,
  type BrowserContext,
  type APIResponse,
} from "@playwright/test";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
const root = path.resolve("..");
const target = "HotelOps_Canonical_20261105_180000_f040001_Fresh";
const directory = path.join(root, "scripts/verification/FS004");
const helper = path.join(directory, "bin/Debug/net10.0/FS004.Verification.dll");
const fixture = JSON.parse(
  fs
    .readFileSync(path.join(directory, ".private/fixture.json"), "utf8")
    .replace(/^\uFEFF/, ""),
);
if (
  process.env.FS004_LIVE_ENABLED !== "APPROVED_FS004_LOCAL_TRIAL" ||
  fixture.database !== target ||
  fixture.owner !== "DEV1_FS004"
)
  throw new Error("APPROVED_FIXTURE_SCOPE_MISMATCH");
function privateCall(args: string[]) {
  const child = spawnSync("dotnet", [helper, ...args], {
    encoding: "utf8",
    env: process.env,
    windowsHide: true,
    timeout: 30000,
  });
  if (child.status !== 0)
    throw new Error(
      "LOCAL_VERIFICATION_HELPER_FAILED; private payload redacted",
    );
  try {
    return JSON.parse(child.stdout.replace(/^\uFEFF/, ""));
  } catch {
    throw new Error("LOCAL_VERIFICATION_RECEIPT_INVALID");
  }
}
const accounts = privateCall(["credentials-ipc", "--private-ipc"]);
const report: {
  mode: string;
  interception: boolean;
  privateKeyExport: boolean;
  target: string;
  completed: boolean;
  checks: unknown[];
  tests: { test: string; status: string }[];
} = {
  mode: "REAL_BROWSER_GATEWAY_VITE_HTTPS_API_SQL",
  interception: false,
  privateKeyExport: false,
  target,
  completed: false,
  checks: [],
  tests: [],
};
function record(name: string, values: Record<string, unknown>) {
  report.checks.push({ name, ...values });
  fs.writeFileSync(
    path.join(root, "docs/fs004/LIVE_RESULTS.json"),
    JSON.stringify(report, null, 2) + "\n",
  );
}
async function request(
  context: BrowserContext,
  method: "GET" | "POST" | "PUT",
  url: string,
  options: object = {},
): Promise<APIResponse> {
  try {
    return await context.request.fetch(url, {
      method,
      maxRedirects: 0,
      ...options,
    });
  } catch {
    throw new Error("LIVE_HTTPS_REQUEST_FAILED; headers/tokens/body redacted");
  }
}
async function csrf(context: BrowserContext) {
  const response = await request(context, "GET", "/api/auth/csrf");
  expect(response.status()).toBe(200);
  const data = await response.json();
  expect(data.headerName).toBe("X-CSRF-TOKEN");
  if (typeof data.csrfToken !== "string" || !data.csrfToken)
    throw new Error("CSRF_BOOTSTRAP_INVALID");
  return data.csrfToken as string;
}
async function login(context: BrowserContext, key: string) {
  const token = await csrf(context);
  const response = await request(context, "POST", "/api/auth/login", {
    headers: { "X-CSRF-TOKEN": token },
    data: {
      username: accounts[key].username,
      password: accounts[key].password,
    },
  });
  expect(response.status(), "Real credential login").toBe(200);
  const session = await request(context, "GET", "/api/auth/session");
  expect(session.status()).toBe(200);
  const body = await session.json();
  expect(body.user.id).toBe(String(accounts[key].userId));
  expect(body.mustChangePassword).toBe(false);
}
async function put(
  context: BrowserContext,
  url: string,
  data: object,
  tag?: string,
) {
  const token = await csrf(context);
  return request(context, "PUT", url, {
    headers: { "X-CSRF-TOKEN": token, ...(tag ? { "If-Match": tag } : {}) },
    data,
  });
}
test.describe.configure({ mode: "serial" });
test.afterEach(async ({}, info) => {
  report.tests.push({ test: info.title, status: info.status || "unknown" });
  record("test-result", { status: info.status });
});
test.afterAll(() => {
  report.completed =
    report.tests.length === 2 &&
    report.tests.every((t) => t.status === "passed");
  record("suite-result", {
    completed: report.completed,
    authenticationMethod:
      "Real API login, shared browser cookie jar; frontend SignIn UI not certified",
  });
});

test("FS004 real room directory -> Vite proxy -> SQL -> React; scope and pagination", async ({
  page,
  browser,
}) => {
  const owner = page.context();
  await login(owner, "owner");
  const branch = fixture.branchId;
  const page1 = await request(
    owner,
    "GET",
    `/api/rooms?branchId=${branch}&page=1&pageSize=20&sort=floor`,
  );
  expect(page1.status()).toBe(200);
  const first = await page1.json();
  expect(first.totalCount).toBe(25);
  expect(first.items).toHaveLength(20);
  expect(
    first.items.every((room: { branchId: number }) => room.branchId === branch),
  ).toBe(true);
  const page2 = await request(
    owner,
    "GET",
    `/api/rooms?branchId=${branch}&page=2&pageSize=20&sort=floor`,
  );
  expect(page2.status()).toBe(200);
  const second = await page2.json();
  expect(second.items).toHaveLength(5);
  expect(
    second.items.every(
      (room: { branchId: number }) => room.branchId === branch,
    ),
  ).toBe(true);
  expect(
    new Set([...first.items, ...second.items].map((room) => room.id)).size,
  ).toBe(25);
  expect(
    (
      await request(
        owner,
        "GET",
        `/api/rooms?branchId=${fixture.foreignBranchId}`,
      )
    ).status(),
  ).toBe(403);
  expect(
    (
      await request(owner, "GET", `/api/rooms?branchId=${branch}&page=0`)
    ).status(),
  ).toBe(400);
  const anonymous = await browser.newContext({
    baseURL: "https://localhost:5173",
  });
  try {
    expect(
      (
        await request(anonymous, "GET", `/api/rooms?branchId=${branch}`)
      ).status(),
    ).toBe(401);
  } finally {
    await anonymous.close();
  }
  const denied = await browser.newContext({
    baseURL: "https://localhost:5173",
  });
  try {
    await login(denied, "denied");
    expect(
      (await request(denied, "GET", `/api/rooms?branchId=${branch}`)).status(),
    ).toBe(403);
  } finally {
    await denied.close();
  }
  await page.goto(`/room-readiness?branchId=${branch}`, {
    waitUntil: "domcontentloaded",
  });
  await expect(
    page.getByText(fixture.firstRoomNumber, { exact: true }),
  ).toBeVisible();
  await expect(page.locator("article.room-tile")).toHaveCount(20);
  await page.getByRole("button", { name: "Trang sau", exact: true }).click();
  await expect(page.locator("article.room-tile")).toHaveCount(5);
  await page.getByLabel("Số phòng", { exact: true }).fill("FS004-NO-SUCH-ROOM");
  await page.getByRole("button", { name: "Tải dữ liệu", exact: true }).click();
  await expect(
    page.getByText("Không có phòng phù hợp bộ lọc.", { exact: true }),
  ).toBeVisible();
  await owner.setOffline(true);
  try {
    await page
      .getByRole("button", { name: "Tải dữ liệu", exact: true })
      .click();
    await expect(page.getByRole("alert")).toBeVisible();
    await expect(page.locator("article.room-tile")).toHaveCount(0);
  } finally {
    await owner.setOffline(false);
  }
  record("rooms-live", {
    authenticated: 200,
    anonymous: 401,
    denied: 403,
    foreignBranch: 403,
    invalidQuery: 400,
    totalCount: first.totalCount,
    pageSizes: [first.items.length, second.items.length],
    browserMatchesSqlFixture: true,
    empty: true,
    realBrowserOfflineFailure: true,
    noMockFallback: true,
  });
});

test("FS004 UC032 real UI save100, SQL audit/evidence and two-client stale412", async ({
  page,
  browser,
}) => {
  const owner = page.context();
  await login(owner, "owner");
  const url = `/api/branches/${fixture.branchId}/cleaning-tasks/${fixture.taskId}/progress`;
  const read = await request(owner, "GET", url);
  expect(read.status()).toBe(200);
  const initial = await read.json();
  const tag = read.headers().etag;
  expect(tag).toMatch(/^"[0-9A-F]{16}"$/);
  expect(initial.state).toBe("IN_PROGRESS");
  expect(initial.items).toHaveLength(2);
  const baseline = privateCall([
    "inspect",
    "--local-sql-certificate-exception",
  ]);
  const input = {
    percent: 20,
    items: initial.items.map((item: { id: number; done: boolean }) => ({
      id: item.id,
      done: item.done,
      note: null,
    })),
    note: "FS004 validation",
  };
  expect((await put(owner, url, input)).status()).toBe(428);
  expect(
    (await put(owner, url, { ...input, percent: 101 }, tag)).status(),
  ).toBe(400);
  expect((await put(owner, url, input, "W/" + tag)).status()).toBe(400);
  expect(
    (
      await request(owner, "PUT", url, {
        headers: { "If-Match": tag },
        data: input,
      })
    ).status(),
  ).toBe(400);
  expect(
    (await request(owner, "GET", url + "/evidence/99999999")).status(),
  ).toBe(404);
  expect(
    (
      await put(
        owner,
        `/api/branches/${fixture.branchId}/cleaning-tasks/${fixture.inactiveTaskId}/progress`,
        input,
        tag,
      )
    ).status(),
  ).toBe(409);
  expect(
    (
      await request(
        owner,
        "GET",
        `/api/branches/${fixture.foreignBranchId}/cleaning-tasks/${fixture.taskId}/progress`,
      )
    ).status(),
  ).toBe(403);
  const other = await browser.newContext({ baseURL: "https://localhost:5173" });
  try {
    await login(other, "other");
    expect((await request(other, "GET", url)).status()).toBe(403);
    expect((await put(other, url, input, tag)).status()).toBe(403);
  } finally {
    await other.close();
  }
  const anonymous = await browser.newContext({
    baseURL: "https://localhost:5173",
  });
  try {
    expect((await put(anonymous, url, input, tag)).status()).toBe(401);
  } finally {
    await anonymous.close();
  }
  await page.goto(
    `/cleaning/tasks/${fixture.taskId}?branchId=${fixture.branchId}`,
    { waitUntil: "domcontentloaded" },
  );
  await expect(page.getByLabel("Tiến độ (%)")).toBeVisible();
  await page.getByLabel("Tiến độ (%)").fill("100");
  for (const checkbox of await page.getByRole("checkbox").all())
    await checkbox.check();
  await page
    .getByLabel("Ghi chú tiến độ", { exact: true })
    .fill("FS004 real UI progress");
  await page
    .getByLabel("Tệp đính kèm (tối đa 5)", { exact: true })
    .setInputFiles({
      name: "fs004-synthetic.png",
      mimeType: "image/png",
      buffer: Buffer.from(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jBz8AAAAASUVORK5CYII=",
        "base64",
      ),
    });
  await page.getByRole("button", { name: "Lưu tiến độ", exact: true }).click();
  await expect(page.getByText(/Đã lưu tiến độ/)).toBeVisible();
  await expect(page.getByLabel("Tiến độ (%)")).toHaveValue("100");
  await expect(
    page.getByRole("button", { name: "Lưu tiến độ", exact: true }),
  ).toBeEnabled();
  const at100 = privateCall([
    "persistence",
    "--local-sql-certificate-exception",
    "--expected-percent=100",
    `--expected-log-count=${baseline.logCount + 1}`,
  ]);
  expect(at100.status).toBe("IN_PROGRESS");
  expect(at100.evidenceCount).toBe(baseline.evidenceCount + 1);
  expect(at100.evidenceFilesPrivate).toBe(true);
  const secondClient = await browser.newContext({
    baseURL: "https://localhost:5173",
  });
  try {
    await login(secondClient, "owner");
    const secondPage = await secondClient.newPage();
    await secondPage.goto(
      `/cleaning/tasks/${fixture.taskId}?branchId=${fixture.branchId}`,
      { waitUntil: "domcontentloaded" },
    );
    await expect(secondPage.getByLabel("Tiến độ (%)")).toHaveValue("100");
    const versionA = (await request(owner, "GET", url)).headers().etag;
    const versionB = (await request(secondClient, "GET", url)).headers().etag;
    expect(versionA).toBe(versionB);
    await page.getByLabel("Tiến độ (%)").fill("40");
    await page
      .getByRole("button", { name: "Lưu tiến độ", exact: true })
      .click();
    await expect(
      page.getByRole("button", { name: "Lưu tiến độ", exact: true }),
    ).toBeEnabled();
    await expect(page.getByLabel("Tiến độ (%)")).toHaveValue("40");
    let secondPuts = 0;
    secondPage.on("request", (req) => {
      if (req.method() === "PUT" && req.url().endsWith(url)) secondPuts++;
    });
    const stale = secondPage.waitForResponse(
      (response) =>
        response.request().method() === "PUT" && response.url().endsWith(url),
    );
    await secondPage.getByLabel("Tiến độ (%)").fill("60");
    await secondPage
      .getByRole("button", { name: "Lưu tiến độ", exact: true })
      .click();
    expect((await stale).status()).toBe(412);
    await expect(secondPage.getByRole("alert")).toContainText(
      "Dữ liệu đã thay đổi",
    );
    await expect(
      secondPage.getByRole("button", { name: "Lưu tiến độ", exact: true }),
    ).toBeDisabled();
    expect(secondPuts).toBe(1);
    expect((await (await request(owner, "GET", url)).json()).percent).toBe(40);
    const persisted = privateCall([
      "persistence",
      "--local-sql-certificate-exception",
      "--expected-percent=40",
      `--expected-log-count=${baseline.logCount + 2}`,
    ]);
    expect(persisted.auditCount - baseline.auditCount).toBe(2);
    expect(persisted.ownerIsAuthor).toBe(true);
    expect(persisted.auditMatches).toBe(true);
    record("uc032-live", {
      read: 200,
      validPut: 200,
      stalePut: 412,
      missingVersion: 428,
      invalidInput: 400,
      csrfRejected: 400,
      anonymous: 401,
      otherAssignee: 403,
      foreignBranch: 403,
      missingEvidence: 404,
      inactivePhase: 409,
      stateAt100: at100.status,
      percentAfterConflict: persisted.percent,
      newLogs: persisted.logCount - baseline.logCount,
      newAudits: persisted.auditCount - baseline.auditCount,
      evidenceFilesPrivate: persisted.evidenceFilesPrivate,
      secondClientPuts: secondPuts,
      sqlPersistence: true,
    });
  } finally {
    await secondClient.close();
  }
});
