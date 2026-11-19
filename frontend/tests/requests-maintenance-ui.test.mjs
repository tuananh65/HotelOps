import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { mkdirSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";

const bundle = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `
import React from 'react'; import {createRoot} from 'react-dom/client'; import {BrowserRouter, Routes, Route} from 'react-router-dom';
import {AuthProvider} from './src/auth/AuthProvider'; import {InternalRequestHistoryPage} from './src/internalRequests/HistoryPage';
import {Dev2Workspace} from './src/dev2/Workspace'; import {CacaoShell} from './src/shared-host/CacaoShell';
import './src/styles.css'; import './src/styles/operations.css'; import './src/dev2/dev2.css'; import './src/styles/cacao.css'; import './src/styles/design-system.css';
createRoot(document.getElementById('root')).render(<BrowserRouter><AuthProvider><CacaoShell><Routes>
<Route path="/internal-requests" element={<InternalRequestHistoryPage/>}/><Route path="/operations/*" element={<Dev2Workspace/>}/>
</Routes></CacaoShell></AuthProvider></BrowserRouter>);`,
  },
  bundle: true,
  write: false,
  outfile: "test.js",
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"development"', "import.meta.env": "{}" },
});
let browser;
before(async () => {
  browser = await chromium.launch({
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    headless: true,
  });
  mkdirSync("../artifacts/requests-maintenance-ui/screenshots", {
    recursive: true,
  });
});
after(async () => browser?.close());
const lines = [
  {
    requestLineId: 1,
    itemName: "Xà phòng",
    unitName: "Chai",
    requestedQty: 5,
    approvedQty: 3,
    issuedQty: 2,
    isActive: true,
  },
];
async function fixture(
  path,
  check,
  { width = 1280, branches = [7], write = false, materialCreate = false } = {},
) {
  const page = await browser.newPage({ viewport: { width, height: 900 } }),
    calls = [],
    errors = [];
  const state = { denied: false, canDecide: false };
  page.setDefaultTimeout(12000);
  page.on("pageerror", (e) => errors.push(e.message));
  await page.route("**/*", async (route) => {
    const request = route.request(),
      url = new URL(request.url()),
      p = url.pathname;
    calls.push({ path: p, query: url.search, method: request.method() });
    const json = (data, status = 200) =>
      route.fulfill({
        status,
        contentType: "application/json",
        body: JSON.stringify(data),
      });
    if (request.resourceType() === "document")
      return route.fulfill({
        contentType: "text/html",
        body: '<html lang="vi"><meta charset="utf-8"><div id="root"></div></html>',
      });
    if (p === "/api/auth/session")
      return json({
        user: { id: "1", username: "hung.nguyen", name: "Nguyễn Văn Hùng" },
        expiresAt: "2099-01-01T00:00:00Z",
        absoluteExpiresAt: "2099-01-02T00:00:00Z",
        mustChangePassword: false,
        csrfToken: "test",
      });
    if (p === "/api/auth/navigation")
      return json({
        paths: [
          "/internal-requests",
          "/operations/maintenance",
          "/operations/material-requests",
          ...(materialCreate ? ["/material-requests/new"] : []),
        ],
      });
    if (p === "/api/auth/navigation/operation-grants")
      return json({
        grants: branches.flatMap((branchId) =>
          ["MAINTENANCE_VIEW", "MR_VIEW", "IR_VIEW", ...(materialCreate ? ["MR_CREATE"] : [])].map((permission) => ({
            branchId,
            permission,
            scope: "BRANCH",
          })),
        ),
      });
    if (p === "/api/internal-requests/options")
      return json({
        statuses: ["CLOSED", "IN_PROGRESS"],
        types: [],
        priorities: [],
        departments: [],
      });
    if (p === "/api/internal-requests")
      return json(
        {
          items: [
            {
              id: 7,
              number: "IR-20260914-001",
              branchId: 7,
              branchName: "Khách sạn Đức Vượng - Chi nhánh Bùi Viện",
              receivingDepartmentId: 2,
              receivingDepartmentName: "Kho & Cung ứng",
              subject: "Xin cấp thêm xà phòng cho buồng phòng",
              status: "CLOSED",
              deadline: "2026-09-14T15:52:00Z",
              overdue: false,
              currentAssigneeId: null,
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 25,
          snapshotUtc: "2026-11-19T00:00:00Z",
        },
        state.denied ? 403 : 200,
      );
    if (p === "/api/internal-requests/7/timeline")
      return json({
        requestId: 7,
        events: [],
        eventTotalCount: 0,
        assignments: [],
        assignmentTotalCount: 0,
        feedback: null,
      });
    if (p === "/api/auth/csrf") return json({ csrfToken: "test" });
    if (
      p === "/api/internal-requests/7/decision" &&
      request.method() === "POST"
    ) {
      assert.equal(request.headers()["x-csrf-token"], "test");
      assert.equal(request.headers()["if-match"], '"AAAAAAAAAAA="');
      assert.equal(request.postDataJSON().accept, true);
      return json({}, 412);
    }
    if (p === "/api/internal-requests/7/decision")
      return json({
        requestId: 7,
        subject: "Xin cấp thêm xà phòng cho buồng phòng",
        description: "Xà phòng trên xe đẩy sắp hết.",
        status: state.canDecide ? "PENDING_ACCEPTANCE" : "CLOSED",
        submittedAt: "2026-09-14T14:52:00Z",
        deadline: "2026-09-14T15:52:00Z",
        desiredDeadline: null,
        ageSeconds: 100000,
        version: "AAAAAAAAAAA=",
        canDecide: state.canDecide,
      });
    if (p === "/api/maintenance-works")
      return json(
        {
          items: [
            {
              maintenanceWorkId: 9,
              branchId: 7,
              workNo: "MW-009",
              title: "Sửa điều hòa phòng 101",
              source: "INCIDENT",
              roomNumber: "101",
              priorityName: "Cao",
              assigneeName: "Trần Bình",
              status: "IN_PROGRESS",
              deadline: { dueAt: "2026-11-20T08:00:00Z", isOverdue: false },
            },
          ],
          page: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        },
        state.denied ? 403 : 200,
      );
    if (p === "/api/maintenance-works/9")
      return json({
        maintenanceWorkId: 9,
        branchId: 7,
        workNo: "MW-009",
        title: "Sửa điều hòa phòng 101",
        status: "IN_PROGRESS",
        source: "INCIDENT",
        roomNumber: "101",
        assigneeName: "Trần Bình",
        priorityName: "Cao",
        maintenanceWorkLogs: [],
        deadline: { dueAt: "2026-11-20T08:00:00Z", isOverdue: false },
        materialUsageAvailable: false,
      });
    if (p === "/api/material-requests/creation-branches") return json([{branchId:7,name:"Bùi Viện"}]);
    if (p === "/api/material-requests")
      return json(
        {
          items: [
            {
              requestId: 4,
              requestNo: "MR-004",
              branchId: 7,
              rawStatus: "APPROVED",
              requesterId: 2,
              assignedApproverUserId: 3,
              createdAt: "2026-11-19T00:00:00Z",
              neededAt: "2026-11-20T08:00:00Z",
              quantities: lines,
            },
          ],
          total: 1,
          page: 1,
          pageSize: 20,
        },
        state.denied ? 403 : 200,
      );
    if (p === "/api/material-requests/4")
      return json({
        requestId: 4,
        requestNo: "MR-004",
        branchId: 7,
        requesterId: 2,
        rawStatus: "APPROVED",
        purpose: "Thay vật tư phòng 101",
        createdAt: "2026-11-19T00:00:00Z",
        neededAt: "2026-11-20T08:00:00Z",
        lines,
        cycles: [],
        decisions: [],
        fulfillments: [],
        returns: [],
      });
    return json({}, 403);
  });
  try {
    await page.goto("https://ui.test" + path);
    for (const f of bundle.outputFiles.filter((f) => f.path.endsWith(".css")))
      await page.addStyleTag({ content: f.text });
    await page.addScriptTag({
      content: bundle.outputFiles.find((f) => f.path.endsWith(".js")).text,
    });
    await check(page, calls, state);
    assert.deepEqual(errors, []);
    assert.ok(
      calls.every(
        (c) =>
          c.method === "GET" ||
          (write && c.path === "/api/internal-requests/7/decision"),
      ),
      "read screens must never mutate",
    );
    assert.ok(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth + 1,
      ),
      "no page-wide overflow",
    );
  } finally {
    await page.close();
  }
}
test("internal requests: compact table, readable detail, no denied command triggers", () =>
  fixture("/internal-requests", async (p) => {
    await p.getByRole("button", { name: "IR-20260914-001" }).waitFor();
    assert.equal(await p.locator("thead th").count(), 4);
    await p.screenshot({
      path: "../artifacts/requests-maintenance-ui/screenshots/internal-list.png",
    });
    await p.getByRole("button", { name: "IR-20260914-001" }).click();
    await p
      .getByRole("heading", { name: "Xin cấp thêm xà phòng cho buồng phòng" })
      .waitFor();
    await p.getByText("21:52 14/09/2026", { exact: false }).waitFor();
    assert.equal(
      await p
        .getByRole("button", { name: "Hoàn tất yêu cầu", exact: true })
        .count(),
      0,
    );
    assert.equal(
      await p
        .getByRole("button", { name: "Yêu cầu bàn giao", exact: true })
        .count(),
      0,
    );
    assert.equal(await p.getByText("100000", { exact: false }).count(), 0);
    await p.screenshot({
      path: "../artifacts/requests-maintenance-ui/screenshots/internal-detail.png",
    });
  }));
test("maintenance: auto branch, no naked request, compact table and safe read detail", () =>
  fixture("/operations/maintenance", async (p, calls) => {
    await p.getByRole("link", { name: "MW-009", exact: true }).waitFor();
    assert.ok(
      calls
        .filter((c) => c.path === "/api/maintenance-works")
        .every((c) => new URLSearchParams(c.query).get("branchId") === "7"),
    );
    assert.equal(await p.locator("thead th").count(), 5);
    assert.equal(await p.getByLabel("Thao tác", { exact: true }).count(), 0);
    await p.screenshot({
      path: "../artifacts/requests-maintenance-ui/screenshots/maintenance.png",
    });
    await p.getByRole("link", { name: "MW-009", exact: true }).click();
    await p.getByRole("heading", { name: /MW-009 —/ }).waitFor();
    assert.equal(
      await p
        .getByRole("button", { name: /Bắt đầu|Nộp kết quả|Xác minh công việc/ })
        .count(),
      0,
    );
  }));
test("materials: auto branch, quantities remain separate, readable detail and history", () =>
  fixture("/operations/material-requests", async (p, calls) => {
    await p.getByRole("link", { name: "MR-004", exact: true }).waitFor();
    assert.ok(
      calls
        .filter((c) => c.path === "/api/material-requests")
        .every((c) => new URLSearchParams(c.query).get("branchId") === "7"),
    );
    await p.getByText("1 dòng vật tư", { exact: true }).click();
    await p.getByText("Xà phòng (Chai)", { exact: true }).waitFor();
    await p.getByRole("link", { name: "MR-004", exact: true }).click();
    await p.getByRole("heading", { name: "MR-004", exact: true }).waitFor();
    assert.equal(await p.locator('a[href*="action=A004A-040"]').count(), 0);
    assert.equal(await p.locator('a[href="/inventory/return-requests"]').count(), 0);
    await p.getByRole("cell", { name: "Xà phòng", exact: true }).waitFor();
    assert.equal(
      await p.getByRole("button", { name: /Duyệt yêu cầu/ }).count(),
      0,
    );
    await p.screenshot({
      path: "../artifacts/requests-maintenance-ui/screenshots/material-detail.png",
    });
  }));
test("multiple branches require selection before loading a list", () =>
  fixture(
    "/operations/maintenance",
    async (p, calls) => {
      await p.getByRole("heading", { name: "Chọn chi nhánh" }).waitFor();
      assert.equal(
        calls.filter((c) => c.path === "/api/maintenance-works").length,
        0,
      );
      await p.getByLabel("Chi nhánh", { exact: true }).selectOption("7");
      await p.getByRole("link", { name: "Xem dữ liệu" }).click();
      await p.getByRole("link", { name: "MW-009", exact: true }).waitFor();
    },
    { branches: [7, 8] },
  ));
test("denied refresh removes old material results", () =>
  fixture("/operations/material-requests", async (p, _calls, state) => {
    await p.getByRole("link", { name: "MR-004", exact: true }).waitFor();
    state.denied = true;
    await p
      .getByRole("button", { name: "Tải lại dữ liệu", exact: true })
      .click();
    await p
      .getByRole("alert")
      .filter({ hasText: "Bạn không có quyền" })
      .waitFor();
    assert.equal(
      await p.getByRole("link", { name: "MR-004", exact: true }).count(),
      0,
    );
  }));
test("maintenance table scrolls inside its container on mobile", () =>
  fixture(
    "/operations/maintenance",
    async (p) => {
      await p.getByRole("link", { name: "MW-009", exact: true }).waitFor();
      await p.screenshot({
        path: "../artifacts/requests-maintenance-ui/screenshots/maintenance-mobile.png",
      });
    },
    { width: 390 },
  ));

test("authorized request action retains CSRF/version checks and never replays a stale write", () =>
  fixture(
    "/internal-requests",
    async (p, calls, state) => {
      await p.getByRole("button", { name: "IR-20260914-001" }).waitFor();
      state.canDecide = true;
      await p.getByRole("button", { name: "IR-20260914-001" }).click();
      await p
        .getByRole("button", {
          name: "Tiếp nhận hoặc từ chối yêu cầu",
          exact: true,
        })
        .click();
      const dialog = p.getByRole("dialog");
      await dialog
        .getByRole("button", { name: "Tiếp nhận yêu cầu", exact: true })
        .click();
      await p
        .getByRole("status")
        .filter({ hasText: "Yêu cầu đã đổi. Tải lại trước khi quyết định." })
        .waitFor();
      assert.equal(calls.filter((c) => c.method === "POST").length, 1);
      assert.equal(
        await p
          .getByRole("button", { name: "Tiếp nhận yêu cầu", exact: true })
          .count(),
        0,
      );
    },
    { write: true },
  ));

test("an ungranted branch never dispatches a business list read", () =>
  fixture("/operations/maintenance?branchId=99", async (p, calls) => {
    await p
      .getByRole("alert")
      .filter({ hasText: "Bạn không có quyền sử dụng chức năng này." })
      .waitFor();
    assert.equal(
      calls.filter((c) => c.path === "/api/maintenance-works").length,
      0,
    );
  }));

for(const dirty of [false,true])test('material create close restores list without posting; dirty='+dirty,()=>fixture('/operations/material-requests?branchId=7',async(p,calls)=>{
 await p.getByRole('link',{name:'MR-004',exact:true}).waitFor();await p.getByRole('link',{name:'Tạo mới',exact:true}).click();const d=p.getByRole('dialog');await d.waitFor();
 if(dirty){const input=d.locator('textarea:not([disabled]),input[type="text"]:not([disabled])').first();await input.fill('Nội dung đang nhập');p.once('dialog',dialog=>dialog.accept());}
 await d.getByRole('button',{name:'Đóng',exact:true}).click();await p.getByRole('link',{name:'MR-004',exact:true}).waitFor();assert.equal(await p.getByRole('dialog').count(),0);assert.equal(calls.filter(c=>c.method==='POST').length,0);assert.equal(new URL(p.url()).searchParams.get('branchId'),'7');
},{materialCreate:true}));
