import { test, expect, type Page } from "@playwright/test";
// Intercepted UI contracts only. These are never real API/SQL lifecycle acceptance.
const tag = '"000000000000000A"';
const meta = {
  id: 12,
  branchId: 7,
  taskNumber: "EXEC-12",
  state: "ASSIGNED",
  assignmentId: 9,
  assigneeId: 5,
  actualStartAtUtc: null,
  sourceETag: tag,
  startCapability: "AVAILABLE",
  reassignCapability: "UNAVAILABLE",
};
async function source(page: Page, overrides: object = {}) {
  await page.route("**/api/branches/7/cleaning-tasks/12/execution", (r) =>
    r.fulfill({ headers: { ETag: tag }, json: { ...meta, ...overrides } }),
  );
}
const open = (page: Page) =>
  page.goto("/cleaning/tasks/12?branchId=7&execution=1");
for (const status of [401, 403, 503])
  test(`execution ${status} never exposes a mutable form`, async ({ page }) => {
    await page.route("**/api/branches/7/cleaning-tasks/12/execution", (r) =>
      r.fulfill({ status, json: { title: "DENIED" } }),
    );
    await open(page);
    await expect(page.getByRole("alert")).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Bắt đầu dọn phòng", exact: true }),
    ).toHaveCount(0);
    await expect(page.locator("form")).toHaveCount(0);
  });
test("unresolved blocker does not offer Resume or Start", async ({ page }) => {
  await source(page, {
    state: "ON_HOLD",
    startCapability: "BLOCKER_RESOLUTION_CONTRACT_REQUIRED",
  });
  await open(page);
  await expect(
    page.getByRole("button", { name: "Bắt đầu dọn phòng", exact: true }),
  ).toBeDisabled();
  await expect(page.getByText(/Chưa thể tiếp tục/)).toBeVisible();
});
test("Start uses current task ETag and fresh CSRF, then reloads source", async ({
  page,
}) => {
  let started = false;
  await page.route("**/api/branches/7/cleaning-tasks/12/execution", (r) =>
    r.fulfill({
      headers: { ETag: tag },
      json: {
        ...meta,
        state: started ? "IN_PROGRESS" : "ASSIGNED",
        startCapability: started ? "STATE_CONFLICT" : "AVAILABLE",
      },
    }),
  );
  await page.route("**/api/auth/csrf", (r) =>
    r.fulfill({
      json: { headerName: "X-CSRF-TOKEN", csrfToken: "interception-only" },
    }),
  );
  await page.route("**/api/branches/7/cleaning-tasks/12/start", async (r) => {
    expect(r.request().headers()["if-match"]).toBe(tag);
    expect(r.request().headers()["x-csrf-token"]).toBe("interception-only");
    expect(r.request().postDataJSON()).toEqual({ resolveBlocker: false });
    started = true;
    await r.fulfill({
      headers: { ETag: tag },
      json: {
        id: 12,
        branchId: 7,
        assignmentId: 9,
        state: "IN_PROGRESS",
        intervalId: 19,
        actualStartAtUtc: "2026-11-07T00:00:00Z",
      },
    });
  });
  await open(page);
  await page
    .getByRole("button", { name: "Bắt đầu dọn phòng", exact: true })
    .click();
  await expect(
    page.getByRole("link", { name: "Mở tiến độ dọn phòng" }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Bắt đầu dọn phòng", exact: true }),
  ).toBeDisabled();
});
test("Reassign retry preserves exact command identity and disables form editing", async ({
  page,
}) => {
  await source(page, { reassignCapability: "PROVISIONAL_NOT_ACCEPTED" });
  await page.route(
    "**/api/branches/7/cleaning-tasks/12/assignment-options?**",
    (r) =>
      r.fulfill({
        json: {
          task: {
            id: 12,
            number: "EXEC-12",
            roomId: 3,
            roomNumber: "101",
            branchId: 7,
            recordedStatus: "PENDING",
            dueAt: null,
            workflowState: "ASSIGNED",
          },
          candidates: [
            {
              id: 5,
              name: "Current owner",
              departmentId: 2,
              openTasks: 1,
              openTasksInWindow: 1,
            },
            {
              id: 6,
              name: "Next owner",
              departmentId: 2,
              openTasks: 0,
              openTasksInWindow: 0,
            },
          ],
          recordedShifts: [],
          total: 2,
          filters: {
            search: null,
            from: "2026-11-07T00:00:00Z",
            to: "2026-11-08T00:00:00Z",
            page: 1,
            pageSize: 20,
          },
          checklist: null,
          capturedAt: "2026-11-07T00:00:00Z",
          workflowStateVerified: true,
        },
      }),
  );
  await page.route("**/api/auth/csrf", (r) =>
    r.fulfill({
      json: { headerName: "X-CSRF-TOKEN", csrfToken: "interception-only" },
    }),
  );
  const writes: { body: unknown; key: string; tag: string }[] = [];
  await page.route(
    "**/api/branches/7/cleaning-tasks/12/reassign",
    async (r) => {
      writes.push({
        body: r.request().postDataJSON(),
        key: r.request().headers()["idempotency-key"],
        tag: r.request().headers()["if-match"],
      });
      await r.fulfill({ status: 503, json: { title: "TRANSIENT" } });
    },
  );
  await open(page);
  const select = page.getByLabel("Người phụ trách mới", { exact: true });
  await expect(select.locator('option[value="5"]')).toHaveCount(0);
  await select.selectOption("6");
  await page
    .getByLabel("Hạn UTC", { exact: true })
    .fill(new Date(Date.now() + 3600000).toISOString());
  await page
    .getByLabel("Lý do", { exact: true })
    .fill("Change staffing allocation");
  await page
    .getByRole("button", { name: "Lưu phân công lại", exact: true })
    .click();
  await expect(select).toBeDisabled();
  await page
    .getByRole("button", {
      name: "Thử lại đúng lệnh phân công lại",
      exact: true,
    })
    .click();
  await expect.poll(() => writes.length).toBe(2);
  expect(writes[0]).toEqual(writes[1]);
  expect(writes[0].key).toMatch(/^[0-9a-f-]{36}$/i);
});
