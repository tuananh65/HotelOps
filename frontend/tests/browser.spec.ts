import { test, expect, type Page } from "@playwright/test";
import {
  catalog,
  fixtureResponse,
  collections,
  ids,
  validSession,
} from "./fixtures/api-fixture.mjs";
async function api(
  page: Page,
  override?: (path: string, method: string, body: unknown) => unknown,
) {
  await page.route(
    (url) => url.pathname.startsWith("/api/"),
    async (route) => {
      const req = route.request();
      const path = new URL(req.url()).pathname;
      const body = req.postDataJSON() ?? {};
      const special = override?.(path, req.method(), body);
      if (special === false) {
        await route.fulfill({ status: 401 });
        return;
      }
      const value = special ?? fixtureResponse(req.url(), req.method(), body);
      await route.fulfill(
        value === undefined
          ? { status: 204 }
          : {
              status: 200,
              contentType: "application/json",
              body: JSON.stringify(value),
            },
      );
    },
  );
}
const screenKeys: Record<number, string> = {
  3: "users",
  4: "users",
  5: "rooms",
  12: "roles",
  23: "rooms",
  25: "cleaning",
  27: "cleaning",
  30: "cleaning",
  34: "inspections",
  36: "inspections",
  37: "inspections",
  38: "inspections",
  40: "incidents",
  42: "incidents",
  50: "assets",
  53: "items",
  61: "transactions",
  68: "tasks",
  69: "tasks",
  70: "tasks",
  73: "workdays",
  78: "tasks",
};
const inferKey = (screen: { id: number; route: string }) =>
  screenKeys[screen.id] ??
  Object.keys(collections).find((k) => {
    const roots: Record<string, string> = {
      roomTypes: "/room-types",
      cleaning: "/cleaning/tasks",
      rework: "/rework",
      checklists: "/checklists",
      inspections: "/inspections",
      schedules: "/maintenance/schedules",
      reference: "/reference-data",
      warehouses: "/inventory/warehouses",
      items: "/inventory/items",
      units: "/inventory/units",
      transactions: "/inventory/transactions",
      stocktakes: "/inventory/stocktakes",
      materials: "/material-requests",
      replenishments: "/replenishments",
      handovers: "/handovers",
      requests: "/internal-requests",
      maintenance: "/maintenance/tasks",
      audit: "/audit-logs",
    };
    return (
      screen.route === (roots[k] ?? "/" + k) ||
      screen.route.startsWith((roots[k] ?? "/" + k) + "/")
    );
  }) ??
  "users";
