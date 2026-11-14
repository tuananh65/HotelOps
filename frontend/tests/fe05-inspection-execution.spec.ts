import { expect, test } from "@playwright/test";
import { createServer, type ViteDevServer } from "vite";
import react from "@vitejs/plugin-react";
import type { Page } from "@playwright/test";
test.use({ channel: "msedge" });
let server: ViteDevServer;
let url = "";
const host = {
  authorization: "granted",
  sessionKey: "generation-a",
  principalId: "42",
  branchId: 7,
  inspectionPerform: true,
};
const etag = '"0123456789ABCDEF"';
const html = `<div id="root"></div><script type="module" src="/__execution-entry"></script>`;
const entry = `import React from 'react';import{createRoot}from'react-dom/client';import{InspectionRoute}from'/src/features/inspectionExecution/InspectionRoute.tsx';const root=createRoot(document.getElementById('root'));window.renderHost=(context,id,severities)=>root.render(React.createElement(React.StrictMode,null,React.createElement(InspectionRoute,{context,initialId:id,severities,onSessionInvalid:()=>{window.invalids=(window.invalids||0)+1}})));window.renderHost();`;
const snapshot = (state = "ASSIGNED", branch = 7) => ({
  id: 42,
  branchId: branch,
  roomId: 11,
  cleaningTaskId: 22,
  inspectorId: 44,
  versionId: 55,
  state,
  dueAt: null,
  criteria: [
    {
      id: null,
      itemId: 17,
      criterion: "Bed clean",
      description: null,
      mandatory: true,
      passCondition: null,
      outcome: null,
      note: null,
      at: null,
    },
  ],
  defects: [],
  evidence: [],
});
test.beforeAll(async () => {
  server = await createServer({
    configFile: false,
    plugins: [
      react(),
      {
        name: "execution-harness",
        resolveId(id) {
          if (id === "/__execution-entry") return "\0execution-entry";
        },
        load(id) {
          if (id === "\0execution-entry") return entry;
        },
        configureServer(dev) {
          dev.middlewares.use(async (req, res, next) => {
            if (req.url?.split("?")[0] !== "/__execution") return next();
            res.setHeader("Content-Type", "text/html");
            res.end(await dev.transformIndexHtml("/__execution", html));
          });
        },
      },
    ],
    server: { host: "127.0.0.1", port: 0 },
  });
  await server.listen();
  url = server.resolvedUrls!.local[0];
});
test.afterAll(async () => {
  await server?.close();
});
async function render(page: Page, context: unknown = host, id = "42") {
  await page.goto(url + "__execution");
  await page.evaluate(
    ([c, i]) => (window as any).renderHost(c, i),
    [context, id],
  );
}
test("gate OFF and invalid scope never issue protected HTTP", async ({
  page,
}) => {
  const requests: string[] = [];
  await page.route("**/api/**", (route) => {
    requests.push(route.request().url());
    return route.abort();
  });
  await render(page, { ...host, authorization: "denied" });
  await expect(page.getByRole("alert")).toContainText("not admitted");
  await render(page, host, "invalid");
  await expect(page.getByRole("alert")).toContainText("valid Inspection ID");
  expect(requests).toHaveLength(0);
});
test("scoped read and no premature READY or implicit conclusion", async ({
  page,
}) => {
  await page.route("**/api/**", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      headers: { ETag: etag },
      body: JSON.stringify(snapshot()),
    }),
  );
  await render(page);
  await expect(page.getByText(/State:/)).toContainText("ASSIGNED");
  await expect(
    page.getByText(/No directory or approval workflow/i),
  ).toBeVisible();
  await expect(
    page.getByText(/Evidence read\/download is not available/i),
  ).toBeVisible();
});
test("branch change A→B→A and session replacement discard old read", async ({
  page,
}) => {
  let resolveOld: () => void = () => {};
  let first = true;
  let oldSeen = false;
  await page.route("**/api/**", async (route) => {
    const branch = Number(
      new URL(route.request().url()).pathname.split("/")[3],
    );
    if (branch === 7 && first) {
      first = false;
      oldSeen = true;
      await new Promise<void>((resolve) => (resolveOld = resolve));
      await route.fulfill({ status: 401, body: "{}" }).catch(() => {});
      return;
    }
    await route
      .fulfill({
        status: 200,
        contentType: "application/json",
        headers: { ETag: etag },
        body: JSON.stringify(snapshot("ASSIGNED", branch)),
      })
      .catch(() => {});
  });
  await render(page);
  await expect.poll(() => oldSeen).toBe(true);
  await page.evaluate(() =>
    (window as any).renderHost(
      {
        ...{
          authorization: "granted",
          sessionKey: "generation-b",
          principalId: "42",
          branchId: 8,
          inspectionPerform: true,
        },
      },
      "42",
    ),
  );
  await expect(page.getByText(/Branch #8/)).toBeVisible();
  await expect(page.getByText(/State:/)).toContainText("ASSIGNED");
  await page.evaluate(() =>
    (window as any).renderHost(
      {
        ...{
          authorization: "granted",
          sessionKey: "generation-c",
          principalId: "42",
          branchId: 7,
          inspectionPerform: true,
        },
      },
      "42",
    ),
  );
  await expect(page.getByText(/Branch #7/)).toBeVisible();
  await expect(page.getByText(/State:/)).toContainText("ASSIGNED");
  resolveOld();
  await page.waitForTimeout(100);
  expect(await page.evaluate(() => (window as any).invalids ?? 0)).toBe(0);
  await expect(page.getByText(/Branch #8/)).toHaveCount(0);
  await expect(page.getByText(/State:/)).toContainText("ASSIGNED");
});

test("session replacement during CSRF bootstrap prevents old write dispatch", async ({
  page,
}) => {
  let release = () => {};
  let csrfSeen = false,
    writes = 0;
  await page.route("**/api/**", async (route) => {
    if (route.request().url().endsWith("/api/auth/csrf")) {
      csrfSeen = true;
      await new Promise<void>((resolve) => {
        release = resolve;
      });
      await route
        .fulfill({
          json: { csrfToken: "old-session", headerName: "X-CSRF-TOKEN" },
        })
        .catch(() => {});
      return;
    }
    if (route.request().method() !== "GET") writes++;
    await route.fulfill({ json: snapshot(), headers: { ETag: etag } });
  });
  await render(page);
  await expect(
    page.getByRole("button", { name: "Start Inspection", exact: true }),
  ).toBeEnabled();
  await page.getByLabel("Reason", { exact: true }).fill("Start");
  await page
    .getByRole("button", { name: "Start Inspection", exact: true })
    .click();
  await expect.poll(() => csrfSeen).toBe(true);
  await page.evaluate((c) => (window as any).renderHost(c, "42"), {
    ...host,
    sessionKey: "replacement",
  });
  await expect(
    page.getByRole("button", { name: "Start Inspection", exact: true }),
  ).toBeEnabled();
  release();
  await page.waitForTimeout(100);
  expect(writes).toBe(0);
  await expect(page.getByLabel("Reason", { exact: true })).toHaveValue("");
});
test("401 closes scope and 403 does not invalidate session", async ({
  page,
}) => {
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 401, body: "{}" }),
  );
  await render(page);
  await expect(page.getByRole("alert")).toContainText("Session expired");
  const called = await page.evaluate(() => (window as any).invalids ?? 0);
  expect(called).toBeGreaterThan(0);
  await page.unrouteAll();
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 403, body: "{}" }),
  );
  await render(page, { ...host, sessionKey: "other" });
  await expect(page.getByRole("alert")).toContainText("permission denied");
  const after = await page.evaluate(() => (window as any).invalids ?? 0);
  expect(after).toBe(0);
});

