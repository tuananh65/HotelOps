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

const closureMeta = {
  state: "IN_PROGRESS",
  startCapability: "STATE_CONFLICT",
  reportBlockerCapability: "PROVISIONAL_V1",
  completeCapability: "PROVISIONAL_V1",
  progressETag: tag,
  blockerReasons: [
    { id: 2, code: "GUEST_IN_ROOM", name: "Khách còn trong phòng" },
  ],
  items: [{ id: 3, label: "Lau sàn", required: true, done: true }],
};
test("Complete stale response reloads both ETags and never automatically reposts", async ({
  page,
}) => {
  let changed = false,
    reads = 0,
    writes = 0;
  const latest = '"000000000000000B"';
  await page.route("**/api/branches/7/cleaning-tasks/12/execution", (r) => {
    reads++;
    return r.fulfill({
      headers: { ETag: changed ? latest : tag },
      json: {
        ...meta,
        ...closureMeta,
        sourceETag: changed ? latest : tag,
        progressETag: changed ? latest : tag,
      },
    });
  });
  await page.route("**/api/auth/csrf", (r) =>
    r.fulfill({
      json: { headerName: "X-CSRF-TOKEN", csrfToken: "interception-only" },
    }),
  );
  await page.route(
    "**/api/branches/7/cleaning-tasks/12/complete",
    async (r) => {
      writes++;
      expect(r.request().headers()["if-match"]).toBe(tag);
      changed = true;
      await r.fulfill({ status: 412, json: { title: "STALE" } });
    },
  );
  await open(page);
  await page
    .getByLabel("Ghi chú hoàn tất", { exact: true })
    .fill("Đã dọn sạch");
  const initialReads = reads;
  await page
    .getByRole("button", { name: "Hoàn tất dọn phòng", exact: true })
    .click();
  await expect(
    page.getByRole("status").filter({ hasText: "Công việc đã thay đổi. Tải lại nguồn trước lệnh mới." }),
  ).toBeVisible();
  await expect.poll(() => reads).toBeGreaterThan(initialReads);
  await expect(
    page.getByLabel("Ghi chú hoàn tất", { exact: true }),
  ).toHaveValue("");
  expect(writes).toBe(1);
});
test("Report uses authorized reason and retains exact command on staging retry", async ({
  page,
}) => {
  await source(page, closureMeta);
  await page.route("**/api/auth/csrf", (r) =>
    r.fulfill({
      json: { headerName: "X-CSRF-TOKEN", csrfToken: "interception-only" },
    }),
  );
  const writes: unknown[] = [];
  await page.route(
    "**/api/branches/7/cleaning-tasks/12/blockers",
    async (r) => {
      writes.push({
        body: r.request().postDataJSON(),
        tag: r.request().headers()["if-match"],
        key: r.request().headers()["idempotency-key"],
      });
      await r.fulfill({ status: 503, json: { title: "STAGING_FAILED" } });
    },
  );
  await open(page);
  await page.getByLabel("Lý do trở ngại", { exact: true }).selectOption("2");
  await page
    .getByLabel("Ghi chú trở ngại", { exact: true })
    .fill("Khách vẫn còn trong phòng");
  await page
    .getByRole("button", { name: "Báo cáo trở ngại", exact: true })
    .click();
  await expect(
    page.getByLabel("Lý do trở ngại", { exact: true }),
  ).toBeDisabled();
  await page
    .getByRole("button", {
      name: "Thử lại đúng lệnh cho công việc 12",
      exact: true,
    })
    .click();
  await expect.poll(() => writes.length).toBe(2);
  expect(writes[0]).toEqual(writes[1]);
  expect((writes[0] as any).body).toEqual({
    reasonId: 2,
    note: "Khách vẫn còn trong phòng",
  });
});
test("Complete remains disabled until required items are saved", async ({
  page,
}) => {
  await source(page, {
    ...closureMeta,
    items: [{ ...closureMeta.items[0], done: false }],
  });
  await open(page);
  await expect(
    page.getByRole("button", { name: "Hoàn tất dọn phòng", exact: true }),
  ).toBeDisabled();
  await expect(
    page.getByText("Lau sàn (bắt buộc)", { exact: false }),
  ).toBeVisible();
});
test("Complete posts both ETags and moves to Pending Inspection without READY", async ({
  page,
}) => {
  let completed = false;
  await page.route("**/api/branches/7/cleaning-tasks/12/execution", (r) =>
    r.fulfill({
      headers: { ETag: tag },
      json: {
        ...meta,
        ...closureMeta,
        ...(completed
          ? {
              state: "PENDING_INSPECTION",
              reportBlockerCapability: "UNAVAILABLE",
              completeCapability: "UNAVAILABLE",
            }
          : {}),
      },
    }),
  );
  await page.route("**/api/auth/csrf", (r) =>
    r.fulfill({
      json: { headerName: "X-CSRF-TOKEN", csrfToken: "interception-only" },
    }),
  );
  await page.route(
    "**/api/branches/7/cleaning-tasks/12/complete",
    async (r) => {
      expect(r.request().postDataJSON()).toEqual({
        note: "Đã dọn sạch",
        progressETag: tag,
      });
      expect(r.request().headers()["if-match"]).toBe(tag);
      completed = true;
      await r.fulfill({
        headers: { ETag: tag },
        json: {
          id: 12,
          branchId: 7,
          assignmentId: 9,
          operationId: r.request().headers()["idempotency-key"],
          publicationId: "12c44400-7ca4-4da1-8990-8d2fc258f211",
          progressId: 8,
          closedIntervalId: 4,
          state: "PENDING_INSPECTION",
          sourceETag: tag,
          occurredAtUtc: "2026-11-07T00:00:00Z",
          readiness: "PENDING_INSPECTION",
          hasEffectiveBlock: false,
          notification: "PENDING_DELIVERY",
          contract: "PROVISIONAL_NOT_ACCEPTED",
        },
      });
    },
  );
  await open(page);
  await page
    .getByLabel("Ghi chú hoàn tất", { exact: true })
    .fill("Đã dọn sạch");
  await page
    .getByRole("button", { name: "Hoàn tất dọn phòng", exact: true })
    .click();
  await expect(
    page.getByText("Đã hoàn tất dọn phòng, chờ kiểm tra.", { exact: true }),
  ).toBeVisible();
  await expect(page.getByText(/PENDING_INSPECTION ·/)).toBeVisible();
});

