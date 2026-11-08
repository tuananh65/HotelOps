import { test, expect } from "@playwright/test";
const coverage = {
  cleaning: "PERSISTED_SOURCE",
  inspection: "NOT_ACCEPTED",
  rework: "PERSISTED_SOURCE_NOT_SHARED_ACCEPTED",
  readiness: "PERSISTED_EVENTS_NOT_SHARED_ACCEPTED",
  roomStatusHistory: "UNAVAILABLE_CYCLE_LINK_CONTRACT",
};
const cycle = {
  id: 3,
  roomId: 4,
  branchId: 2,
  roomNumber: "101",
  number: "CL-3",
  cleaningType: "Checkout",
  recordedStatus: "IN_PROGRESS",
  workflowState: "PENDING_INSPECTION",
  createdAtUtc: "2026-11-07T00:00:00Z",
  startUtc: null,
  endUtc: null,
  isCurrentCycle: true,
  result: null,
  reworkCount: 0,
  cancellationReason: null,
  assignees: [],
};
test("history filters/paginates cycles, opens ordered timeline and preserves source coverage", async ({
  page,
}) => {
  const queries: URL[] = [];
  await page.route("**/api/rooms/4/turnover-history?**", (r) => {
    const url = new URL(r.request().url());
    queries.push(url);
    return r.fulfill({
      json: {
        roomId: 4,
        branchId: 2,
        items: [
          { ...cycle, isCurrentCycle: url.searchParams.get("page") === "1" },
        ],
        total: 21,
        filters: { page: Number(url.searchParams.get("page")), pageSize: 20 },
        coverage,
      },
    });
  });
  await page.route("**/api/rooms/4/turnover-history/3?**", (r) =>
    r.fulfill({
      json: {
        cycle,
        timeline: [
          {
            sourceType: "CLEANING_TASK",
            sourceId: 3,
            event: "CREATED",
            atUtc: "2026-11-07T00:00:00Z",
            actorId: 5,
            state: null,
            note: null,
          },
        ],
        timelineTotal: 1,
        timelinePage: 1,
        timelinePageSize: 50,
        inspections: [],
        reworks: [],
        evidence: [],
        coverage,
      },
    }),
  );
  await page.goto("/rooms/turnover?roomId=4&branchId=2");
  await expect(
    page.getByRole("link", { name: "CL-3", exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("note")).toContainText("NOT_ACCEPTED");
  await page.getByLabel("Từ UTC", { exact: true }).fill("2026-11-01T00:00:00Z");
  await page.getByRole("button", { name: "Tải lịch sử", exact: true }).click();
  await expect
    .poll(() => queries.at(-1)?.searchParams.get("from"))
    .toBe("2026-11-01T00:00:00Z");
  await page.getByRole("button", { name: "Trang sau", exact: true }).click();
  await expect.poll(() => queries.at(-1)?.searchParams.get("page")).toBe("2");
  await expect(
    page.getByRole("cell", { name: "Lịch sử", exact: true }),
  ).toBeVisible();
  await page.getByRole("link", { name: "CL-3", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Timeline (1)", exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("listitem")).toContainText("CREATED");
  await expect(
    page.getByText("Current cycle · PENDING_INSPECTION", { exact: true }),
  ).toBeVisible();
});
test("history empty page and explicit unavailable result filter have no mock fallback", async ({
  page,
}) => {
  await page.route("**/api/rooms/4/turnover-history?**", (r) =>
    new URL(r.request().url()).searchParams.has("result")
      ? r.fulfill({ status: 503, json: { title: "UNAVAILABLE" } })
      : r.fulfill({
          json: {
            roomId: 4,
            branchId: 2,
            items: [],
            total: 0,
            filters: { page: 1, pageSize: 20 },
            coverage,
          },
        }),
  );
  await page.goto("/rooms/turnover?roomId=4&branchId=2");
  await expect(
    page.getByText("Không có cycle phù hợp bộ lọc.", { exact: true }),
  ).toBeVisible();
  await page
    .getByLabel("Kết quả Inspection", { exact: true })
    .selectOption("PASS");
  await page.getByRole("button", { name: "Tải lịch sử", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText(
    "Inspection contract accepted",
  );
});
for (const status of [401, 403, 404, 503])
  test(`history ${status} exposes error without prototype data`, async ({
    page,
  }) => {
    await page.route("**/api/rooms/4/turnover-history?**", (r) =>
      r.fulfill({ status, json: { title: "DENIED" } }),
    );
    await page.goto("/rooms/turnover?roomId=4&branchId=2");
    await expect(page.getByRole("alert")).toBeVisible();
    await expect(
      page.getByRole("link", { name: "CL-3", exact: true }),
    ).toHaveCount(0);
  });
test("history ignores stale room response after navigation", async ({
  page,
}) => {
  let release!: () => void;
  const hold = new Promise<void>((r) => {
    release = r;
  });
  await page.route("**/api/rooms/4/turnover-history?**", async (r) => {
    await hold;
    await r
      .fulfill({
        json: {
          roomId: 4,
          branchId: 2,
          items: [cycle],
          total: 1,
          filters: { page: 1, pageSize: 20 },
          coverage,
        },
      })
      .catch(() => {});
  });
  await page.route("**/api/rooms/5/turnover-history?**", (r) =>
    r.fulfill({
      json: {
        roomId: 5,
        branchId: 2,
        items: [],
        total: 0,
        filters: { page: 1, pageSize: 20 },
        coverage,
      },
    }),
  );
  await page.goto("/rooms/turnover?roomId=4&branchId=2", {
    waitUntil: "domcontentloaded",
  });
  await page.getByLabel("Phòng (ID)", { exact: true }).fill("5");
  await page.getByRole("button", { name: "Tải lịch sử", exact: true }).click();
  await expect(
    page.getByText("Không có cycle phù hợp bộ lọc.", { exact: true }),
  ).toBeVisible();
  release();
  await expect(
    page.getByRole("link", { name: "CL-3", exact: true }),
  ).toHaveCount(0);
});
