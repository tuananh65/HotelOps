import { test, expect } from "@playwright/test";
// Intercepted contract tests only. Never count these as SQL write acceptance.
const plan = {
  task: {
    id: 12,
    number: "API-CL-12",
    roomId: 3,
    roomNumber: "101",
    branchId: 7,
    recordedStatus: "PENDING",
    dueAt: null,
    workflowState: "PENDING_ASSIGNMENT",
  },
  candidates: [
    {
      id: 5,
      name: "API Staff",
      departmentId: 2,
      openTasks: 2,
      openTasksInWindow: 1,
    },
  ],
  recordedShifts: [],
  total: 1,
  filters: {
    search: null,
    from: "2026-11-06T00:00:00Z",
    to: "2026-11-07T00:00:00Z",
    page: 1,
    pageSize: 20,
  },
  checklist: null,
  capturedAt: "2026-11-06T00:00:00Z",
  workflowStateVerified: true,
};
test("create cannot save prototype data when real create contract is blocked", async ({
  page,
}) => {
  const mutations: string[] = [];
  page.on("request", (r) => {
    if (r.url().includes("/api/") && !["GET", "HEAD"].includes(r.method()))
      mutations.push(r.method());
  });
  await page.goto("/cleaning/tasks/new?branchId=7");
  await expect(
    page.getByRole("button", { name: "Tạo công việc", exact: true }),
  ).toBeDisabled();
  await expect(
    page.getByText("Chưa thể tạo công việc trên máy chủ.", { exact: true }),
  ).toBeVisible();
  expect(mutations).toEqual([]);
});
test("assignment previews exact server candidate draft but never sends a mutation", async ({
  page,
}) => {
  const mutations: string[] = [];
  page.on("request", (r) => {
    if (r.url().includes("/api/") && r.method() !== "GET")
      mutations.push(r.method());
  });
  await page.route(
    "**/api/branches/7/cleaning-tasks/12/assignment-options?**",
    (r) => r.fulfill({ json: plan }),
  );
  await page.goto("/cleaning/assignment?branchId=7&taskId=12");
  await expect(
    page.getByText("API Staff", { exact: true }).last(),
  ).toBeVisible();
  await page
    .getByLabel("Nhân viên (bản nháp)", { exact: true })
    .selectOption("5");
  await page
    .getByLabel("Hạn UTC (ISO 8601)", { exact: true })
    .fill(new Date(Date.now() + 3600000).toISOString());
  await page
    .getByLabel("Lý do phân công", { exact: true })
    .fill("Prepare draft only");
  await page
    .getByRole("button", { name: "Kiểm tra bản nháp", exact: true })
    .click();
  await expect(page.getByText(/Bản nháp hợp lệ về định dạng/)).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Lưu phân công", exact: true }),
  ).toBeDisabled();
  expect(mutations).toEqual([]);
});
for (const status of [400, 401, 403, 404, 409, 503])
  test(`assignment ${status} has no mock fallback`, async ({ page }) => {
    await page.route(
      "**/api/branches/7/cleaning-tasks/12/assignment-options?**",
      (r) => r.fulfill({ status, json: { title: "DENIED" } }),
    );
    await page.goto("/cleaning/assignment?branchId=7&taskId=12");
    await expect(page.getByRole("alert")).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Lưu phân công", exact: true }),
    ).toHaveCount(0);
  });
test("already assigned task cannot prepare a reassignment through initial assign", async ({
  page,
}) => {
  await page.route(
    "**/api/branches/7/cleaning-tasks/12/assignment-options?**",
    (r) =>
      r.fulfill({
        json: { ...plan, task: { ...plan.task, workflowState: "ASSIGNED" } },
      }),
  );
  await page.goto("/cleaning/assignment?branchId=7&taskId=12");
  await expect(
    page.getByRole("button", { name: "Kiểm tra bản nháp", exact: true }),
  ).toBeDisabled();
});

test("assignment paging and search reload server results", async ({ page }) => {
  const requests: URL[] = [];
  await page.route(
    "**/api/branches/7/cleaning-tasks/12/assignment-options?**",
    (r) => {
      const url = new URL(r.request().url());
      requests.push(url);
      const empty = url.searchParams.get("search") === "none";
      return r.fulfill({
        json: {
          ...plan,
          candidates: empty ? [] : plan.candidates,
          total: empty ? 0 : 21,
          filters: {
            ...plan.filters,
            page: Number(url.searchParams.get("page")),
            search: url.searchParams.get("search"),
          },
        },
      });
    },
  );
  await page.goto("/cleaning/assignment?branchId=7&taskId=12");
  await page.getByRole("button", { name: "Trang sau", exact: true }).click();
  await expect.poll(() => requests.at(-1)?.searchParams.get("page")).toBe("2");
  await page.getByLabel("Tìm nhân viên", { exact: true }).fill("none");
  await page
    .getByRole("button", { name: "Tải phương án", exact: true })
    .click();
  await expect(
    page.getByText("Không có ứng viên phù hợp.", { exact: true }),
  ).toBeVisible();
  expect(requests.at(-1)?.searchParams.get("page")).toBe("1");
});
test("assignment cancels stale branch results without retaining staff", async ({
  page,
}) => {
  let release!: () => void;
  const wait = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route(
    "**/api/branches/7/cleaning-tasks/12/assignment-options?**",
    async (r) => {
      await wait;
      await r.fulfill({ json: plan }).catch(() => {});
    },
  );
  await page.route(
    "**/api/branches/8/cleaning-tasks/12/assignment-options?**",
    (r) => r.fulfill({ status: 403, json: { title: "DENIED" } }),
  );
  await page.goto("/cleaning/assignment?branchId=7&taskId=12", {
    waitUntil: "domcontentloaded",
  });
  await page.getByLabel("Chi nhánh (ID)", { exact: true }).fill("8");
  await page
    .getByRole("button", { name: "Tải phương án", exact: true })
    .click();
  await expect(page.getByRole("alert")).toContainText("CLEANING_ASSIGN");
  release();
  await expect(page.getByText("API Staff", { exact: true })).toHaveCount(0);
});
