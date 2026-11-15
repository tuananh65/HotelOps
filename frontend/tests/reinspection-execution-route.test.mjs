import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
const session = {
  user: { id: "42", username: "supervisor", name: "Supervisor" },
  expiresAt: "2026-11-16T12:00:00Z",
  absoluteExpiresAt: "2026-11-16T20:00:00Z",
  mustChangePassword: false,
  csrfToken: "test-token",
};
const tag = '"0000000000000001"';
const data = {
  id: 8,
  reworkId: 2,
  branchId: 1,
  roomId: 3,
  cleaningTaskId: 4,
  originalInspectionId: 5,
  inspectorId: 42,
  versionId: 6,
  state: "ASSIGNED",
  sourceETag: tag,
  reworkETag: tag,
  bindingETag: tag,
  effectiveReadiness: "BLOCKED",
  messageCode: "MSG17",
  defects: [
    {
      id: 9,
      description: "PRIVATE-DEFECT",
      remediation: "PRIVATE-REMEDIATION",
      fixed: null,
      note: null,
      originalPhotos: [],
      remediationPhotos: [],
      reinspectionPhotos: [],
    },
  ],
};
const bundle = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `
import React from 'react'; import { createRoot } from 'react-dom/client'; import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from './src/auth/AuthProvider'; import { AuthRouteHost } from './src/auth/AuthRouteHost';
import { createAuthStore } from './src/auth/authStore'; import { createAuthApi } from './src/auth/authApi';
const store = createAuthStore(createAuthApi(window.fetch.bind(window))); window.reinspectionTest = { revalidate: () => store.revalidate() };
const host = <AuthRouteHost />; createRoot(document.getElementById('root')).render(<BrowserRouter><AuthProvider store={store} fallback={host}>{host}</AuthProvider></BrowserRouter>);
`,
  },
  bundle: true,
  outdir: ".test-output/reinspection-execution-memory-only",
  write: false,
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"test"', "import.meta.env": "{}" },
});
let browser;
before(async () => {
  browser = await chromium.launch({
    headless: true,
    executablePath:
      process.env.HOTELOPS_TEST_BROWSER ||
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  });
});
after(async () => browser?.close());
async function scenario(run) {
  const page = await browser.newPage();
  page.setDefaultTimeout(10000);
  const state = {
    signedIn: true,
    fail: false,
    posts: [],
    csrf: null,
    errors: [],
    data: structuredClone(data),
  };
  page.on("pageerror", (e) => state.errors.push(e.message));
  await page.route("**/*", async (route) => {
    const req = route.request(),
      path = new URL(req.url()).pathname;
    if (req.resourceType() === "document")
      return route.fulfill({
        contentType: "text/html",
        body: '<div id="root"></div>',
      });
    if (path === "/api/auth/session")
      return route.fulfill({
        status: state.signedIn ? 200 : 401,
        json: state.signedIn ? session : { code: "AUTHENTICATION_REQUIRED" },
      });
    if (path === "/api/branches/1/reinspections/8/execution")
      return route.fulfill({ json: state.data, headers: { ETag: tag } });
    if (path === "/api/branches/1/reinspections/8/original")
      return route.fulfill({
        headers: { ETag: tag },
        json: {
          reinspectionId: 8,
          reworkId: 2,
          branchId: 1,
          id: 5,
          roomId: 3,
          cleaningTaskId: 4,
          inspectorId: 7,
          versionId: 6,
          state: "FAILED",
          sourceETag: tag,
          summary: "PRIVATE-ORIGINAL-SUMMARY",
          criteria: [
            {
              itemId: 10,
              criterion: "Clean",
              description: null,
              outcome: "FAIL",
              note: "PRIVATE-ORIGINAL-NOTE",
              evidenceIds: [12],
            },
          ],
          defects: [
            {
              id: 9,
              itemId: 10,
              description: "PRIVATE-ORIGINAL-DEFECT",
              location: "Desk",
              evidenceIds: [12],
            },
          ],
          evidence: [
            {
              id: 12,
              entityType: "INSPECTION",
              entityId: 5,
              name: "original.png",
              downloadPath:
                "/api/branches/1/reinspections/8/original/evidence/12",
            },
          ],
        },
      });
    if (path === "/api/auth/csrf") {
      if (state.csrf) await state.csrf;
      return route
        .fulfill({
          json: { headerName: "X-CSRF-TOKEN", csrfToken: "test-token" },
        })
        .catch(() => {});
    }
    if (path === "/api/branches/1/reinspections/8/conclude") {
      state.posts.push(req.postDataJSON());
      if (state.fail)
        return route.fulfill({
          status: 503,
          json: { title: "PRIVATE-SQL-ERROR" },
        });
      return route.fulfill({
        headers: { ETag: tag },
        json: {
          id: 8,
          reworkId: 2,
          branchId: 1,
          roomId: 3,
          cleaningTaskId: 4,
          state: "PASSED",
          reworkState: "COMPLETED",
          sourceETag: tag,
          reworkETag: tag,
          operationId: req.headers()["idempotency-key"],
          effectiveReadiness: "BLOCKED",
          messageCode: "MSG17",
          notification: "PENDING_DELIVERY",
          contract: "FE05_REINSPECTION_CONCLUSION_V1",
        },
      });
    }
    return route.fulfill({ status: 404, json: { code: "NOT_FOUND" } });
  });
  try {
    await page.goto("https://hotelops.test/reinspections/1/8/execution");
    await page.addScriptTag({ content: bundle.outputFiles[0].text });
    await page.getByText("PRIVATE-DEFECT", { exact: true }).waitFor();
    await run(page, state);
    assert.deepEqual(state.errors, []);
  } finally {
    await page.close();
  }
}
async function review(page) {
  await page
    .getByLabel("Result for defect #9", { exact: true })
    .selectOption("FIXED");
  await page
    .getByLabel("Conclusion reason", { exact: true })
    .fill("Checked remediation");
  await page
    .getByRole("button", { name: "Review conclusion", exact: true })
    .click();
}

