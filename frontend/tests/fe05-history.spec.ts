import { test, expect, type Page } from "@playwright/test";
import { createServer, type ViteDevServer } from "vite";
import react from "@vitejs/plugin-react";
test.use({ channel: "msedge" });
// Intercepted local HTTP fixture, not a real AuthProvider or production route.
let server: ViteDevServer, origin: string;
const host = {
  authorization: "granted",
  sessionKey: "s1",
  principalId: "u1",
  branchId: 7,
  inspectionView: true,
};
const entry = `import React from 'react';import {createRoot} from 'react-dom/client';import {HistoryRoute} from '/src/features/inspectionHistory/HistoryRoute.tsx';
const root=createRoot(document.getElementById('root'));window.denials=[];
window.renderHost=(context,initialId)=>root.render(React.createElement(React.StrictMode,null,React.createElement(HistoryRoute,{context,initialId,onAccessDenied:status=>window.denials.push(status)})));window.renderHost();`;
test.beforeAll(async () => {
  server = await createServer({
    configFile: false,
    plugins: [
      react(),
      {
        name: "history-fixture",
        resolveId(id) {
          if (id === "/__history-entry") return "\0history-entry";
        },
        load(id) {
          if (id === "\0history-entry") return entry;
        },
        configureServer(dev) {
          dev.middlewares.use(async (req, res, next) => {
            if (req.url?.split("?")[0] !== "/__history") return next();
            res.setHeader("Content-Type", "text/html");
            res.end(
              await dev.transformIndexHtml(
                "/__history",
                '<div id="root"></div><script type="module" src="/__history-entry"></script>',
              ),
            );
          });
        },
      },
    ],
    server: { host: "127.0.0.1", port: 0 },
  });
  await server.listen();
  origin = server.resolvedUrls!.local[0];
});
test.afterAll(async () => {
  await server?.close();
});
async function render(page: Page, context: unknown = host, id?: string) {
  await page.evaluate(
    ([c, id]) => (window as any).renderHost(c, id),
    [context, id],
  );
}
function row(branch = 7, name = "INSP-42") {
  return {
    id: 42,
    number: name,
    roomId: 3,
    roomNumber: "301",
    branchId: branch,
    cleaningTaskId: 12,
    versionId: 4,
    versionNumber: 2,
    inspectorId: 8,
    inspectorName: "Inspector One",
    status: "COMPLETED",
    result: "FAIL",
    at: "2026-11-13T10:00:00",
    summary: null,
    reworkCount: 1,
  };
}
function collection(url: string, name?: string) {
  const p = new URL(url).searchParams,
    branch = Number(p.get("branchId"));
  return {
    items: p.get("search") === "empty" ? [] : [row(branch, name)],
    total: p.get("search") === "empty" ? 0 : 21,
    filters: {
      branchId: branch,
      page: Number(p.get("page")),
      pageSize: Number(p.get("pageSize")),
      roomId: p.has("roomId") ? Number(p.get("roomId")) : null,
      inspectorId: p.has("inspectorId") ? Number(p.get("inspectorId")) : null,
      housekeeperId: p.has("housekeeperId")
        ? Number(p.get("housekeeperId"))
        : null,
      from: p.get("from"),
      to: p.get("to"),
      result: p.get("result"),
      search: p.get("search"),
    },
  };
}
function detail(branch = 7) {
  return {
    inspection: row(branch),
    housekeepers: [
      {
        id: 1,
        userId: 2,
        name: "Cleaner One",
        status: "ENDED",
        assignedAt: "2026-11-13T09:00:00",
        endedAt: null,
        reason: null,
      },
    ],
    criteria: [
      {
        id: 5,
        itemId: 6,
        criterion: "Historical criterion",
        description: null,
        mandatory: true,
        passCondition: "No dust",
        outcome: "FAIL",
        note: "Dust found",
        at: null,
        group: null,
        evidenceIds: [9],
      },
    ],
    defects: [
      {
        id: 10,
        itemId: 6,
        description: "Dust defect",
        status: "OPEN",
        severityId: 1,
        correctiveAction: null,
        resolvedAt: null,
        resolutionNote: null,
        location: "Desk",
        evidenceIds: [9],
      },
    ],
    reworks: [
      {
        id: 11,
        cleaningTaskId: 12,
        inspectionId: 42,
        assigneeId: 2,
        reason: "Clean again",
        status: "PENDING",
        requestedAt: "2026-11-13T10:00:00",
        completedAt: null,
        note: null,
      },
    ],
    cycleInspections: [row(branch)],
    evidence: [
      {
        id: 9,
        entityType: "INSPECTION",
        entityId: 42,
        name: "private.png",
        mimeType: "image/png",
        bytes: 8,
        uploadedAt: "2026-11-13T10:00:00Z",
        downloadPath: "https://untrusted.invalid/private",
      },
    ],
  };
}
test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    const originalCreate = URL.createObjectURL.bind(URL),
      originalRevoke = URL.revokeObjectURL.bind(URL);
    (window as any).created = [];
    (window as any).revoked = [];
    URL.createObjectURL = (blob) => {
      const url = originalCreate(blob);
      (window as any).created.push(url);
      return url;
    };
    URL.revokeObjectURL = (url) => {
      (window as any).revoked.push(url);
      originalRevoke(url);
    };
  });
  await page.goto(origin + "__history?branchId=999");
  await expect(page.getByRole("alert")).toBeVisible();
});
test("direct loading and unresolved/denied/invalid contexts never preload protected data", async ({
  page,
}) => {
  const calls: string[] = [];
  await page.route("**/api/**", (route) => {
    calls.push(route.request().url());
    return route.fulfill({ status: 500 });
  });
  for (const context of [
    null,
    { ...host, authorization: "unresolved" },
    { ...host, authorization: "denied" },
    { ...host, inspectionView: false },
    { ...host, sessionKey: "" },
    { ...host, principalId: "" },
    ...[0, -1, "abc", "9007199254740992"].map((branchId) => ({
      ...host,
      branchId,
    })),
  ]) {
    await render(page, context, "42");
    await expect(page.getByRole("alert")).toBeVisible();
  }
  await render(page, host, "-1");
  await expect(page.getByRole("alert")).toContainText("ID");
  expect(calls).toEqual([]);
});
test("list/detail preserve historical results and separate defects/reworks; evidence is explicit GET download", async ({
  page,
}) => {
  const calls: { url: string; method: string }[] = [];
  await page.route("**/api/**", (route) => {
    const request = route.request();
    calls.push({ url: request.url(), method: request.method() });
    if (request.url().includes("/evidence/"))
      return route.fulfill({
        body: Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
        contentType: "image/png",
      });
    return route.fulfill({
      json: request.url().includes("/42?")
        ? detail()
        : collection(request.url()),
    });
  });
  await render(page);
  await page.getByRole("button", { name: "INSP-42", exact: true }).click();
  const view = page.getByRole("region", { name: "Chi tiết lịch sử" });
  await expect(
    view.getByText("Historical criterion", { exact: false }),
  ).toBeVisible();
  await expect(
    view.getByRole("heading", { name: "Defects", exact: true }),
  ).toBeVisible();
  await expect(
    view.getByRole("heading", { name: "Reworks", exact: true }),
  ).toBeVisible();
  await expect(view.getByText(/Checklist #4 v2/).first()).toBeVisible();
  await expect(view.getByText(/Cleaner One/)).toBeVisible();
  expect(calls.some((c) => c.url.includes("/evidence/"))).toBe(false);
  await expect(page.locator("img,iframe,object,embed")).toHaveCount(0);
  const download = page.waitForEvent("download");
  await view
    .getByRole("button", { name: "Tải evidence #9", exact: true })
    .click();
  expect((await download).suggestedFilename()).toBe(
    "inspection-42-evidence-9.png",
  );
  const file = calls.filter((c) => c.url.includes("/evidence/"));
  expect(file).toHaveLength(1);
  expect(new URL(file[0].url).pathname).toBe(
    "/api/inspection-history/42/evidence/9",
  );
  expect(new URL(file[0].url).search).toBe("?branchId=7");
  expect(calls.every((c) => c.method === "GET")).toBe(true);
  expect(
    calls.some(
      (c) =>
        c.url.includes("branchId=999") || c.url.includes("untrusted.invalid"),
    ),
  ).toBe(false);
  await expect
    .poll(() => page.evaluate(() => (window as any).revoked.length))
    .toBe(1);
});
test("filters and pagination use existing contract; invalid UTC sends no request; empty state retains filters", async ({
  page,
}) => {
  const calls: string[] = [];
  await page.route("**/api/**", (route) => {
    calls.push(route.request().url());
    return route.fulfill({ json: collection(route.request().url()) });
  });
  await render(page);
  await expect(
    page.getByRole("button", { name: "INSP-42", exact: true }),
  ).toBeVisible();
  const before = calls.length;
  await page.getByLabel("Từ (UTC)", { exact: true }).fill("2026-11-13");
  await page
    .getByRole("button", { name: "Áp dụng bộ lọc", exact: true })
    .click();
  await expect(page.getByRole("alert")).toContainText("UTC");
  expect(calls).toHaveLength(before);
  await page
    .getByLabel("Từ (UTC)", { exact: true })
    .fill("2026-11-12T00:00:00Z");
  await page
    .getByLabel("Đến (UTC)", { exact: true })
    .fill("2026-11-13T00:00:00Z");
  for (const [label, value] of [
    ["ID phòng", "3"],
    ["ID người kiểm tra", "8"],
    ["ID nhân viên dọn phòng", "2"],
    ["Tìm kiếm", "dust"],
  ])
    await page.getByLabel(label, { exact: true }).fill(value);
  await page.getByLabel("Kết quả", { exact: true }).selectOption("FAIL");
  await page
    .getByRole("button", { name: "Áp dụng bộ lọc", exact: true })
    .click();
  await expect.poll(() => calls.at(-1)).toContain("housekeeperId=2");
  await expect(
    page.getByRole("button", { name: "Trang sau", exact: true }),
  ).toBeEnabled();
  await page.getByRole("button", { name: "Trang sau", exact: true }).click();
  await expect
    .poll(() => new URL(calls.at(-1)!).searchParams.get("page"))
    .toBe("2");
  await expect(page.getByText("21 bản ghi · Trang 2")).toBeVisible();
  await page.getByLabel("Tìm kiếm", { exact: true }).fill("empty");
  await page
    .getByRole("button", { name: "Áp dụng bộ lọc", exact: true })
    .click();
  await expect(
    page.getByText("Không có lịch sử khớp bộ lọc đã áp dụng."),
  ).toBeVisible();
  await expect(page.getByLabel("Tìm kiếm", { exact: true })).toHaveValue(
    "empty",
  );
});
for (const change of [
  { branchId: 8 },
  { sessionKey: "s2" },
  { principalId: "u2" },
])
  test(`scope replacement discards pending data: ${JSON.stringify(change)}`, async ({
    page,
  }) => {
    let epoch = 1,
      seen = false,
      release!: () => void;
    const held = new Promise<void>((resolve) => (release = resolve));
    await page.route("**/api/**", async (route) => {
      const captured = epoch;
      if (captured === 1) {
        seen = true;
        await held;
      }
      await route
        .fulfill({
          json: collection(
            route.request().url(),
            captured === 1 ? "Old private history" : "New private history",
          ),
        })
        .catch(() => {});
    });
    await render(page);
    await expect.poll(() => seen).toBe(true);
    epoch = 2;
    await render(page, { ...host, ...change });
    await expect(
      page.getByRole("button", { name: "New private history", exact: true }),
    ).toBeVisible();
    release();
    await page.evaluate(
      () => new Promise((resolve) => setTimeout(resolve, 100)),
    );
    await expect(
      page.getByRole("button", { name: "Old private history", exact: true }),
    ).toHaveCount(0);
    await render(page, { ...host, authorization: "denied" });
    await expect(
      page.getByRole("button", { name: "New private history", exact: true }),
    ).toHaveCount(0);
  });
for (const status of [401, 403])
  test(`${status} closes protected data and notifies host`, async ({
    page,
  }) => {
    let calls = 0;
    await page.route("**/api/**", (route) => {
      calls++;
      return route.request().url().includes("/42?")
        ? route.fulfill({ status, json: {} })
        : route.fulfill({ json: collection(route.request().url()) });
    });
    await render(page, host, "42");
    await expect(page.getByRole("alert")).toContainText(
      status === 401 ? "xác thực lại" : "Không có quyền",
    );
    await expect
      .poll(() =>
        page.evaluate((code) => (window as any).denials.includes(code), status),
      )
      .toBe(true);
    await expect(
      page.getByRole("form", { name: "Bộ lọc lịch sử" }),
    ).toHaveCount(0);
    const deniedCalls = calls;
    for (const id of [undefined, "-1", "42"]) {
      await render(page, host, id);
      await expect(page.getByRole("alert")).toContainText(
        status === 401 ? "xác thực lại" : "Không có quyền",
      );
    }
    expect(calls).toBe(deniedCalls);
  });
test("cross-branch response is never displayed", async ({ page }) => {
  await page.route("**/api/**", (route) => {
    const body = collection(route.request().url());
    body.items[0].branchId = 99;
    return route.fulfill({ json: body });
  });
  await render(page);
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "INSP-42", exact: true }),
  ).toHaveCount(0);
});
test("unsupported evidence response cannot create a temporary URL", async ({
  page,
}) => {
  await page.route("**/api/**", (route) =>
    route.request().url().includes("/evidence/")
      ? route.fulfill({ body: "<html>bad</html>", contentType: "text/html" })
      : route.fulfill({
          json: route.request().url().includes("/42?")
            ? detail()
            : collection(route.request().url()),
        }),
  );
  await render(page, host, "42");
  await page
    .getByRole("button", { name: "Tải evidence #9", exact: true })
    .click();
  await expect(page.getByRole("alert")).toContainText("Định dạng");
  expect(await page.evaluate(() => (window as any).created.length)).toBe(0);
  await expect(page.locator("img,iframe,object,embed")).toHaveCount(0);
});
test("logout cancels an evidence download before any blob URL is exposed", async ({
  page,
}) => {
  let seen = false,
    release!: () => void;
  const held = new Promise<void>((resolve) => (release = resolve));
  await page.route("**/api/**", async (route) => {
    if (route.request().url().includes("/evidence/")) {
      seen = true;
      await held;
      await route
        .fulfill({
          body: Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
          contentType: "image/png",
        })
        .catch(() => {});
      return;
    }
    await route.fulfill({
      json: route.request().url().includes("/42?")
        ? detail()
        : collection(route.request().url()),
    });
  });
  await render(page, host, "42");
  await page
    .getByRole("button", { name: "Tải evidence #9", exact: true })
    .click();
  await expect.poll(() => seen).toBe(true);
  await render(page, { ...host, authorization: "denied" });
  release();
  await expect(page.getByRole("alert")).toBeVisible();
  await page.evaluate(() => new Promise((resolve) => setTimeout(resolve, 100)));
  expect(await page.evaluate(() => (window as any).created.length)).toBe(0);
});
