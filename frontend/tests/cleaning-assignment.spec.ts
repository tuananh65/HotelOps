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
test("create without authorized room configuration never posts", async ({
  page,
}) => {
  const mutations: string[] = [];
  page.on("request", (r) => {
    if (r.url().includes("/api/") && r.method() !== "GET")
      mutations.push(r.method());
  });
  await page.goto("/cleaning/tasks/new?branchId=7");
  await expect(page.locator('input[name="roomId"]')).toBeVisible();
  await expect(page.locator('select[name="cleaningTypeId"]')).toHaveCount(0);
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

test("candidate Save uses exact task ETag and displays pending delivery", async ({
  page,
}) => {
  let posts = 0;
  const tag = '"0000000000000001"';
  await page.route(
    "**/api/branches/7/cleaning-tasks/12/assignment-options?**",
    (r) =>
      r.fulfill({
        headers: { ETag: tag },
        json: { ...plan, assignmentCapability: "PROVISIONAL_NOT_ACCEPTED" },
      }),
  );
  await page.route("**/api/auth/csrf", (r) =>
    r.fulfill({ json: { headerName: "X-CSRF-TOKEN", csrfToken: "fresh" } }),
  );
  await page.route("**/api/branches/7/cleaning-tasks/12/assign", (r) => {
    posts++;
    const req = r.request(),
      d = req.postDataJSON();
    expect(req.headers()["if-match"]).toBe(tag);
    return r.fulfill({
      headers: { ETag: '"0000000000000002"' },
      json: {
        id: 12,
        branchId: 7,
        assigneeUserId: d.assigneeId,
        assignmentId: 9,
        operationId: req.headers()["idempotency-key"],
        publicationId: "76c44400-7ca4-4da1-8990-8d2fc258f211",
        workflowState: "ASSIGNED",
        notification: "PENDING_DELIVERY",
        sourceETag: '"0000000000000002"',
        contract: "PROVISIONAL_NOT_ACCEPTED",
      },
    });
  });
  await page.goto("/cleaning/assignment?branchId=7&taskId=12");
  await page.locator("select[name=assigneeId]").selectOption("5");
  await page.locator("input[name=dueAt]").fill("2027-01-01T00:00:00Z");
  await page.locator("input[name=reason]").fill("candidate test");
  await page.locator("form button[type=button]").click();
  await expect(
    page.getByText("Assignment 9 committed. Notification pending delivery."),
  ).toBeVisible();
  expect(posts).toBe(1);
  await expect(page.locator("form button[type=button]")).toBeDisabled();
});

test("candidate explicit retry preserves original command and key with fresh CSRF", async ({
  page,
}) => {
  let posts = 0,
    csrf = 0;
  const commands: string[] = [],
    keys: string[] = [];
  await page.route(
    "**/api/branches/7/cleaning-tasks/12/assignment-options?**",
    (r) =>
      r.fulfill({
        headers: { ETag: '"0000000000000001"' },
        json: { ...plan, assignmentCapability: "PROVISIONAL_NOT_ACCEPTED" },
      }),
  );
  await page.route("**/api/auth/csrf", (r) =>
    r.fulfill({
      json: { headerName: "X-CSRF-TOKEN", csrfToken: `fresh-${++csrf}` },
    }),
  );
  await page.route("**/api/branches/7/cleaning-tasks/12/assign", (r) => {
    posts++;
    commands.push(r.request().postData()!);
    keys.push(r.request().headers()["idempotency-key"]);
    return r.fulfill({ status: 503, json: { title: "UNAVAILABLE" } });
  });
  await page.goto("/cleaning/assignment?branchId=7&taskId=12");
  await page.locator("select[name=assigneeId]").selectOption("5");
  await page.locator("input[name=dueAt]").fill("2027-01-01T00:00:00Z");
  await page.locator("input[name=reason]").fill("candidate test");
  await page.locator("form button[type=button]").click();
  await expect(
    page.getByRole("button", { name: "Retry same command" }),
  ).toBeVisible();
  expect(posts).toBe(1);
  await page.getByRole("button", { name: "Retry same command" }).click();
  await expect.poll(() => posts).toBe(2);
  expect(commands[0]).toBe(commands[1]);
  expect(keys[0]).toBe(keys[1]);
  expect(csrf).toBe(2);
});

test("assignment result from a previous branch cannot replace the current task state", async ({
  page,
}) => {
  let release!: () => void;
  const gate = new Promise<void>((r) => (release = r));
  let began!: () => void;
  const started = new Promise<void>((r) => (began = r));
  const tag = '"0000000000000001"';
  await page.route(
    "**/api/branches/7/cleaning-tasks/12/assignment-options?**",
    (r) =>
      r.fulfill({
        headers: { ETag: tag },
        json: { ...plan, assignmentCapability: "PROVISIONAL_NOT_ACCEPTED" },
      }),
  );
  await page.route(
    "**/api/branches/8/cleaning-tasks/13/assignment-options?**",
    (r) =>
      r.fulfill({
        json: {
          ...plan,
          task: {
            ...plan.task,
            id: 13,
            branchId: 8,
            roomId: 4,
            roomNumber: "201",
            number: "API-OTHER",
          },
        },
      }),
  );
  await page.route("**/api/auth/csrf", (r) =>
    r.fulfill({ json: { headerName: "X-CSRF-TOKEN", csrfToken: "fresh" } }),
  );
  await page.route("**/api/branches/7/cleaning-tasks/12/assign", async (r) => {
    began();
    await gate;
    const req = r.request();
    await r.fulfill({
      headers: { ETag: '"0000000000000002"' },
      json: {
        id: 12,
        branchId: 7,
        assigneeUserId: 5,
        assignmentId: 9,
        operationId: req.headers()["idempotency-key"],
        publicationId: "76c44400-7ca4-4da1-8990-8d2fc258f211",
        workflowState: "ASSIGNED",
        notification: "PENDING_DELIVERY",
        sourceETag: '"0000000000000002"',
        contract: "PROVISIONAL_NOT_ACCEPTED",
      },
    });
  });
  await page.goto("/cleaning/assignment?branchId=7&taskId=12");
  await page.locator("select[name=assigneeId]").selectOption("5");
  await page.locator("input[name=dueAt]").fill("2027-01-01T00:00:00Z");
  await page.locator("input[name=reason]").fill("scope test");
  await page.locator("form button[type=button]").click();
  await started;
  await page.locator("input[name=branchId]").fill("8");
  await page.locator("input[name=taskId]").fill("13");
  await page.locator("form.filter-bar button").click();
  await expect(
    page.getByRole("heading", { name: "API-OTHER · 201" }),
  ).toBeVisible();
  const response = page.waitForResponse((r) =>
    r.url().endsWith("/cleaning-tasks/12/assign"),
  );
  release();
  await response;
  await page.evaluate(
    () =>
      new Promise<void>((resolve) =>
        requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
      ),
  );
  await expect(
    page.getByText("Assignment 9 committed. Notification pending delivery."),
  ).toHaveCount(0);
  await expect(
    page.getByRole("heading", { name: "API-OTHER · 201" }),
  ).toBeVisible();
});
