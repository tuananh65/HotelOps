import { test, expect } from "@playwright/test";
// Intercepted HTTP only; not live API/SQL acceptance.
const room = {
  id: 12,
  branchId: 7,
  roomNumber: "API-101",
  floor: { id: 3, code: "F1", name: "API Floor", number: 1 },
  area: null,
  roomType: { id: 8, code: "STD", name: "API Standard" },
  operationalStatusCode: "DIRTY",
  isActive: true,
};
test("board loads API DTO, applies filters and paginates", async ({ page }) => {
  const requests: URL[] = [];
  await page.route("**/api/rooms?**", async (route) => {
    const url = new URL(route.request().url());
    requests.push(url);
    await route.fulfill({
      json: {
        items: [room],
        page: Number(url.searchParams.get("page")),
        pageSize: 20,
        totalCount: 21,
      },
    });
  });
  await page.goto("/room-readiness", { waitUntil: "domcontentloaded" });
  await page.getByLabel("Chi nhánh (ID)").fill("7");
  await page.getByRole("button", { name: "Tải dữ liệu" }).click();
  await expect(page.getByText("API-101", { exact: true })).toBeVisible();
  await expect(page.getByText("API Standard", { exact: true })).toBeVisible();
  await page
    .getByLabel("Trạng thái phòng", { exact: true })
    .selectOption("DIRTY");
  await page.getByLabel("Số phòng", { exact: true }).fill("101");
  await page.getByRole("button", { name: "Tải dữ liệu" }).click();
  await expect
    .poll(() => requests.at(-1)?.searchParams.get("operationalStatusCode"))
    .toBe("DIRTY");
  await page.getByRole("button", { name: "Trang sau", exact: true }).click();
  await expect.poll(() => requests.at(-1)?.searchParams.get("page")).toBe("2");
  expect(requests.at(-1)?.searchParams.get("branchId")).toBe("7");
  expect(requests.at(-1)?.searchParams.get("search")).toBe("101");
});
for (const status of [401, 403, 503]) {
  test(`board shows ${status} without prototype fallback`, async ({ page }) => {
    await page.route("**/api/rooms?**", (route) =>
      route.fulfill({ status, json: { title: "DENIED" } }),
    );
    await page.goto("/room-readiness?branchId=7", {
      waitUntil: "domcontentloaded",
    });
    await expect(page.getByRole("alert")).toBeVisible();
    await expect(page.locator("article.room-tile")).toHaveCount(0);
    await expect(page.getByRole("alert")).toContainText(
      status === 401
        ? "Phiên đăng nhập"
        : status === 403
          ? "ROOM_VIEW"
          : "không khả dụng",
    );
  });
}
test("board clears old rooms on failed reload and renders empty result", async ({
  page,
}) => {
  let phase = 1;
  await page.route("**/api/rooms?**", (route) =>
    phase === 2
      ? route.fulfill({ status: 403, json: { title: "PERMISSION_DENIED" } })
      : route.fulfill({
          json: {
            items: phase === 1 ? [room] : [],
            page: 1,
            pageSize: 20,
            totalCount: phase === 1 ? 1 : 0,
          },
        }),
  );
  await page.goto("/room-readiness?branchId=7", {
    waitUntil: "domcontentloaded",
  });
  await expect(page.locator("article.room-tile")).toHaveCount(1);
  phase = 2;
  await page.getByRole("button", { name: "Tải dữ liệu" }).click();
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(page.locator("article.room-tile")).toHaveCount(0);
  phase = 3;
  await page.getByRole("button", { name: "Thử lại" }).click();
  await expect(
    page.getByText("Không có phòng phù hợp bộ lọc.", { exact: true }),
  ).toBeVisible();
});
