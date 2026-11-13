import { test, expect, type Page } from "@playwright/test";
import { createServer, type ViteDevServer } from "vite";
import react from "@vitejs/plugin-react";

test.use({ channel: "msedge" });

// Local intercepted-HTTP component fixture only. This does not mount a production
// route or supply an authentication authority. DEV3 must supply real host state.
let server: ViteDevServer;
let origin: string;
const host = {
  authorization: "granted",
  sessionKey: "s1",
  principalId: "u1",
  branchId: 7,
  checklistManage: true,
};
const etag = '"' + "A".repeat(64) + '"';
const entry = `import React from 'react';
import {createRoot} from 'react-dom/client';
import {MemoryRouter} from 'react-router-dom';
import {ChecklistRoute} from '/src/features/inspectionChecklists/ChecklistRoute.tsx';
const root=createRoot(document.getElementById('root'));
window.renderHost=(context,initialId)=>root.render(React.createElement(React.StrictMode,null,
  React.createElement(MemoryRouter,{initialEntries:['/?branchId=999']},React.createElement(ChecklistRoute,{context,initialId}))));
window.renderHost();`;
test.beforeAll(async () => {
  server = await createServer({
    configFile: false,
    plugins: [
      react(),
      {
        name: "ui-a-test-fixture",
        resolveId(id) {
          if (id === "/__ui-a-entry") return "\0ui-a-entry";
        },
        load(id) {
          if (id === "\0ui-a-entry") return entry;
        },
        configureServer(dev) {
          dev.middlewares.use(async (req, res, next) => {
            if (req.url?.split("?")[0] === "/__ui-a") {
              res.setHeader("Content-Type", "text/html");
              res.end(
                await dev.transformIndexHtml(
                  "/__ui-a",
                  '<div id="root"></div><script type="module" src="/__ui-a-entry"></script>',
                ),
              );
              return;
            }
            next();
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
async function render(page: Page, context: unknown = host, initialId?: string) {
  await page.evaluate(
    ([context, initialId]) => (window as any).renderHost(context, initialId),
    [context, initialId],
  );
}
function collection(branch: number, name = `branch-${branch}`) {
  return {
    branchId: branch,
    page: 1,
    pageSize: 20,
    isActive: null,
    search: null,
    status: null,
    roomTypeId: null,
    total: 1,
    items: [
      {
        id: 42,
        branchId: branch,
        code: `CL${branch}`,
        name,
        active: true,
        roomTypeIds: [11],
        versions: [],
      },
    ],
  };
}
function detail(branch = 7) {
  return {
    id: 42,
    branchId: branch,
    code: "CL7",
    name: "Detail protected",
    active: true,
    roomTypeIds: [11],
    versions: [
      {
        id: 1,
        number: 1,
        status: "DRAFT",
        usedByInspection: false,
        criteria: [{ id: 1, order: 1, text: "Clean", mandatory: true }],
      },
    ],
  };
}
test.beforeEach(async ({ page }) => {
  await page.goto(origin + "__ui-a");
  await expect(page.getByRole("alert")).toBeVisible();
});

test("direct URL, unresolved auth, missing permission and invalid branches send no API request", async ({
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
    { ...host, checklistManage: false },
    ...["", 0, -1, "abc", "9007199254740992"].map((branchId) => ({
      ...host,
      branchId,
    })),
  ]) {
    await render(page, context, "42");
    await expect(page.getByRole("alert")).toBeVisible();
  }
  expect(calls).toEqual([]);
});

test("host branch overrides URL; branch/session/principal switches discard stale content and late responses", async ({
  page,
}) => {
  const pending: Promise<void>[] = [];
  let release!: () => void;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route("**/api/**", async (route) => {
    const branch = Number(
      route
        .request()
        .url()
        .match(/branches\/(\d+)/)?.[1],
    );
    if (branch === 7) {
      pending.push(held);
      await held;
    }
    await route.fulfill({ json: collection(branch) }).catch(() => {});
  });
  await render(page);
  await expect.poll(() => pending.length).toBeGreaterThan(0);
  await render(page, { ...host, branchId: 8 });
  await expect(page.getByText("branch-8", { exact: true })).toBeVisible();
  release();
  await expect(page.getByText("branch-7", { exact: true })).toHaveCount(0);
  await render(page, { ...host, authorization: "denied" });
  await expect(page.getByText("branch-8", { exact: true })).toHaveCount(0);
  for (const context of [
    { ...host, branchId: 8, sessionKey: "s2" },
    { ...host, branchId: 8, principalId: "u2", sessionKey: "s3" },
  ]) {
    await render(page, context);
    await expect(page.getByText("branch-8", { exact: true })).toBeVisible();
    await render(page, { ...context, authorization: "unresolved" });
    await expect(page.getByText("branch-8", { exact: true })).toHaveCount(0);
  }
});

for (const change of [{ sessionKey: "s2" }, { principalId: "u2" }])
  test(`late response from old identity is discarded: ${JSON.stringify(change)}`, async ({
    page,
  }) => {
    let epoch = 1;
    let oldRequestSeen = false;
    let release!: () => void;
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    await page.route("**/api/**", async (route) => {
      const requestEpoch = epoch;
      if (requestEpoch === 1) {
        oldRequestSeen = true;
        await held;
      }
      await route
        .fulfill({
          json: collection(
            7,
            requestEpoch === 1 ? "old identity" : "new identity",
          ),
        })
        .catch(() => {});
    });
    await render(page);
    await expect.poll(() => oldRequestSeen).toBe(true);
    epoch = 2;
    await render(page, { ...host, ...change });
    await expect(page.getByText("new identity", { exact: true })).toBeVisible();
    release();
    await page.evaluate(
      () => new Promise((resolve) => setTimeout(resolve, 100)),
    );
    await expect(page.getByText("old identity", { exact: true })).toHaveCount(
      0,
    );
    await expect(page.getByText("new identity", { exact: true })).toBeVisible();
  });

for (const status of [401, 403])
  test(`${status} detail denies entire protected display and stays denied`, async ({
    page,
  }) => {
    await page.route("**/api/**", (route) =>
      route.request().url().includes("/42")
        ? route.fulfill({ status, json: {} })
        : route.fulfill({ json: collection(7) }),
    );
    await render(page, host, "42");
    await expect(page.getByRole("alert")).toContainText("xác thực lại");
    await expect(
      page.getByRole("button", { name: "Tạo checklist", exact: true }),
    ).toHaveCount(0);
    await render(page);
    await expect(page.getByRole("alert")).toContainText("xác thực lại");
  });

test("manual IDs create without catalog lookup or MASTER_DATA_MANAGE", async ({
  page,
}) => {
  const calls: string[] = [];
  const writes: unknown[] = [];
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    calls.push(request.url());
    if (request.url().includes("/auth/csrf"))
      return route.fulfill({
        json: { csrfToken: "fixture", headerName: "X-CSRF-TOKEN" },
      });
    if (request.method() === "POST") {
      writes.push(request.postDataJSON());
      return route.fulfill({ json: detail(), headers: { ETag: etag } });
    }
    return route.fulfill({
      json: request.url().includes("/42") ? detail() : collection(7),
      headers: { ETag: etag },
    });
  });
  await render(page);
  await page
    .getByRole("button", { name: "Tạo checklist", exact: true })
    .click();
  const form = page.getByRole("form", { name: "Tạo checklist", exact: true });
  await form.getByLabel("Mã checklist", { exact: true }).fill("CL7");
  await form
    .getByLabel("Tên checklist", { exact: true })
    .fill("Manual checklist");
  await form.getByLabel("Nội dung", { exact: true }).fill("Clean");
  const ids = form.getByLabel(/Mã loại phòng áp dụng/);
  for (const invalid of ["0", "-1", "1.5", "1e2", "abc", "9007199254740992"]) {
    await ids.fill(invalid);
    await form.getByRole("button", { name: "Lưu Draft", exact: true }).click();
    await expect(form.getByRole("alert")).toBeVisible();
    expect(writes).toHaveLength(0);
  }
  await ids.fill("11,12");
  await form.getByRole("button", { name: "Lưu Draft", exact: true }).click();
  await expect.poll(() => writes.length).toBe(1);
  expect((writes[0] as any).roomTypeIds).toEqual([11, 12]);
  expect(calls.some((url) => url.includes("/api/room-types"))).toBe(false);
});

test("logout during CSRF bootstrap prevents the subsequent write", async ({
  page,
}) => {
  let release!: () => void;
  let bootstrapped = false;
  let writes = 0;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route("**/api/**", async (route) => {
    if (route.request().url().includes("/auth/csrf")) {
      bootstrapped = true;
      await held;
      await route
        .fulfill({ json: { csrfToken: "fixture", headerName: "X-CSRF-TOKEN" } })
        .catch(() => {});
      return;
    }
    if (route.request().method() !== "GET") writes++;
    await route.fulfill({
      json: route.request().url().includes("/42") ? detail() : collection(7),
      headers: { ETag: etag },
    });
  });
  await render(page, host, "42");
  await page
    .getByRole("button", { name: "Công bố Draft", exact: true })
    .click();
  const form = page.getByRole("form", { name: "Xác nhận phiên bản" });
  await form.getByLabel("Lý do", { exact: true }).fill("Publish");
  await form.getByRole("checkbox").check();
  await form.getByRole("button", { name: "Xác nhận", exact: true }).click();
  await expect.poll(() => bootstrapped).toBe(true);
  await render(page, { ...host, authorization: "denied" });
  release();
  await expect(page.getByRole("alert")).toBeVisible();
  // A read through the fixture confirms the browser event loop processed the release.
  await page.evaluate(() => new Promise((resolve) => setTimeout(resolve, 100)));
  expect(writes).toBe(0);
});