test("all 80 route components render using HTTP fixtures with the proposed contract", async ({
  page,
}) => {
  test.setTimeout(180000);
  await api(page);
  const errors: string[] = [];
  page.on("pageerror", (e) => errors.push(e.message));
  for (const screen of catalog.screens) {
    const url = screen.route.replace(":id", ids[inferKey(screen)] ?? "select");
    await page.goto(url);
    await expect(
      page.locator(screen.id <= 3 ? ".auth-panel h1" : ".page-header h1"),
    ).toHaveText(screen.name);
    await expect(
      page.getByRole("heading", {
        name: "This screen could not load",
        exact: true,
      }),
    ).toHaveCount(0);
    await expect(
      page.getByRole("heading", { name: "Record unavailable", exact: true }),
    ).toHaveCount(0);
  }
  expect(errors).toEqual([]);
  expect(
    await page.evaluate(() =>
      localStorage.getItem("hotelops-public-prototype-v1"),
    ),
  ).toBeNull();
});
test("protected routes redirect to login and never accept the old prototype browser database", async ({
  page,
}) => {
  await api(page, (path) => (path === "/api/auth/session" ? false : undefined));
  await page.addInitScript(() =>
    localStorage.setItem(
      "hotelops-public-prototype-v1",
      JSON.stringify({
        users: [{ name: "Fake administrator" }],
        rooms: [{ name: "Fake room" }],
      }),
    ),
  );
  await page.goto("/rooms");
  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByLabel("Username or email")).toHaveValue("");
  await expect(page.getByLabel(/^Password/)).toHaveValue("");
  await expect(page.getByText("Fake room")).toHaveCount(0);
});
test("read-only account cannot open administration routes", async ({
  page,
}) => {
  await api(page, (path) =>
    path === "/api/auth/session"
      ? {
          ...validSession,
          principal: { ...validSession.principal, grants: [] },
        }
      : undefined,
  );
  await page.goto("/users");
  await expect(
    page.getByRole("heading", {
      name: "You do not have permission to perform this action.",
      exact: true,
    }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Create user", exact: true }),
  ).toHaveCount(0);
});
test("a failed API read shows an error and retry without mock fallback", async ({
  page,
}) => {
  await api(page);
  await page.route("**/api/rooms?**", (route) =>
    route.fulfill({
      status: 500,
      contentType: "application/problem+json",
      body: JSON.stringify({ title: "API unavailable" }),
    }),
  );
  await page.goto("/rooms");
  await expect(
    page.getByText("API unavailable", { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Retry", exact: true }),
  ).toBeVisible();
  await expect(page.locator(".entity-table tbody tr")).toHaveCount(0);
});
async function openRoomForm(page: Page) {
  await page.goto("/rooms");
  await page.getByRole("button", { name: "Create room", exact: true }).click();
  await page.getByLabel("Room number").fill("202");
  await page
    .locator("dialog")
    .getByLabel(/^Floor/)
    .selectOption("floor1");
  await page
    .locator("dialog")
    .getByLabel(/^Room type/)
    .selectOption("rt1");
}
test("validation ProblemDetails is shown under the matching field and keeps entered values", async ({
  page,
}) => {
  await api(page);
  await page.route("**/api/rooms", (route) =>
    route.request().method() === "POST"
      ? route.fulfill({
          status: 400,
          contentType: "application/problem+json",
          body: JSON.stringify({
            title: "Validation failed",
            errors: { Name: ["Room number already exists."] },
          }),
        })
      : route.fallback(),
  );
  await openRoomForm(page);
  await page
    .getByRole("button", { name: "Create room", exact: true })
    .last()
    .click();
  await expect(page.locator("#field-error-name")).toHaveText(
    "Room number already exists.",
  );
  await expect(page.getByLabel("Room number")).toHaveValue("202");
  await expect(page.locator(".toast")).toHaveCount(0);
});
test("double submit sends one POST and waits for the server before announcing success", async ({
  page,
}) => {
  await api(page);
  let writes = 0;
  let release: () => void = () => {};
  const waitForRelease = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route("**/api/rooms", async (route) => {
    if (route.request().method() !== "POST") {
      await route.fallback();
      return;
    }
    writes++;
    await waitForRelease;
    await route.fulfill({
      status: 201,
      contentType: "application/json",
      body: JSON.stringify({
        ...collections.rooms[0],
        id: "room-created",
        name: "202",
      }),
    });
  });
  await openRoomForm(page);
  await page.locator("dialog form").evaluate((form: HTMLFormElement) => {
    form.requestSubmit();
    form.requestSubmit();
  });
  await expect.poll(() => writes).toBe(1);
  await expect(page.locator(".toast")).toHaveCount(0);
  await expect(page.getByRole("button", { name: /Saving/ })).toBeDisabled();
  release();
  await expect(page.locator("dialog")).toHaveCount(0);
  await expect(page.locator(".toast")).toContainText("Record saved.");
  expect(writes).toBe(1);
});
test("unknown write outcome blocks resubmission and asks for a reload", async ({
  page,
}) => {
  await api(page);
  await page.route("**/api/rooms", (route) =>
    route.request().method() === "POST"
      ? route.abort("failed")
      : route.fallback(),
  );
  await openRoomForm(page);
  await page
    .getByRole("button", { name: "Create room", exact: true })
    .last()
    .click();
  await expect(
    page.getByText(
      "The result is not confirmed yet. Please reload the record before trying again.",
      { exact: true },
    ),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Reload screen", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Create room", exact: true }).last(),
  ).toBeDisabled();
});
test("server readiness and desktop/mobile layout stay visible", async ({
  page,
}) => {
  await api(page);
  await page.goto("/room-readiness");
  await expect(page.locator(".room-tile")).toContainText("Pending Inspection");
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth + 1,
    ),
  ).toBe(true);
  await page.screenshot({
    path: "docs/verification/api-readiness-desktop.png",
    fullPage: true,
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/rooms");
  await expect(page.locator(".page-header h1")).toHaveText("Room List");
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth + 1,
    ),
  ).toBe(true);
  await page.screenshot({
    path: "docs/verification/api-rooms-mobile.png",
    fullPage: true,
  });
});

test("an empty successful API response displays the existing empty state", async ({
  page,
}) => {
  await api(page);
  await page.route("**/api/rooms?**", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        items: [],
        page: 1,
        pageSize: 100,
        totalCount: 0,
        snapshot: "2026-10-16T10:00:00Z",
      }),
    }),
  );
  await page.goto("/rooms");
  await expect(
    page.getByRole("heading", { name: "No matching records", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Retry", exact: true }),
  ).toHaveCount(0);
});
