import { test, expect } from "@playwright/test";
import { build } from "esbuild";
import { board, room, detail, history } from "./room-status-fixtures.mjs";
// OFFLINE: the app bundle and all API responses are intercepted in memory.
// Run without a webServer: playwright test --config tests room-status.spec.ts.
test.use({
  baseURL: "http://fe03.offline.invalid",
  channel: "msedge",
  serviceWorkers: "block",
});
let app: string;
const mutations = new WeakMap<import("@playwright/test").Page, string[]>();
test.beforeAll(async () => {
  const bundle = await build({
    entryPoints: ["src/main.tsx"],
    bundle: true,
    write: false,
    platform: "browser",
    format: "iife",
    loader: { ".css": "empty" },
    define: { "import.meta.env": "{}", "process.env.NODE_ENV": '"test"' },
  });
  app = bundle.outputFiles[0].text;
});
// Every options request stays isolated; individual action tests override this default.
test.beforeEach(async ({ page }) => {
  const writes: string[] = [];
  mutations.set(page, writes);
  page.on("request", (request) => {
    if (
      new URL(request.url()).pathname.startsWith("/api/") &&
      request.method() !== "GET"
    )
      writes.push(`${request.method()} ${request.url()}`);
  });
  await page.route("**/*", (route) => {
    const url = new URL(route.request().url());
    if (url.origin !== "http://fe03.offline.invalid") return route.abort();
    if (url.pathname === "/offline-app.js")
      return route.fulfill({
        contentType: "application/javascript",
        body: app,
      });
    if (url.pathname.startsWith("/api/"))
      return route.fulfill({
        status: 503,
        json: { title: "OFFLINE_UNMOCKED_REQUEST" },
      });
    if (route.request().isNavigationRequest())
      return route.fulfill({
        contentType: "text/html",
        body: '<div id="root"></div><script src="/offline-app.js"></script>',
      });
    return route.abort();
  });
  await page.route("**/api/branches/**/cleaning-task-options?**", (route) =>
    route.fulfill({ status: 403, json: { title: "PERMISSION_DENIED" } }),
  );
});
test.afterEach(async ({ page }) => {
  expect(mutations.get(page)).toEqual([]);
  expect(
    await page.evaluate(() =>
      performance
        .getEntriesByType("resource")
        .filter((entry) => entry.name.includes("/api/incidents")),
    ),
  ).toEqual([]);
});

for (const query of [
  "branchId=7&branchId=7",
  "branchId=7&branchId=8&roomId=11",
  "branchId=7&roomId=11&roomId=11",
  "branchId=7&roomId=11&roomId=12",
  "branchId=7&%72oomId=11&roomId=11",
  "roomId=11",
  ...[
    "",
    "0",
    "-1",
    "01",
    "1.0",
    "1e2",
    "9007199254740992",
    "%201",
    "%0A1",
    "1%26roomId%3D99",
  ].flatMap((value) => [
    `branchId=${value}&roomId=11`,
    `branchId=7&roomId=${value}`,
  ]),
])
  test(`invalid explicit context performs no API read: ${query}`, async ({
    page,
  }) => {
    const calls: string[] = [];
    page.on("request", (request) => {
      if (new URL(request.url()).pathname.startsWith("/api/"))
        calls.push(request.url());
    });
    await page.goto(`/rooms/status?${query}`);
    await expect(page.getByRole("alert")).toBeVisible();
    await expect(
      page.getByRole("region", { name: "Hành động tiếp theo", exact: true }),
    ).toHaveCount(0);
    await expect(page.getByLabel("Bộ đếm trạng thái")).toHaveCount(0);
    expect(calls).toEqual([]);
    expect(new URL(page.url()).search).toBe(`?${query}`);
  });

test("missing optional context waits for explicit branch selection", async ({
  page,
}) => {
  const calls: string[] = [];
  page.on("request", (request) => {
    if (new URL(request.url()).pathname.startsWith("/api/"))
      calls.push(request.url());
  });
  await page.goto("/rooms/status");
  await expect(
    page.getByText("Chọn chi nhánh để tải các phòng được phép xem."),
  ).toBeVisible();
  await expect(page.getByRole("alert")).toHaveCount(0);
  expect(calls).toEqual([]);
});

test("detail scope switch and Back wait for fresh authoritative detail", async ({
  page,
}) => {
  let detailCalls = 0;
  let holdDetail = false;
  let release!: () => void;
  const pending = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route("**/api/rooms/11?**", async (route) => {
    detailCalls++;
    if (holdDetail) await pending;
    await route.fulfill({ json: detail }).catch(() => {});
  });
  await page.route("**/api/rooms/11/history?**", (route) =>
    route.fulfill({ json: history }),
  );
  await page.route("**/api/rooms?**", (route) =>
    route.fulfill({ status: 403, json: {} }),
  );
  let eligibilityCalls = 0;
  await page.route(
    "**/api/branches/7/cleaning-task-options?roomId=11",
    (route) => {
      eligibilityCalls++;
      return route.fulfill({ json: actionOptions });
    },
  );
  await page.goto("/rooms/status?branchId=7&roomId=11");
  const link = page.getByRole("link", {
    name: "Tạo công việc vệ sinh",
    exact: true,
  });
  await expect(link).toHaveAttribute(
    "href",
    "/cleaning/tasks/new?branchId=7&roomId=11",
  );
  const initialDetails = detailCalls;
  const initialEligibility = eligibilityCalls;
  holdDetail = true;
  await page.getByLabel("Chi nhánh (ID)", { exact: true }).fill("8");
  await page.getByRole("button", { name: "Lọc phòng" }).click();
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(link).toHaveCount(0);
  await page.goBack();
  try {
    await expect.poll(() => detailCalls).toBeGreaterThan(initialDetails);
    await expect(link).toHaveCount(0);
    await expect(
      page.getByRole("region", { name: "Hành động tiếp theo", exact: true }),
    ).toHaveCount(0);
    expect(eligibilityCalls).toBe(initialEligibility);
  } finally {
    release();
  }
  await expect(link).toHaveAttribute(
    "href",
    "/cleaning/tasks/new?branchId=7&roomId=11",
  );
  await expect.poll(() => eligibilityCalls).toBeGreaterThan(initialEligibility);
  await expect(
    page.getByRole("button", { name: "Phân công kiểm tra phòng", exact: true }),
  ).toBeDisabled();
  await expect(
    page.getByRole("button", { name: "Báo cáo sự cố", exact: true }),
  ).toBeDisabled();
});
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

