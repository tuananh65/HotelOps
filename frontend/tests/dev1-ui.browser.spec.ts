import { test, expect, type Page } from "@playwright/test";
const session = {
  user: { id: "7", username: "tester", name: "Tester" },
  expiresAt: "2099-01-01T00:00:00Z",
  absoluteExpiresAt: "2099-01-02T00:00:00Z",
  mustChangePassword: false,
  csrfToken: "csrf-test",
};
const floor = {
  kind: "floor",
  id: 3,
  branchId: 2,
  code: "F3",
  name: "Tầng 3",
  floorNumber: 3,
  description: null,
  floorId: null,
  isActive: true,
};
async function setup(page: Page, writeStatus = 200) {
  const requests: {
    path: string;
    method: string;
    body: unknown;
    match: string | undefined;
    csrf: string | undefined;
  }[] = [];
  await page.route("**/api/**", async (route) => {
    const req = route.request();
    const url = new URL(req.url());
    const path = url.pathname;
    const method = req.method();
    const body = req.postData() ? req.postDataJSON() : null;
    requests.push({
      path,
      method,
      body,
      match: req.headers()["if-match"],
      csrf: req.headers()["x-csrf-token"],
    });
    let data: unknown;
    let status = 200;
    if (path === "/api/auth/session") data = session;
    else if (path === "/api/auth/csrf")
      data = { csrfToken: "csrf-test", headerName: "X-CSRF-TOKEN" };
    else if (path === "/api/auth/logout") data = {};
    else if (path === "/api/branches/2/structure")
      data = {
        branch: { id: 2, code: "B2", name: "Branch 2", isActive: true },
        floors: { items: [floor], page: 1, pageSize: 20, totalCount: 1 },
      };
    else if (path === "/api/branches/2/floors/3") {
      data = method === "PUT" ? { ...floor, ...body } : floor;
      status = method === "PUT" ? writeStatus : 200;
      if (status !== 200) data = { title: "STALE", status };
    } else if (
      path === "/api/rooms" &&
      url.searchParams.get("branchId") === "9"
    ) {
      status = 403;
      data = { title: "FORBIDDEN", status };
    } else if (path === "/api/rooms")
      data = { items: [], page: 1, pageSize: 20, totalCount: 0 };
    else {
      status = 404;
      data = { title: "Unexpected test route", path };
    }
    await route.fulfill({
      status,
      contentType: "application/json",
      headers: { ETag: '"v1"' },
      body: JSON.stringify(data),
    });
  });
  return requests;
}
test("floor screen loads native data and submits cookie CSRF plus ETag", async ({
  page,
}) => {
  const calls = await setup(page);
  await page.goto("/locations?branchId=2");
  await expect(
    page.getByRole("cell", { name: "Tầng 3", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Mở chi tiết" }).click();
  await page.getByRole("button", { name: "Sửa", exact: true }).click();
  await page.getByLabel("Tên *", { exact: true }).fill("Tầng mới");
  await page.getByLabel("Lý do *").fill("Điều chỉnh tên");
  await page.getByRole("button", { name: "Lưu", exact: true }).click();
  await expect
    .poll(() => calls.filter((c) => c.method === "PUT").length)
    .toBe(1);
  const write = calls.find((c) => c.method === "PUT")!;
  expect(write.match).toBe('"v1"');
  expect(write.csrf).toBe("csrf-test");
  expect(write.body).toMatchObject({
    name: "Tầng mới",
    reason: "Điều chỉnh tên",
  });
  expect(calls.some((c) => c.path.includes("/ui/screens/"))).toBe(false);
});
test("stale write cannot be retried without reloading and re-opening form", async ({
  page,
}) => {
  const calls = await setup(page, 412);
  await page.goto("/locations?branchId=2&recordId=3");
  await page.getByRole("button", { name: "Sửa", exact: true }).click();
  await page.getByLabel("Lý do *").fill("Test");
  await page.getByRole("button", { name: "Lưu", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("Dữ liệu đã thay đổi");
  await expect(
    page.getByRole("button", { name: "Lưu", exact: true }),
  ).toHaveCount(0);
  expect(calls.filter((c) => c.method === "PUT")).toHaveLength(1);
});
test("branch change clears data and shows backend 403, no fake empty success", async ({
  page,
}) => {
  await setup(page);
  await page.goto("/rooms?branchId=2");
  await expect(
    page.getByText("Danh sách trống.", { exact: true }),
  ).toBeVisible();
  await page.getByLabel("ID chi nhánh", { exact: true }).fill("9");
  await page.getByRole("button", { name: "Tải dữ liệu", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("không có quyền");
  await expect(page.getByText("Danh sách trống.", { exact: true })).toHaveCount(
    0,
  );
});
test("checklist missing list is explicit, existing auth guard remains active", async ({
  page,
}) => {
  const calls = await setup(page);
  await page.goto("/checklists?branchId=2");
  await expect(
    page.getByText("Backend chưa có API danh sách", { exact: false }),
  ).toBeVisible();
  expect(calls.some((c) => c.path === "/api/inspection-checklists")).toBe(
    false,
  );
  await page.unroute("**/api/**");
  await page.route("**/api/**", (route) =>
    route.fulfill({
      status: 401,
      contentType: "application/json",
      body: '{"title":"Unauthorized"}',
    }),
  );
  await page.goto("/locations?branchId=2");
  await expect(page).toHaveURL(/\/login/);
});
