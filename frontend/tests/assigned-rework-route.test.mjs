import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";

const session = {
  user: { id: "42", username: "staff", name: "Staff" },
  expiresAt: "2026-11-16T12:00:00Z",
  absoluteExpiresAt: "2026-11-16T20:00:00Z",
  mustChangePassword: false,
  csrfToken: "test-token",
};
const source = {
  id: 12,
  branchId: 3,
  cleaningTaskId: 4,
  inspectionId: 5,
  roomId: 6,
  assigneeId: 42,
  state: "PENDING_ACCEPTANCE",
  dueAtUtc: null,
  sourceETag: '"0000000000000001"',
  inspectionETag: '"0000000000000002"',
  capability: "READ_ONLY",
  sourceAuthority: "FE05_COMMITTED_V1",
  defects: [
    {
      id: 7,
      description: "PRIVATE-DEFECT",
      remediation: null,
      inspectionAttachmentIds: [8],
      remediationAttachmentIds: [],
    },
  ],
};
const bundle = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `
import React from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { AuthProvider } from "./src/auth/AuthProvider";
import { AuthRouteHost } from "./src/auth/AuthRouteHost";
import { createAuthStore } from "./src/auth/authStore";
import { createAuthApi } from "./src/auth/authApi";
const store = createAuthStore(createAuthApi(window.fetch.bind(window)));
window.reworkTest = { revalidate: () => store.revalidate() };
const host = <AuthRouteHost />;
createRoot(document.getElementById("root")).render(<BrowserRouter><AuthProvider store={store} fallback={host}>{host}</AuthProvider></BrowserRouter>);
`,
  },
  bundle: true,
  outdir: ".test-output/assigned-rework-memory-only",
  write: false,
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"test"', "import.meta.env": "{}" },
});
let browser;
before(async () => {
  const executablePath =
    process.env.HOTELOPS_TEST_BROWSER ||
    [
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
      "C:/Program Files/Google/Chrome/Application/chrome.exe",
    ].find(existsSync);
  browser = await chromium.launch({
    headless: true,
    ...(executablePath ? { executablePath } : {}),
  });
});
after(async () => browser?.close());
async function scenario(run, writable = false) {
  const page = await browser.newPage();
  const state = {
    signedIn: true,
    status: 200,
    data: structuredClone(source),
    calls: [],
    errors: [],
  };
  if (writable) state.data.capability = "ACCEPT_START";
  page.on("pageerror", (e) => state.errors.push(e.message));
  await page.route("**/*", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (request.resourceType() === "document")
      return route.fulfill({
        contentType: "text/html",
        body: '<div id="root"></div>',
      });
    state.calls.push({ path, method: request.method() });
    if (path === "/api/auth/session")
      return route.fulfill({
        status: state.signedIn ? 200 : 401,
        json: state.signedIn ? session : { code: "AUTHENTICATION_REQUIRED" },
      });
    if (path === "/api/branches/3/reworks/12/execution")
      return route.fulfill({
        status: state.status,
        json: state.data,
        headers: { ETag: source.sourceETag },
      });
    if (path === "/api/auth/csrf")
      return route.fulfill({
        json: { csrfToken: "test-anonymous-token", headerName: "X-CSRF-TOKEN" },
      });
    if (
      writable &&
      request.method() === "POST" &&
      /\/(accept|start)$/.test(path)
    ) {
      const action = path.endsWith("/accept") ? "accept" : "start";
      assert.equal(request.headers()["x-csrf-token"], "test-anonymous-token");
      assert.equal(request.headers()["if-match"], source.sourceETag);
      assert.equal(
        request.postDataJSON().note,
        action === "accept" ? "Kết quả làm lại đã chấp nhận"
          : state.data.state === "ON_HOLD" ? "Đã tiếp tục làm lại sau khi hết ca" : "Đã bắt đầu làm lại",
      );
      state.data = {
        ...state.data,
        state: action === "accept" ? "ACCEPTED" : "IN_PROGRESS",
        capability: action === "accept" ? "ACCEPT_START" : "READ_ONLY",
      };
      return route.fulfill({
        json: {
          id: 12,
          branchId: 3,
          cleaningTaskId: 4,
          inspectionId: 5,
          assigneeId: 42,
          action,
          state: state.data.state,
          operationId: request.headers()["idempotency-key"],
          sourceETag: source.sourceETag,
          occurredAtUtc: "2026-11-15T03:00:00Z",
          intervalId: action === "start" ? 19 : null,
          publicationId: null,
          readiness: "REWORK_REQUIRED",
          notification: "NOT_REQUIRED",
          contract: "FE05_COMMITTED_V1",
        },
        headers: { ETag: source.sourceETag },
      });
    }
    return route.fulfill({ status: 404, json: { code: "NOT_FOUND" } });
  });
  try {
    await page.goto("https://hotelops.test/cleaning/reworks/12?branchId=3");
    await page.addScriptTag({ content: bundle.outputFiles[0].text });
    await run(page, state);
    assert.deepEqual(state.errors, []);
    if (!writable)
      assert.equal(
        state.calls.some((c) => c.method !== "GET"),
        false,
      );
  } finally {
    await page.close();
  }
}

