import { test, expect } from "@playwright/test";
// API-contract fixtures only. Never connects to SQL or issues a real authenticated write.
const initial = {
  id: 42,
  branchId: 7,
  kind: "cleaning",
  state: "IN_PROGRESS",
  percent: 20,
  items: [
    {
      id: 9,
      label: "Clean floor",
      required: true,
      order: 0,
      done: false,
      outcome: null,
      note: null,
    },
  ],
  timeline: [],
  evidence: [],
};
const headers = { ETag: '"0000000000000001"' };
test("UC032 UI reads, saves with CSRF/ETag, refreshes; 100 remains IN_PROGRESS", async ({
  page,
}) => {
  let data = initial;
  let puts = 0;
  let readsAfterSave = 0;
  await page.route("**/api/auth/csrf", (route) =>
    route.fulfill({
      json: { csrfToken: "browser-fixture-only", headerName: "X-CSRF-TOKEN" },
    }),
  );
  await page.route(
    "**/api/branches/7/cleaning-tasks/42/progress",
    async (route) => {
      if (route.request().method() === "PUT") {
        puts++;
        expect(route.request().headers()["if-match"]).toBe(headers.ETag);
        expect(route.request().headers()["x-csrf-token"]).toBe(
          "browser-fixture-only",
        );
        const input = route.request().postDataJSON();
        expect(input.percent).toBe(100);
        expect(input.items[0].done).toBe(true);
        expect(input).not.toHaveProperty("state");
        expect(input).not.toHaveProperty("actorId");
        data = {
          ...initial,
          percent: input.percent,
          items: [{ ...initial.items[0], done: true }],
        };
      } else if (puts) readsAfterSave++;
      await route.fulfill({
        json: data,
        headers: puts ? { ETag: '"0000000000000002"' } : headers,
      });
    },
  );
  await page.goto("/cleaning/tasks/42?branchId=7", {
    waitUntil: "domcontentloaded",
  });
  await expect(page.getByLabel("Tiến độ (%)")).toHaveValue("20");
  await page.getByLabel("Tiến độ (%)").fill("100");
  await page.getByRole("checkbox", { name: "Clean floor (bắt buộc)" }).check();
  await page.getByLabel("Ghi chú tiến độ", { exact: true }).fill("Cleaned");
  await page.getByRole("button", { name: "Lưu tiến độ", exact: true }).click();
  await expect(page.getByText(/Đã lưu tiến độ/)).toBeVisible();
  await expect.poll(() => readsAfterSave).toBeGreaterThan(0);
  expect(puts).toBe(1);
  await expect(page.getByLabel("Tiến độ (%)")).toHaveValue("100");
});
test("UC032 412 preserves draft and blocks retries until explicit reload", async ({
  page,
}) => {
  let puts = 0;
  await page.route("**/api/auth/csrf", (route) =>
    route.fulfill({
      json: { csrfToken: "fixture", headerName: "X-CSRF-TOKEN" },
    }),
  );
  await page.route("**/api/branches/7/cleaning-tasks/42/progress", (route) =>
    route.request().method() === "PUT"
      ? (puts++, route.fulfill({ status: 412, json: { title: "STALE" } }))
      : route.fulfill({ json: initial, headers }),
  );
  await page.goto("/cleaning/tasks/42?branchId=7", {
    waitUntil: "domcontentloaded",
  });
  await page.getByLabel("Tiến độ (%)").fill("80");
  await page.getByRole("button", { name: "Lưu tiến độ", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("Dữ liệu đã thay đổi");
  await expect(page.getByLabel("Tiến độ (%)")).toHaveValue("80");
  await expect(
    page.getByRole("button", { name: "Lưu tiến độ", exact: true }),
  ).toBeDisabled();
  expect(puts).toBe(1);
  await page
    .getByRole("button", { name: "Tải lại (bỏ bản sửa)", exact: true })
    .click();
  await expect(page.getByLabel("Tiến độ (%)")).toHaveValue("20");
  await expect(
    page.getByRole("button", { name: "Lưu tiến độ", exact: true }),
  ).toBeEnabled();
});
for (const status of [400, 403, 409, 428, 503]) {
  test(`UC032 PUT ${status} has error state, no automatic retry`, async ({
    page,
  }) => {
    let puts = 0;
    await page.route("**/api/auth/csrf", (route) =>
      route.fulfill({
        json: { csrfToken: "fixture", headerName: "X-CSRF-TOKEN" },
      }),
    );
    await page.route("**/api/branches/7/cleaning-tasks/42/progress", (route) =>
      route.request().method() === "PUT"
        ? (puts++, route.fulfill({ status, json: { title: "TEST_ERROR" } }))
        : route.fulfill({ json: initial, headers }),
    );
    await page.goto("/cleaning/tasks/42?branchId=7", {
      waitUntil: "domcontentloaded",
    });
    await page
      .getByRole("button", { name: "Lưu tiến độ", exact: true })
      .click();
    await expect(page.getByRole("alert")).toBeVisible();
    expect(puts).toBe(1);
    if (status === 400)
      await expect(
        page.getByRole("button", { name: "Lưu tiến độ", exact: true }),
      ).toBeEnabled();
    else
      await expect(
        page.getByRole("button", { name: "Lưu tiến độ", exact: true }),
      ).toBeDisabled();
  });
}
test("UC032 wrong-branch DTO is rejected without prototype fallback", async ({
  page,
}) => {
  await page.route("**/api/branches/7/cleaning-tasks/42/progress", (route) =>
    route.fulfill({ json: { ...initial, branchId: 8 }, headers }),
  );
  await page.goto("/cleaning/tasks/42?branchId=7", {
    waitUntil: "domcontentloaded",
  });
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Lưu tiến độ", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByText("Dữ liệu minh họa.", { exact: false }),
  ).toHaveCount(0);
});
test("UC032 saved write followed by failed GET is not presented as a failed write", async ({
  page,
}) => {
  let puts = 0;
  await page.route("**/api/auth/csrf", (route) =>
    route.fulfill({
      json: { csrfToken: "fixture", headerName: "X-CSRF-TOKEN" },
    }),
  );
  await page.route("**/api/branches/7/cleaning-tasks/42/progress", (route) => {
    if (route.request().method() === "PUT") {
      puts++;
      return route.fulfill({ json: initial, headers });
    }
    return puts
      ? route.fulfill({ status: 503, json: { title: "UNAVAILABLE" } })
      : route.fulfill({ json: initial, headers });
  });
  await page.goto("/cleaning/tasks/42?branchId=7", {
    waitUntil: "domcontentloaded",
  });
  await page.getByRole("button", { name: "Lưu tiến độ", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("Đã lưu thành công");
  await expect(
    page.getByRole("button", { name: "Lưu tiến độ", exact: true }),
  ).toBeDisabled();
  expect(puts).toBe(1);
});

test("UC032 loading and empty items use API state only", async ({ page }) => {
  let release!: () => void;
  const ready = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route(
    "**/api/branches/7/cleaning-tasks/42/progress",
    async (route) => {
      await ready;
      await route.fulfill({ json: { ...initial, items: [] }, headers });
    },
  );
  await page.goto("/cleaning/tasks/42?branchId=7", {
    waitUntil: "domcontentloaded",
  });
  await expect(
    page.getByText("Đang tải tiến độ…", { exact: true }),
  ).toBeVisible();
  release();
  await expect(
    page.getByText("Chưa có hạng mục vệ sinh.", { exact: true }),
  ).toBeVisible();
});
test("UC032 anonymous response has no editable form", async ({ page }) => {
  await page.route("**/api/branches/7/cleaning-tasks/42/progress", (route) =>
    route.fulfill({ status: 401, json: { title: "SESSION_INVALID" } }),
  );
  await page.goto("/cleaning/tasks/42?branchId=7", {
    waitUntil: "domcontentloaded",
  });
  await expect(page.getByRole("alert")).toContainText("Phiên đăng nhập");
  await expect(
    page.getByRole("button", { name: "Lưu tiến độ", exact: true }),
  ).toHaveCount(0);
});
test("room board ignores a canceled stale branch request", async ({ page }) => {
  let release!: () => void;
  const delayed = new Promise<void>((resolve) => {
    release = resolve;
  });
  const room = {
    id: 12,
    branchId: 8,
    roomNumber: "CURRENT-BRANCH",
    floor: { id: 3, code: "F", name: "Current floor", number: 1 },
    area: null,
    roomType: { id: 9, code: "STD", name: "Standard" },
    operationalStatusCode: "DIRTY",
    isActive: true,
  };
  await page.route("**/api/rooms?**", async (route) => {
    const branch = new URL(route.request().url()).searchParams.get("branchId");
    if (branch === "7") await delayed;
    await route
      .fulfill({
        json: {
          items: [
            {
              ...room,
              branchId: Number(branch),
              roomNumber: branch === "7" ? "STALE-BRANCH" : room.roomNumber,
            },
          ],
          page: 1,
          pageSize: 20,
          totalCount: 1,
        },
      })
      .catch(() => {});
  });
  await page.goto("/room-readiness?branchId=7", {
    waitUntil: "domcontentloaded",
  });
  await expect(
    page.getByText("Đang tải phòng…", { exact: true }),
  ).toBeVisible();
  await page.getByLabel("Chi nhánh (ID)").fill("8");
  await page.getByRole("button", { name: "Tải dữ liệu", exact: true }).click();
  await expect(page.getByText("CURRENT-BRANCH", { exact: true })).toBeVisible();
  release();
  await expect(page.getByText("STALE-BRANCH", { exact: true })).toHaveCount(0);
});