test("401 remains denied across inspection URL changes until authority changes", async ({
  page,
}) => {
  let calls = 0;
  await page.route("**/api/**", (route) => {
    calls++;
    return route.fulfill({ status: 401, body: "{}" });
  });
  await render(page);
  await expect(page.getByRole("alert")).toContainText("Session expired");
  const before = calls;
  for (const id of ["43", "invalid", "42"]) {
    await page.evaluate(
      ([c, id]) => (window as any).renderHost(c, id),
      [host, id],
    );
    await expect(page.getByRole("alert")).toContainText("Session expired");
    await page.waitForTimeout(50);
    expect(calls).toBe(before);
  }
});

test("start, save FAIL, record private evidence and finish stop at Pending Conclusion", async ({
  page,
}) => {
  let current = snapshot();
  const writes: Array<{ path: string; body: any }> = [];
  await page.route("**/api/**", async (route) => {
    const request = route.request(),
      path = new URL(request.url()).pathname;
    if (path === "/api/auth/csrf")
      return route.fulfill({
        json: { csrfToken: "fixture-csrf", headerName: "X-CSRF-TOKEN" },
      });
    if (request.method() !== "GET") {
      expect(request.headers()["if-match"]).toBe(etag);
      expect(request.headers()["x-csrf-token"]).toBe("fixture-csrf");
      const body = request.postDataJSON();
      writes.push({ path, body });
      if (path.endsWith("/start")) current = snapshot("INSPECTING");
      if (path.endsWith("/results"))
        current.criteria[0] = {
          ...current.criteria[0],
          outcome: body.results[0].outcome,
        };
      if (path.endsWith("/finish"))
        current = { ...current, state: "PENDING_CONCLUSION" };
    }
    return route.fulfill({ json: current, headers: { ETag: etag } });
  });
  await render(page);
  await expect(
    page.getByRole("button", { name: "Start Inspection", exact: true }),
  ).toBeEnabled();
  await page.getByLabel("Reason", { exact: true }).fill("Inspection fixture");
  await page
    .getByRole("button", { name: "Start Inspection", exact: true })
    .click();
  await expect(page.getByText(/State:/)).toContainText("INSPECTING");
  await page.getByRole("combobox", { name: /^Result/ }).selectOption("FAIL");
  await page.getByRole("button", { name: "Save results", exact: true }).click();
  await expect(
    page.getByLabel("Failed criterion").locator("option[value='17']"),
  ).toHaveCount(1);
  await expect(
    page.getByRole("button", { name: "Record defect", exact: true }),
  ).toBeDisabled();
  // Explicit fixture-only catalog supplied through the trusted host input, never a source default.
  await page.evaluate(
    (c) =>
      (window as any).renderHost(c, "42", [
        { id: 617, code: "MAJOR", name: "Fixture major" },
      ]),
    host,
  );
  await page.getByLabel("Failed criterion").selectOption("17");
  await page.getByRole("combobox", { name: /^Severity/ }).selectOption("617");
  await page.getByLabel("Description", { exact: true }).fill("Stain");
  await page.getByLabel("Location", { exact: true }).fill("Bed");
  await page.getByLabel(/Evidence \(JPG/).setInputFiles({
    name: "evidence.png",
    mimeType: "image/png",
    buffer: Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
  });
  await page
    .getByRole("button", { name: "Record defect", exact: true })
    .click();
  await expect
    .poll(() => writes.filter((w) => w.path.endsWith("/defects")).length)
    .toBe(1);
  await expect(page.getByRole("button", { name: /Finish/ })).toBeEnabled();
  await page.getByRole("button", { name: /Finish/ }).click();
  await expect(page.getByText(/State:/)).toContainText("PENDING_CONCLUSION");
  expect(writes.map((w) => w.path.split("/").pop())).toEqual([
    "start",
    "results",
    "defects",
    "finish",
  ]);
  expect(writes[2].body).toMatchObject({
    itemId: 17,
    severityId: 617,
    photos: [{ mimeType: "image/png", base64: "iVBORw0KGgo=" }],
  });
  await expect(page.locator("img,iframe,object,embed,a[download]")).toHaveCount(
    0,
  );
});

for (const status of [412, 428, 503, "network"] as const) {
  test(`write ${status} blocks replay until an explicit authorized GET`, async ({
    page,
  }) => {
    let writes = 0,
      reads = 0;
    await page.route("**/api/**", (route) => {
      const request = route.request();
      if (request.url().endsWith("/api/auth/csrf"))
        return route.fulfill({
          json: { csrfToken: "fixture", headerName: "X-CSRF-TOKEN" },
        });
      if (request.method() === "GET") {
        reads++;
        return route.fulfill({ json: snapshot(), headers: { ETag: etag } });
      }
      writes++;
      return status === "network"
        ? route.abort("failed")
        : route.fulfill({ status, json: {} });
    });
    await render(page);
    await expect(
      page.getByRole("button", { name: "Start Inspection", exact: true }),
    ).toBeEnabled();
    await page.getByLabel("Reason", { exact: true }).fill("Start fixture");
    await page
      .getByRole("button", { name: "Start Inspection", exact: true })
      .click();
    await expect(
      page.getByText("This operation is blocked.", { exact: false }),
    ).toBeVisible();
    const before = reads;
    await page.waitForTimeout(100);
    expect(writes).toBe(1);
    expect(reads).toBe(before);
    await page
      .getByRole("button", { name: "Verify current state (GET)", exact: true })
      .click();
    await expect(
      page.getByRole("button", { name: "Start Inspection", exact: true }),
    ).toBeEnabled();
    expect(reads).toBeGreaterThan(before);
    expect(writes).toBe(1);
  });
}
