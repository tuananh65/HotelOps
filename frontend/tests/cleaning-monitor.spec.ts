import { test, expect } from "@playwright/test";
// HTTP interception only; separate from FS005 live SQL acceptance.
const item = {
  kind: "cleaning",
  id: 12,
  cleaningTaskId: 12,
  taskNumber: "API-CL-12",
  roomId: 3,
  roomNumber: "101",
  floorId: 2,
  floorName: null,
  recordedStatus: "IN_PROGRESS",
  workflowState: null,
  priorityId: null,
  priorityCode: null,
  assigneeId: 5,
  assigneeName: "API Staff",
  dueAtUtc: null,
  isOverdue: null,
  progressPercent: 40,
  blockerCode: null,
  blockerNote: null,
  description: null,
};
const meta = {
  fromDate: "2026-11-06",
  toDate: "2026-11-06",
  timezone: "Asia/Ho_Chi_Minh",
  asOfUtc: "2026-11-06T00:00:00Z",
};
test("monitor distinguishes current cycle and unresolved blocker from historical work", async ({
  page,
}) => {
  await page.route("**/api/branches/7/cleaning-monitor?**", (route) =>
    route.fulfill({
      json: {
        ...meta,
        total: 2,
        page: 1,
        pageSize: 20,
        items: [
          {
            ...item,
            isCurrentCycle: true,
            hasUnresolvedBlocker: true,
            blockerCode: "GUEST_PRESENT",
          },
          {
            ...item,
            id: 13,
            taskNumber: "HISTORICAL-13",
            isCurrentCycle: false,
            hasUnresolvedBlocker: false,
          },
        ],
      },
    }),
  );
  await page.goto("/cleaning/tasks?branchId=7");
  const current = page.getByRole("row").filter({ hasText: "API-CL-12" });
  await expect(
    current.getByText("Chưa xử lý GUEST_PRESENT", { exact: true }),
  ).toBeVisible();
  await expect(current.getByRole("cell").nth(9)).toHaveText("Có");
  const historical = page.getByRole("row").filter({ hasText: "HISTORICAL-13" });
  await expect(historical.getByRole("cell").nth(9)).toHaveText("Không");
  await expect(historical.getByRole("cell").nth(10)).toHaveText("Không");
});
test("monitor list filters, paginates and opens real detail mode", async ({
  page,
}) => {
  const requests: URL[] = [];
  await page.route("**/api/branches/7/cleaning-monitor?**", (route) => {
    const q = new URL(route.request().url());
    requests.push(q);
    return route.fulfill({
      json: {
        ...meta,
        items: [item],
        total: 21,
        page: Number(q.searchParams.get("page")),
        pageSize: 20,
      },
    });
  });
  await page.route(
    "**/api/branches/7/cleaning-monitor/cleaning/12?**",
    (route) =>
      route.fulfill({
        json: {
          item,
          assignments: [],
          assignmentCount: 0,
          progress: [],
          progressCount: 0,
          audit: [],
          auditCount: 0,
          timelineLimit: 20,
          timelinePage: 1,
          asOfUtc: meta.asOfUtc,
        },
      }),
  );
  await page.goto("/cleaning/tasks?branchId=7");
  await expect(
    page.getByRole("link", { name: "API-CL-12", exact: true }),
  ).toBeVisible();
  await page.getByLabel("Tìm kiếm", { exact: true }).fill("101");
  await page.getByRole("button", { name: "Tải dữ liệu", exact: true }).click();
  await expect
    .poll(() => requests.at(-1)?.searchParams.get("search"))
    .toBe("101");
  await page.getByRole("button", { name: "Trang sau", exact: true }).click();
  await expect.poll(() => requests.at(-1)?.searchParams.get("page")).toBe("2");
  await page.getByRole("link", { name: "API-CL-12", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "API-CL-12 · 101" }),
  ).toBeVisible();
  await expect(page.getByText("API Staff", { exact: true })).toBeVisible();
  await expect(page.getByRole("link", { name: /Mở UC032/ })).toHaveAttribute(
    "href",
    "/cleaning/tasks/12?branchId=7",
  );
});
for (const status of [401, 403, 404, 503])
  test(`monitor ${status} never falls back`, async ({ page }) => {
    await page.route("**/api/branches/7/cleaning-monitor?**", (r) =>
      r.fulfill({ status, json: { title: "DENIED" } }),
    );
    await page.goto("/cleaning/tasks?branchId=7");
    await expect(page.getByRole("alert")).toBeVisible();
    await expect(page.locator("tbody tr")).toHaveCount(0);
  });
test("monitor empty response", async ({ page }) => {
  await page.route("**/api/branches/7/cleaning-monitor?**", (r) =>
    r.fulfill({
      json: { ...meta, items: [], total: 0, page: 1, pageSize: 20 },
    }),
  );
  await page.goto("/cleaning/tasks?branchId=7");
  await expect(
    page.getByText("Không có công việc phù hợp bộ lọc."),
  ).toBeVisible();
});

test("monitor ignores a stale branch response after filter cancellation", async ({
  page,
}) => {
  let release!: () => void;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route("**/api/branches/7/cleaning-monitor?**", async (r) => {
    await held;
    await r
      .fulfill({
        json: { ...meta, items: [item], total: 1, page: 1, pageSize: 20 },
      })
      .catch(() => {});
  });
  await page.route("**/api/branches/8/cleaning-monitor?**", (r) =>
    r.fulfill({
      json: { ...meta, items: [], total: 0, page: 1, pageSize: 20 },
    }),
  );
  await page.goto("/cleaning/tasks?branchId=7", {
    waitUntil: "domcontentloaded",
  });
  await page.getByLabel("Chi nhánh (ID)", { exact: true }).fill("8");
  await page.getByRole("button", { name: "Tải dữ liệu", exact: true }).click();
  await expect(
    page.getByText("Không có công việc phù hợp bộ lọc."),
  ).toBeVisible();
  release();
  await expect(
    page.getByRole("link", { name: "API-CL-12", exact: true }),
  ).toHaveCount(0);
});
for (const status of [401, 403, 404, 503])
  test(`detail ${status} has no prototype fallback`, async ({ page }) => {
    await page.route("**/api/branches/7/cleaning-monitor/cleaning/12?**", (r) =>
      r.fulfill({ status, json: { title: "DENIED" } }),
    );
    await page.goto("/cleaning/tasks/12?branchId=7&monitor=1");
    await expect(page.getByRole("alert")).toBeVisible();
    await expect(page.getByRole("link", { name: /Mở UC032/ })).toHaveCount(0);
  });