test("end-of-shift hold resumes only with the server capability", async () => {
  await scenario(async (page, state) => {
    await page.getByText("PRIVATE-DEFECT", { exact: true }).waitFor();
    state.data = { ...source, state: "ON_HOLD", capability: "READ_ONLY" };
    await page.getByRole("button", { name: "Tải lại làm lại" }).click();
    await page.getByText("Làm lại #12: on hold.", { exact: true }).waitFor();
    assert.equal(await page.getByRole("button", { name: "Tiếp tục làm lại", exact: true }).count(), 0);
    state.data.capability = "RESUME_REWORK";
    await page.getByRole("button", { name: "Tải lại làm lại" }).click();
    await page.getByRole("button", { name: "Tiếp tục làm lại", exact: true }).click();
    await page.getByText("Làm lại #12: in progress.", { exact: true }).waitFor();
    assert.deepEqual(state.calls.filter(c => c.method === "POST").map(c => c.path), ["/api/branches/3/reworks/12/start"]);
  }, true);
});

test("canonical receive workflow accepts and starts using current CSRF and committed receipts", async () => {
  await scenario(async (page, state) => {
    await page
      .getByRole("button", { name: "Tiếp nhận làm lại", exact: true })
      .click();
    await page
      .getByRole("button", { name: "Bắt đầu làm lại", exact: true })
      .click();
    await page.getByText("Làm lại #12: in progress.", { exact: true }).waitFor();
    assert.deepEqual(
      state.calls.filter((c) => c.method === "POST").map((c) => c.path),
      ["/api/branches/3/reworks/12/accept", "/api/branches/3/reworks/12/start"],
    );
    assert.equal(
      await page
        .getByRole("button", {
          name: /Tiếp nhận làm lại|Bắt đầu làm lại|Hoàn tất làm lại/,
        })
        .count(),
      0,
    );
  }, true);
});
test("canonical assigned source displays committed defects, then clears them on session loss", async () => {
  await scenario(async (page, state) => {
    await page.getByText("PRIVATE-DEFECT", { exact: true }).waitFor();
    assert.equal(
      await page
        .getByRole("link", { name: "Xem ảnh lỗi 8" })
        .getAttribute("href"),
      "/api/branches/3/reworks/12/evidence/8",
    );
    assert.equal(
      await page.getByRole("button", { name: /Tiếp nhận|Bắt đầu|Hoàn tất/ }).count(),
      0,
    );
    state.signedIn = false;
    await page.evaluate(() => window.reworkTest.revalidate());
    await page.waitForURL(/\/login/);
    assert.equal(
      await page.getByText("PRIVATE-DEFECT", { exact: true }).count(),
      0,
    );
  });
});
test("executor terminal acknowledgement is read-only, separates readiness and clears on session loss", async () => {
  await scenario(async (page, state) => {
    for (const ended of ["COMPLETED", "FAILED"]) {
      state.data = { ...source, state: ended, conclusion: { reinspectionId: 15, state: ended === "COMPLETED" ? "PASSED" : "FAILED",
        effectiveReadiness: "PENDING_CLEANING", readinessAtConclusion: "BLOCKED" } };
      await page.getByRole("button", { name: "Tải lại làm lại" }).click();
      await page.getByText(`Kiểm tra lại #15: ${ended === "COMPLETED" ? "passed" : "failed"}.`, { exact: true }).waitFor();
      await page.getByText("Mức sẵn sàng của phòng hiện tại: Chờ vệ sinh.", { exact: true }).waitFor();
      await page.getByText("Mức sẵn sàng khi kết luận: BLOCKED.", { exact: true }).waitFor();
      assert.equal(await page.getByRole("button", { name: /Tiếp nhận làm lại|Bắt đầu làm lại|Nộp kết quả làm lại|Kết luận|Conclude/ }).count(), 0);
    }
    state.signedIn = false;
    await page.evaluate(() => window.reworkTest.revalidate());
    await page.waitForURL(/\/login/);
    assert.equal(await page.getByRole("region", { name: "Kết quả kiểm tra lại" }).count(), 0);
  });
});
test("source 401 invalidates the current session and removes prior details", async () => {
  await scenario(async (page, state) => {
    await page.getByText("PRIVATE-DEFECT", { exact: true }).waitFor();
    state.status = 401;
    state.data = { code: "AUTHENTICATION_REQUIRED" };
    await page.getByRole("button", { name: "Tải lại làm lại" }).click();
    await page.waitForURL(/\/login/);
    assert.equal(
      await page.getByText("PRIVATE-DEFECT", { exact: true }).count(),
      0,
    );
  });
});
test("unsupported lifecycle capability is not displayed as pending acceptance", async () => {
  await scenario(async (page, state) => {
    await page.getByText("PRIVATE-DEFECT", { exact: true }).waitFor();
    state.data = {
      ...source,
      state: "IN_PROGRESS",
      capability: "PROVISIONAL_NOT_ACCEPTED",
    };
    await page.getByRole("button", { name: "Tải lại làm lại" }).click();
    await page.getByRole("alert").waitFor();
    assert.equal(
      await page.getByText("PRIVATE-DEFECT", { exact: true }).count(),
      0,
    );
    assert.equal(await page.getByText(/đang chờ tiếp nhận\./).count(), 0);
  });
});