test("closure unavailable keeps progress link and sends no mutation", async ({
  page,
}) => {
  await source(page, {
    state: "IN_PROGRESS",
    startCapability: "STATE_CONFLICT",
    reportBlockerCapability: "UNAVAILABLE",
    completeCapability: "UNAVAILABLE",
  });
  const writes: string[] = [];
  page.on("request", (r) => {
    if (r.url().includes("/api/") && r.method() !== "GET")
      writes.push(r.method());
  });
  await open(page);
  await expect(
    page.getByRole("button", { name: "Báo cáo trở ngại", exact: true }),
  ).toBeDisabled();
  await expect(
    page.getByRole("button", { name: "Hoàn tất dọn phòng", exact: true }),
  ).toBeDisabled();
  await expect(
    page.getByRole("link", { name: "Mở tiến độ dọn phòng" }),
  ).toBeVisible();
  expect(writes).toEqual([]);
});

test("provisional Resume requires note and sends explicit confirmation with current ETag", async ({
  page,
}) => {
  await source(page, {
    state: "ON_HOLD",
    startCapability: "BLOCKER_RESOLUTION_CONTRACT_REQUIRED",
    resumeCapability: "PROVISIONAL_V1",
  });
  await page.route("**/api/auth/csrf", (r) =>
    r.fulfill({
      json: { headerName: "X-CSRF-TOKEN", csrfToken: "interception-only" },
    }),
  );
  let writes = 0;
  await page.route("**/api/branches/7/cleaning-tasks/12/start", async (r) => {
    writes++;
    expect(r.request().postDataJSON()).toEqual({
      resolveBlocker: true,
      note: "Guest has left",
    });
    expect(r.request().headers()["if-match"]).toBe(tag);
    await r.fulfill({ status: 412, json: { title: "STALE" } });
  });
  await open(page);
  const resume = page.getByRole("button", {
    name: "Tiếp tục dọn phòng",
    exact: true,
  });
  await expect(resume).toBeDisabled();
  await page.getByLabel("Ghi chú xử lý trở ngại").fill("Guest has left");
  await resume.click();
  await expect(page.getByRole("alert")).toContainText("Công việc đã thay đổi");
  expect(writes).toBe(1);
});
test("Reassign pages through server candidates without posting a command", async ({
  page,
}) => {
  await source(page, { reassignCapability: "PROVISIONAL_NOT_ACCEPTED" });
  const pages: number[] = [],
    writes: string[] = [];
  page.on("request", (r) => {
    if (r.url().includes("/api/") && r.method() !== "GET")
      writes.push(r.method());
  });
  await page.route(
    "**/api/branches/7/cleaning-tasks/12/assignment-options?**",
    (r) => {
      const pageNumber = Number(
        new URL(r.request().url()).searchParams.get("page"),
      );
      pages.push(pageNumber);
      return r.fulfill({
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
              id: pageNumber === 1 ? 6 : 26,
              name: "Page " + pageNumber + " staff",
              departmentId: 2,
              openTasks: 0,
              openTasksInWindow: 0,
            },
          ],
          recordedShifts: [],
          total: 21,
          filters: {
            search: null,
            from: "2026-11-07T00:00:00Z",
            to: "2026-11-08T00:00:00Z",
            page: pageNumber,
            pageSize: 20,
          },
          checklist: null,
          capturedAt: "2026-11-07T00:00:00Z",
          workflowStateVerified: true,
        },
      });
    },
  );
  await open(page);
  await expect(page.getByRole("option", { name: /Page 1 staff/ })).toHaveCount(
    1,
  );
  await page
    .getByRole("button", { name: "Trang sau nhân viên", exact: true })
    .click();
  await expect(page.getByRole("option", { name: /Page 2 staff/ })).toHaveCount(
    1,
  );
  await expect(
    page.getByRole("button", { name: "Trang sau nhân viên", exact: true }),
  ).toBeDisabled();
  expect(pages).toEqual([1, 2]);
  expect(writes).toEqual([]);
});
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
