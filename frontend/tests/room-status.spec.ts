import { test, expect } from "@playwright/test";
import { board, room, detail, history } from "./room-status-fixtures.mjs";
// Intercepted fixtures test UI behavior only, not live SQL/HTTPS.
test("board filters, paginates and opens room detail/history", async ({
  page,
}) => {
  const urls: URL[] = [];
  await page.route("**/api/rooms?**", (route) => {
    const u = new URL(route.request().url());
    urls.push(u);
    return route.fulfill({
      json: { ...board, page: Number(u.searchParams.get("page")) },
    });
  });
  await page.route("**/api/rooms/11?**", (r) => r.fulfill({ json: detail }));
  await page.route("**/api/rooms/11/history?**", (r) =>
    r.fulfill({ json: history }),
  );
  await page.goto("/rooms/status?branchId=7");
  await expect(
    page.getByRole("link", { name: "A101", exact: true }),
  ).toBeVisible();
  await expect(page.getByLabel("Bộ đếm trạng thái")).toContainText("DIRTY: 21");
  await expect(page.getByRole("note")).toContainText(
    "Chưa có nguồn được xác nhận",
  );
  await expect(
    page.getByRole("button", { name: /cập nhật|check.in|check.out/i }),
  ).toHaveCount(0);
  await page.getByLabel("Tầng (ID)", { exact: true }).fill("2");
  await page.getByRole("button", { name: "Lọc phòng" }).click();
  await expect.poll(() => urls.at(-1)?.searchParams.get("floorId")).toBe("2");
  await page.getByRole("button", { name: "Trang sau", exact: true }).click();
  await expect.poll(() => urls.at(-1)?.searchParams.get("page")).toBe("2");
  await page.getByRole("link", { name: "A101", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Phòng A101", exact: true }),
  ).toBeVisible();
  await expect(page.getByText(/Repair door/)).toBeVisible();
  await expect(
    page.getByRole("region", { name: "Lịch sử trạng thái", exact: true }),
  ).toContainText("Checkout recorded");
});
for (const status of [401, 403, 409, 400, 503])
  test(`board handles ${status} without fallback`, async ({ page }) => {
    await page.route("**/api/rooms?**", (r) =>
      r.fulfill({ status, json: { title: "DENIED" } }),
    );
    await page.goto("/rooms/status?branchId=7");
    await expect(page.getByRole("alert")).toBeVisible();
    await expect(page.getByRole("link", { name: "A101" })).toHaveCount(0);
    await expect(page.getByRole("button", { name: "Thử lại" })).toBeVisible();
  });
test("branch switch ignores canceled stale response", async ({ page }) => {
  let release!: () => void;
  const pending = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route("**/api/rooms?**", async (r) => {
    if (new URL(r.request().url()).searchParams.get("branchId") === "7") {
      await pending;
      await r.fulfill({ json: board }).catch(() => {});
    } else
      await r.fulfill({
        json: {
          ...board,
          items: [{ ...room, branchId: 8, roomNumber: "B201" }],
        },
      });
  });
  await page.goto("/rooms/status?branchId=7");
  await expect(page.getByRole("status")).toBeVisible();
  await page.getByLabel("Chi nhánh (ID)", { exact: true }).fill("8");
  await page.getByRole("button", { name: "Lọc phòng" }).click();
  await expect(page.getByRole("link", { name: "B201" })).toBeVisible();
  release();
  await expect(page.getByRole("link", { name: "A101" })).toHaveCount(0);
});
test("empty response displays no matching rooms", async ({ page }) => {
  await page.route("**/api/rooms?**", (r) =>
    r.fulfill({
      json: { ...board, items: [], totalCount: 0, statusCounts: [] },
    }),
  );
  await page.goto("/rooms/status?branchId=7");
  await expect(
    page.getByText("Không có phòng phù hợp trên trang này."),
  ).toBeVisible();
});

test("denied branch hides previously displayed room data", async ({ page }) => {
  await page.route("**/api/rooms?**", (route) =>
    new URL(route.request().url()).searchParams.get("branchId") === "7"
      ? route.fulfill({ json: board })
      : route.fulfill({ status: 403, json: { title: "PERMISSION_DENIED" } }),
  );
  await page.goto("/rooms/status?branchId=7");
  await expect(
    page.getByRole("link", { name: "A101", exact: true }),
  ).toBeVisible();
  await page.getByLabel("Chi nhánh (ID)", { exact: true }).fill("8");
  await page.getByRole("button", { name: "Lọc phòng" }).click();
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(
    page.getByRole("link", { name: "A101", exact: true }),
  ).toHaveCount(0);
  await expect(page.getByLabel("Bộ đếm trạng thái")).toHaveCount(0);
});

test("detail with another room identity is rejected before rendering", async ({
  page,
}) => {
  await page.route("**/api/rooms/11?**", (r) =>
    r.fulfill({ json: { ...detail, room: { ...room, id: 99 } } }),
  );
  await page.route("**/api/rooms/11/history?**", (r) =>
    r.fulfill({ json: history }),
  );
  await page.goto("/rooms/status?branchId=7&roomId=11");
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(
    page.getByRole("region", { name: "Chi tiết trạng thái phòng" }),
  ).toHaveCount(0);
});

test("returning to the same URL does not reveal cached room data while reauthorizing", async ({
  page,
}) => {
  let leftBoard = false;
  let returning = false;
  let release!: () => void;
  const pending = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route("**/api/rooms?**", async (route) => {
    if (!leftBoard) return route.fulfill({ json: board });
    returning = true;
    await pending;
    await route
      .fulfill({ status: 403, json: { title: "PERMISSION_DENIED" } })
      .catch(() => {});
  });
  await page.route("**/api/rooms/11**", async (route) => {
    leftBoard = true;
    await pending;
    await route
      .fulfill({ status: 403, json: { title: "PERMISSION_DENIED" } })
      .catch(() => {});
  });
  await page.goto("/rooms/status?branchId=7");
  await expect(
    page.getByRole("link", { name: "A101", exact: true }),
  ).toBeVisible();
  await page.getByRole("link", { name: "A101", exact: true }).click();
  await expect(page.getByRole("status")).toBeVisible();
  await page.goBack();
  await expect.poll(() => returning).toBe(true);
  try {
    await expect(
      page.getByRole("link", { name: "A101", exact: true }),
    ).toHaveCount(0);
    await expect(page.getByRole("status")).toBeVisible();
  } finally {
    release();
  }
  await expect(page.getByRole("alert")).toBeVisible();
});

test("explicit retry recovers from unavailable API without retained room data", async ({
  page,
}) => {
  let available = false;
  await page.route("**/api/rooms?**", (route) =>
    available
      ? route.fulfill({ json: board })
      : route.fulfill({
          status: 503,
          json: { title: "ROOM_DIRECTORY_UNAVAILABLE" },
        }),
  );
  await page.goto("/rooms/status?branchId=7");
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(
    page.getByRole("link", { name: "A101", exact: true }),
  ).toHaveCount(0);
  available = true;
  await page.getByRole("button", { name: "Thử lại" }).click();
  await expect(
    page.getByRole("link", { name: "A101", exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("alert")).toHaveCount(0);
});

for (const changedByUserId of [8, null])
  test(`history shows recorded attribution ${changedByUserId} without current-user fallback`, async ({
    page,
  }) => {
    await page.route("**/api/rooms/11?**", (r) => r.fulfill({ json: detail }));
    await page.route("**/api/rooms/11/history?**", (r) =>
      r.fulfill({
        json: { ...history, items: [{ ...history.items[0], changedByUserId }] },
      }),
    );
    await page.goto("/rooms/status?branchId=7&roomId=11");
    const section = page.getByRole("region", {
      name: "Lịch sử trạng thái",
      exact: true,
    });
    await expect(section).toContainText(
      `Người thay đổi (ID): ${changedByUserId ?? "Chưa ghi nhận"}`,
    );
  });