const actionOptions = {
  roomId: 11,
  branchId: 7,
  roomNumber: "A101",
  cleaningTypes: [],
  priorities: [],
  cleaningItemCount: 0,
};
async function actionRoom(page: import("@playwright/test").Page) {
  await page.route("**/api/rooms/11?**", (r) => r.fulfill({ json: detail }));
  await page.route("**/api/rooms/11/history?**", (r) =>
    r.fulfill({ json: history }),
  );
}
test("next action passes room context to registered UC028 without mutations", async ({
  page,
}) => {
  await actionRoom(page);
  const writes: string[] = [];
  page.on("request", (r) => {
    if (r.url().includes("/api/") && r.method() !== "GET")
      writes.push(r.method() + " " + r.url());
  });
  await page.route("**/api/branches/7/cleaning-task-options?roomId=11", (r) =>
    r.fulfill({ json: actionOptions }),
  );
  await page.goto("/rooms/status?branchId=7&roomId=11");
  await expect(
    page.getByRole("button", { name: "Phân công kiểm tra phòng", exact: true }),
  ).toBeDisabled();
  await expect(
    page.getByRole("button", { name: "Báo cáo sự cố", exact: true }),
  ).toBeDisabled();
  const link = page.getByRole("link", {
    name: "Tạo công việc vệ sinh",
    exact: true,
  });
  await expect(link).toHaveAttribute(
    "href",
    "/cleaning/tasks/new?branchId=7&roomId=11",
  );
  await link.click();
  await expect(page).toHaveURL(/\/cleaning\/tasks\/new\?branchId=7&roomId=11$/);
  await expect(page.getByText("Page not found", { exact: true })).toHaveCount(
    0,
  );
  await expect(
    page.getByRole("button", { name: "Tạo công việc", exact: true }),
  ).toBeVisible();
  expect(writes).toEqual([]);
});
for (const value of [403, 503, "wrongBranch"])
  test(`next action unavailable for ${value}, history preserved`, async ({
    page,
  }) => {
    await actionRoom(page);
    await page.route(
      "**/api/branches/7/cleaning-task-options?roomId=11",
      (r) =>
        typeof value === "number"
          ? r.fulfill({ status: value, json: {} })
          : r.fulfill({ json: { ...actionOptions, branchId: 8 } }),
    );
    await page.goto("/rooms/status?branchId=7&roomId=11");
    await expect(
      page.getByRole("button", { name: "Kiểm tra lại hành động" }),
    ).toBeVisible();
    await expect(
      page.getByRole("link", { name: "Tạo công việc vệ sinh", exact: true }),
    ).toHaveCount(0);
    await expect(
      page.getByRole("region", { name: "Lịch sử trạng thái", exact: true }),
    ).toContainText("Checkout recorded");
  });
test("cross-branch detail never queries workflow eligibility", async ({
  page,
}) => {
  let queries = 0;
  await page.route("**/api/rooms/11?**", (r) =>
    r.fulfill({ json: { ...detail, room: { ...room, branchId: 8 } } }),
  );
  await page.route("**/api/rooms/11/history?**", (r) =>
    r.fulfill({ json: history }),
  );
  await page.route("**/api/branches/**/cleaning-task-options?**", (r) => {
    queries++;
    return r.fulfill({ json: actionOptions });
  });
  await page.goto("/rooms/status?branchId=7&roomId=11");
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(
    page.getByRole("region", { name: "Hành động tiếp theo", exact: true }),
  ).toHaveCount(0);
  expect(queries).toBe(0);
});
test("scope switch and Back discard canceled eligibility and reauthorize", async ({
  page,
}) => {
  await actionRoom(page);
  let switched = false;
  await page.route("**/api/rooms?**", (r) => {
    switched = true;
    return r.fulfill({ status: 403, json: {} });
  });
  let calls = 0;
  let release!: () => void;
  const pending = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route(
    "**/api/branches/7/cleaning-task-options?roomId=11",
    async (r) => {
      calls++;
      if (!switched) {
        await pending;
        await r.fulfill({ json: actionOptions }).catch(() => {});
      } else await r.fulfill({ status: 403, json: {} });
    },
  );
  await page.goto("/rooms/status?branchId=7&roomId=11");
  await expect.poll(() => calls).toBeGreaterThan(0);
  await page.getByLabel("Chi nhánh (ID)", { exact: true }).fill("8");
  await page.getByRole("button", { name: "Lọc phòng" }).click();
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(
    page.getByRole("region", { name: "Hành động tiếp theo", exact: true }),
  ).toHaveCount(0);
  await page.goBack();
  try {
    await expect(
      page.getByRole("button", { name: "Kiểm tra lại hành động" }),
    ).toBeVisible();
  } finally {
    release();
  }
  await expect(
    page.getByRole("link", { name: "Tạo công việc vệ sinh", exact: true }),
  ).toHaveCount(0);
});