test("canonical original link is read only and clears private source on session loss", async () => {
  await scenario(async (page, state) => {
    await page
      .getByRole("link", { name: "Original inspection #5", exact: true })
      .click();
    await page.waitForURL("https://hotelops.test/reinspections/1/8/original");
    await page.getByText("PRIVATE-ORIGINAL-NOTE", { exact: true }).waitFor();
    assert.equal(
      await page
        .getByRole("link", { name: "original.png", exact: true })
        .getAttribute("href"),
      "/api/branches/1/reinspections/8/original/evidence/12",
    );
    assert.equal(
      await page
        .getByRole("button", {
          name: /Approve|Reject|Start|Save|Confirm conclusion/,
        })
        .count(),
      0,
    );
    await page
      .getByRole("link", { name: "Return to reinspection", exact: true })
      .click();
    await page.getByText("PRIVATE-REMEDIATION", { exact: false }).waitFor();
    await page
      .getByRole("link", { name: "Original inspection #5", exact: true })
      .click();
    await page.getByText("PRIVATE-ORIGINAL-SUMMARY", { exact: true }).waitFor();
    state.signedIn = false;
    await page.evaluate(() => window.reinspectionTest.revalidate());
    await page.waitForURL("**/login?*");
    assert.equal(
      new URL(page.url()).searchParams.get("returnTo"),
      "/reinspections/1/8/original",
    );
    assert.equal(
      await page.getByText("PRIVATE-ORIGINAL-SUMMARY", { exact: true }).count(),
      0,
    );
    assert.equal(state.posts.length, 0);
  });
});

