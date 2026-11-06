import { test, expect } from "@playwright/test";
const options = {
  roomId: 3,
  roomNumber: "API-101",
  branchId: 7,
  cleaningTypes: [{ id: 4, code: "CHECKOUT", name: "Checkout" }],
  priorities: [{ id: 5, code: "NORMAL", name: "Normal" }],
  cleaningItemCount: 2,
};
test("Create loads authorized configuration and posts once with CSRF then opens live detail", async ({
  page,
}) => {
  let posts = 0;
  await page.route("**/api/branches/7/cleaning-task-options?**", (r) =>
    r.fulfill({ json: options }),
  );
  await page.route("**/api/auth/csrf", (r) =>
    r.fulfill({ json: { headerName: "X-CSRF-TOKEN", csrfToken: "fresh" } }),
  );
  await page.route("**/api/branches/7/cleaning-tasks", (r) => {
    posts++;
    expect(r.request().headers()["x-csrf-token"]).toBe("fresh");
    return r.fulfill({
      status: 201,
      json: {
        id: 6,
        branchId: 7,
        roomId: 3,
        taskNumber: "CL-6",
        state: "PENDING_ASSIGNMENT",
      },
    });
  });
  await page.route("**/api/branches/7/cleaning-monitor/cleaning/6?**", (r) =>
    r.fulfill({ status: 503, json: { title: "UNAVAILABLE" } }),
  );
  await page.goto("/cleaning/tasks/new?branchId=7&roomId=3");
  await expect(
    page.getByText("API-101 · Chi nhánh 7", { exact: true }),
  ).toBeVisible();
  await page
    .getByLabel("Hạn UTC (ISO 8601)", { exact: true })
    .fill("2027-01-01T00:00:00Z");
  await page
    .getByRole("button", { name: "Tạo công việc", exact: true })
    .click();
  await expect(page).toHaveURL(/\/cleaning\/tasks\/6\?branchId=7&monitor=1/);
  expect(posts).toBe(1);
});
test("Create conflict keeps form and blocks blind retry", async ({ page }) => {
  let posts = 0;
  await page.route("**/api/branches/7/cleaning-task-options?**", (r) =>
    r.fulfill({ json: options }),
  );
  await page.route("**/api/auth/csrf", (r) =>
    r.fulfill({ json: { headerName: "X-CSRF-TOKEN", csrfToken: "fresh" } }),
  );
  await page.route("**/api/branches/7/cleaning-tasks", (r) => {
    posts++;
    return r.fulfill({ status: 409, json: { title: "DUPLICATE" } });
  });
  await page.goto("/cleaning/tasks/new?branchId=7&roomId=3");
  await page
    .getByLabel("Hạn UTC (ISO 8601)", { exact: true })
    .fill("2027-01-01T00:00:00Z");
  await page
    .getByRole("button", { name: "Tạo công việc", exact: true })
    .click();
  await expect(page.getByRole("alert")).toContainText("đã có công việc");
  await expect(
    page.getByRole("button", { name: "Tạo công việc", exact: true }),
  ).toBeDisabled();
  expect(posts).toBe(1);
});

for (const status of [401, 403, 503])
  test(`Create options ${status} maps server error without enabling post`, async ({
    page,
  }) => {
    let posts = 0;
    await page.route("**/api/branches/7/cleaning-task-options?**", (r) =>
      r.fulfill({ status, json: { title: "UNAVAILABLE" } }),
    );
    page.on("request", (r) => {
      if (r.url().includes("/api/") && r.method() === "POST") posts++;
    });
    await page.goto("/cleaning/tasks/new?branchId=7&roomId=3");
    await expect(page.getByRole("alert")).toBeVisible();
    await expect(page.locator("select[name=cleaningTypeId]")).toHaveCount(0);
    expect(posts).toBe(0);
  });