test("terminal reload shows fresh readiness and recorded decisions without enabling conclusion", async () => {
  await scenario(async (page, state) => {
    state.data = {
      ...data,
      state: "PASSED",
      effectiveReadiness: "READY",
      messageCode: null,
      defects: [
        {
          ...data.defects[0],
          fixed: true,
          note: "Verified after rework",
          reinspectionPhotos: [12],
        },
      ],
      conclusion: {
        id: 8,
        reworkId: 2,
        branchId: 1,
        roomId: 3,
        cleaningTaskId: 4,
        state: "PASSED",
        reworkState: "COMPLETED",
        sourceETag: tag,
        reworkETag: tag,
        operationId: crypto.randomUUID(),
        effectiveReadiness: "BLOCKED",
        messageCode: "MSG17",
        notification: "PENDING_DELIVERY",
        contract: "FE05_REINSPECTION_CONCLUSION_V1",
      },
    };
    await page.evaluate(() => window.reinspectionTest.revalidate());
    await page
      .getByText("Current room readiness: READY.", { exact: true })
      .waitFor();
    await page
      .getByText(
        "Recorded outcome: PASSED; readiness at conclusion: BLOCKED.",
        { exact: true },
      )
      .waitFor();
    await page.getByText("Verified after rework", { exact: true }).waitFor();
    assert.equal(
      await page
        .getByRole("button", { name: "Review conclusion", exact: true })
        .count(),
      0,
    );
    assert.equal(
      await page
        .getByRole("link", { name: "Reinspection photo #12", exact: true })
        .getAttribute("href"),
      "/api/branches/1/reinspections/8/evidence/12",
    );
    state.signedIn = false;
    await page.evaluate(() => window.reinspectionTest.revalidate());
    await page.waitForURL("**/login?*");
    assert.equal(
      await page.getByText("Verified after rework", { exact: true }).count(),
      0,
    );
    assert.equal(state.posts.length, 0);
  });
});
test("canonical reinspection requires every decision and confirms blocked result once", async () => {
  await scenario(async (page, state) => {
    await page
      .getByRole("button", { name: "Review conclusion", exact: true })
      .click();
    assert.equal(
      await page
        .getByRole("button", { name: "Confirm conclusion", exact: true })
        .count(),
      0,
    );
    await review(page);
    await page
      .getByRole("button", { name: "Confirm conclusion", exact: true })
      .click();
    await page
      .getByText("The room remains blocked (MSG17).", { exact: true })
      .waitFor();
    assert.equal(state.posts.length, 1);
    assert.equal(state.posts[0].defects[0].fixed, true);
    assert.equal(
      await page
        .getByRole("button", { name: "Confirm conclusion", exact: true })
        .count(),
      0,
    );
  });
});
test("unknown conclusion outcome locks form and hides raw server detail", async () => {
  await scenario(async (page, state) => {
    state.fail = true;
    await review(page);
    await page
      .getByRole("button", { name: "Confirm conclusion", exact: true })
      .click();
    await page.getByText(/Conclusion outcome is unconfirmed/).waitFor();
    assert.equal(await page.getByText("PRIVATE-SQL-ERROR").count(), 0);
    assert.equal(
      await page
        .getByRole("button", { name: "Review conclusion", exact: true })
        .isDisabled(),
      true,
    );
    assert.equal(state.posts.length, 1);
  });
});
test("session invalidation during CSRF removes private source and prevents conclusion", async () => {
  await scenario(async (page, state) => {
    let release;
    state.csrf = new Promise((resolve) => {
      release = resolve;
    });
    await review(page);
    await page
      .getByRole("button", { name: "Confirm conclusion", exact: true })
      .click();
    state.signedIn = false;
    await page.evaluate(() => window.reinspectionTest.revalidate());
    release();
    await page.waitForURL("**/login?*");
    assert.equal(
      await page.getByText("PRIVATE-DEFECT", { exact: true }).count(),
      0,
    );
    assert.equal(state.posts.length, 0);
  });
});
