import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { mkdirSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";

// Grants and menu captured read-only from lan.dang; resource responses are mocked.
const own = [
  "ATTENDANCE_RECORD",
  "CLEANING_EXECUTE",
  "CLEANING_VIEW",
  "DEFECT_RESOLVE",
  "INCIDENT_VIEW",
  "IR_CREATE",
  "IR_PROCESS",
  "IR_VIEW",
  "MR_CREATE",
  "MR_VIEW",
  "SHIFT_VIEW",
  "TASK_EXECUTE",
  "TASK_VIEW",
];
const grants = [
  ...own.map((permission) => ({ permission, branchId: 1, scope: "OWN" })),
  ...["INCIDENT_CREATE", "ROOM_VIEW"].map((permission) => ({
    permission,
    branchId: 1,
    scope: "BRANCH",
  })),
];
const paths = [
  "/profile",
  "/change-password",
  "/notifications",
  "/room-directory",
  "/rooms/status",
  "/hotel",
  "/operational-tasks",
  "/operations/material-requests",
  "/material-requests/new",
  "/internal-requests",
  "/internal-requests/new",
  "/cleaning/tasks",
  "/rework",
  "/workday",
  "/workday/scheduled",
  "/my-time-log",
  "/time-corrections",
  "/incidents/new",
];
const bundle = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `
import React from 'react';import {createRoot} from 'react-dom/client';import {BrowserRouter,Routes,Route} from 'react-router-dom';
import {AuthProvider,useAuth} from './src/auth/AuthProvider';import {HomeNavigation} from './src/shared-host/HomeNavigation';
import {TaskProgressPanel} from './src/operationalTasks/TaskProgressPanel';
import {Dev2Workspace} from './src/dev2/Workspace';import Cleaning from './src/screens/housekeeping/CleaningTaskListScreen';
import Header from './src/screens/housekeeping/AssignedCleaningHeader';import Rework from './src/screens/housekeeping/ReworkTaskDetailScreen';
import Execution from './src/screens/housekeeping/CleaningExecutionPanel';
import CleaningProgress from './src/screens/housekeeping/CleaningProgressPanel';
import {MaterialListView} from './src/dev2/MaterialListView';
import {MaterialTaskLinks} from './src/dev2/MaterialTaskLinks';
import {ReportAccessLink} from './src/insights/ReportsPage';
import {permittedOperations} from './src/dev2/AuthorizedOperations';
import './src/styles.css';import './src/styles/design-system.css';
window.permittedOperations=permittedOperations;
function Progress(){const {state}=useAuth();return state.authorityGeneration ? <TaskProgressPanel branch={1} task={9} owner={state.authorityGeneration}/> : null;}
createRoot(document.getElementById('root')).render(<BrowserRouter><AuthProvider><Routes>
<Route path="/home" element={<><h1>Trang chủ</h1><HomeNavigation/><ReportAccessLink/></>}/>
<Route path="/legacy-cleaning" element={<Execution branch="1" task="1"/>}/>
<Route path="/legacy-progress" element={<CleaningProgress branch="1" task="1"/>}/>
<Route path="/material-links" element={<><MaterialTaskLinks record={{requestId:15,branchId:1,availableLinkKinds:[]}}/><MaterialTaskLinks kind="maintenance" record={{requestId:15,branchId:1,availableLinkKinds:['maintenance']}}/></>}/>
<Route path="/cleaning/tasks" element={<Cleaning/>}/><Route path="/rework" element={<Cleaning kindOnly="rework"/>}/>
<Route path="/rework/:id" element={<Rework/>}/>
<Route path="/assigned" element={<Header branch="1" task="1" assignment="1"/>}/>
<Route path="/task-progress" element={<Progress/>}/>
<Route path="/operations/*" element={<Dev2Workspace/>}/>
</Routes></AuthProvider></BrowserRouter>);`,
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
  mkdirSync("../artifacts/lan-dang-access-review/screenshots", {
    recursive: true,
  });
});
after(async () => browser?.close());
async function fixture(path, check, options = {}) {
  const page = await browser.newPage({
      viewport: { width: 1280, height: 850 },
    }),
    calls = [],
    errors = [];
  page.setDefaultTimeout(10000);
  page.on("pageerror", (e) => errors.push(e.message));
  await page.route("**/*", async (route) => {
    const req = route.request(),
      u = new URL(req.url()),
      p = u.pathname;
    calls.push({ path: p, method: req.method() });
    const json = (body, status = 200) =>
      route.fulfill({
        status,
        contentType: "application/json",
        body: JSON.stringify(body),
      });
    if (p === "/api/branches/1/operational-tasks/9/progress")
      return json(
        { title: options.forbiddenProgress ? "FORBIDDEN" : "SCHEMAREQUIRED" },
        options.forbiddenProgress ? 403 : 503,
      );
    if (p === "/api/branches/1/cleaning-tasks/1/execution")
      return json({ title: "SCHEMAREQUIRED" }, 503);
    if (p === "/api/branches/1/cleaning-tasks/1/progress")
      return json({ title: "SCHEMAREQUIRED" }, 503);
    if (p === "/api/branches/1/cleaning-tasks/1/record")
      return options.deniedRecord
        ? json({ title: "FORBIDDEN" }, 403)
        : json({
            id: 1,
            branchId: 1,
            roomNumber: "201",
            taskNumber: "CLN-OLD",
            recordedStatus: "PENDING",
            cleaningType: "Dọn phòng",
            instructions: "Hồ sơ vệ sinh cũ",
            dueAt: "2026-10-16T12:30:00Z",
          });
    if (p === "/api/branches/1/reworks/8/record")
      return options.deniedRecord
        ? json({ title: "FORBIDDEN" }, 403)
        : json({
            id: 8,
            branchId: 1,
            roomNumber: "502",
            taskNumber: "CLN-20261016-010",
            inspectionId: 13,
            recordedStatus: "IN_PROGRESS",
            reason: "Vết bẩn trên bàn cần làm sạch",
            note: null,
            requestedAt: "2026-10-16T08:00:00Z",
            completedAt: null,
          });
    if (req.resourceType() === "document")
      return route.fulfill({
        contentType: "text/html",
        body: '<html lang="vi"><meta charset="utf-8"><div id="root"></div></html>',
      });
    if (p === "/api/auth/session")
      return json({
        user: { id: "6", username: "lan.dang", name: "Đặng Thị Lan" },
        expiresAt: "2099-01-01T00:00:00Z",
        absoluteExpiresAt: "2099-01-02T00:00:00Z",
        mustChangePassword: false,
        csrfToken: "test",
      });
    if (p === "/api/auth/navigation") return json({ paths });
    if (p === "/api/auth/navigation/operation-grants")
      return json({
        grants: grants.filter(
          (g) => !options.readOnly || g.permission !== "CLEANING_EXECUTE",
        ),
      });
    if (p === "/api/rooms/workspace-options")
      return json([
        {
          id: 1,
          name: "Khách sạn Đức Vượng - Chi nhánh Bùi Viện",
          isPrimary: true,
          canViewRooms: true,
          canMonitor: false,
          canCreate: false,
          canReadOwn: true,
        },
      ]);
    if (p.endsWith("/my-cleaning-assignments"))
      return json({
        items: [
          {
            id: 1,
            branchId: 1,
            assignmentId: 1,
            roomNumber: "201",
            taskNumber: "CLN-20261016-001",
            recordedStatus: "PENDING",
          },
        ],
        nextCursor: null,
      });
    if (p.endsWith("/my-rework-assignments"))
      return json({
        items: [
          {
            id: 8,
            branchId: 1,
            roomNumber: "502",
            taskNumber: "CLN-20261016-010",
            recordedStatus: "IN_PROGRESS",
          },
        ],
        nextCursor: null,
      });
    if (p === "/api/branches/1/cleaning-tasks/1")
      return json({
        id: 1,
        branchId: 1,
        roomId: 2,
        roomNumber: "201",
        taskNumber: "CLN-20261016-001",
        recordedStatus: "IN_PROGRESS",
        assignmentId: 1,
      });
    if (p.endsWith("/reworks/8/execution"))
      return json({ title: "DUPLICATE_REWORK_SOURCE" }, 409);
    if (p === "/api/incidents/41")
      return options.foreign
        ? json({ title: "NOT_FOUND" }, 404)
        : json({
            incidentId: 41,
            branchId: 1,
            incidentNo: "INC-LAN-41",
            title: "Sự cố do Lan báo",
            status: "OPEN",
            canCancelTriage: false,
            canManageRoomBlocks: false,
            canAssign: false,
            reportedBy: 6,
            assetId: 31,
            itemId: 32,
            maintenanceWorks: [{ maintenanceWorkId: 33, warehouseId: 34 }],
            attachments: [],
          });
    if (p === "/api/incidents/creation-branches")
      return json([{ branchId: 1, name: "Bùi Viện" }]);
    if (p.includes("/evidence"))
      return json({
        items: [],
        total: 0,
        page: 1,
        pageSize: 20,
        canAdd: false,
      });
    return json({}, 403);
  });
  try {
    await page.goto("https://lan.test" + path);
    for (const f of bundle.outputFiles.filter((f) => f.path.endsWith(".css")))
      await page.addStyleTag({ content: f.text });
    await page.addScriptTag({
      content: bundle.outputFiles.find((f) => f.path.endsWith(".js")).text,
    });
    await check(page, calls);
    assert.deepEqual(errors, []);
  } finally {
    await page.close();
  }
}

test("Lan menu includes exactly authorized entries and no manager/warehouse/inspection entry", () =>
  fixture("/home", async (p) => {
    await p.getByRole("link", { name: "Hồ sơ cá nhân", exact: true }).waitFor();
    const actual = await p
      .locator("a")
      .evaluateAll((nodes) => nodes.map((n) => n.getAttribute("href")));
    assert.deepEqual(actual.sort(), [...paths].sort());
    await p.screenshot({
      path: "../artifacts/lan-dang-access-review/screenshots/menu.png",
      fullPage: true,
    });
  }));
test("Lan own cleaning auto-loads and does not expose creation or branch monitor", () =>
  fixture("/cleaning/tasks", async (p, calls) => {
    await p
      .getByRole("link", { name: "CLN-20261016-001", exact: true })
      .waitFor();
    assert.equal(
      await p
        .getByRole("button", { name: "Tạo công việc", exact: true })
        .count(),
      0,
    );
    assert.equal(
      calls.some((c) => c.path.includes("cleaning-monitor")),
      false,
    );
  }));
test("own rework loads immediately and returns to rework instead of cleaning", () =>
  fixture("/rework", async (p) => {
    await p.getByRole("link", { name: "CLN-20261016-010 · Làm lại 8" }).click();
    await p
      .getByText("Có nhiều phiếu làm lại đang mở", { exact: false })
      .waitFor();
    await p
      .getByText("Vết bẩn trên bàn cần làm sạch", { exact: true })
      .waitFor();
    assert.equal(await p.getByRole("alert").count(), 0);
    await p.screenshot({
      path: "../artifacts/lan-workflow-demo/archived-rework.png",
      fullPage: true,
    });
    await p.getByRole("link", { name: "Danh sách công việc làm lại" }).click();
    await p
      .getByRole("link", { name: "CLN-20261016-010 · Làm lại 8" })
      .waitFor();
    assert.equal(new URL(p.url()).pathname, "/rework");
  }));

test("legacy task progress is readable without red alert; permission denial remains an error", async () => {
  await fixture("/task-progress", async (p, calls) => {
    await p
      .getByRole("heading", { name: "Hồ sơ công việc cũ · Chỉ xem" })
      .waitFor();
    await p.screenshot({
      path: "../artifacts/lan-workflow-demo/archived-task.png",
      fullPage: true,
    });
    assert.equal(await p.getByRole("alert").count(), 0);
    assert.equal(
      calls.some((c) => c.method === "POST"),
      false,
    );
  });
  await fixture(
    "/task-progress",
    async (p) => {
      await p.getByRole("alert").waitFor();
      assert.equal(
        await p
          .getByRole("heading", { name: "Hồ sơ công việc cũ · Chỉ xem" })
          .count(),
        0,
      );
    },
    { forbiddenProgress: true },
  );
});

test("denied archived rework never displays historical details", () =>
  fixture(
    "/rework/8?branchId=1",
    async (p) => {
      await p.getByRole("alert").waitFor();
      assert.equal(
        await p
          .getByText("Vết bẩn trên bàn cần làm sạch", { exact: true })
          .count(),
        0,
      );
    },
    { deniedRecord: true },
  ));
for (const readOnly of [false, true])
  test(`assigned cleaning execution links respect execute permission: readOnly=${readOnly}`, () =>
    fixture(
      "/assigned",
      async (p) => {
        await p
          .getByRole("heading", { name: "CLN-20261016-001 · 201" })
          .waitFor();
        if (!readOnly)
          await p
            .getByRole("link", { name: "Mở thực hiện công việc" })
            .waitFor();
        assert.equal(
          await p.getByRole("link", { name: "Mở thực hiện công việc" }).count(),
          readOnly ? 0 : 1,
        );
        assert.equal(
          await p.getByRole("link", { name: /Reassign|Phân công lại/ }).count(),
          0,
        );
      },
      { readOnly },
    ));
test("OWN incident source detail is permitted without exposing incident queue or manager operations", () =>
  fixture("/operations/incidents/41?branchId=1", async (p, calls) => {
    await p.getByText("Sự cố do Lan báo", { exact: true }).first().waitFor();
    assert.equal(
      calls.some((c) => c.path === "/api/incidents"),
      false,
    );
    assert.equal(
      await p
        .locator('nav[aria-label="Vận hành"] a[href^="/operations/incidents"]')
        .count(),
      0,
    );
    assert.equal(
      await p
        .getByRole("link", {
          name: /Quay lại danh sách sự cố|bảo trì liên quan|phân công xử lý|vật tư cho sự cố/,
        })
        .count(),
      0,
    );
    assert.equal(
      await p
        .getByRole("button", {
          name: /Phân loại|Phân công|Khóa phòng|Hủy sự cố/,
        })
        .count(),
      0,
    );
    assert.equal(
      await p
        .locator(
          'a[href^="/operations/assets/"], a[href^="/operations/inventory/"], a[href^="/operations/maintenance/"]',
        )
        .count(),
      0,
    );
    await p.screenshot({
      path: "../artifacts/lan-dang-access-review/screenshots/own-incident.png",
      fullPage: true,
    });
  }));
test("foreign incident denial clears detail and presents no mutation", () =>
  fixture(
    "/operations/incidents/41?branchId=1",
    async (p, calls) => {
      await p
        .getByText("Không tìm thấy hồ sơ hoặc hồ sơ ngoài phạm vi được phép.", {
          exact: true,
        })
        .waitFor();
      assert.equal(
        await p.getByText("Sự cố do Lan báo", { exact: true }).count(),
        0,
      );
      assert.equal(
        calls.some((c) => c.method === "POST"),
        false,
      );
    },
    { foreign: true },
  ));
test("report-only popup cancel returns home without entering denied queue or posting", () =>
  fixture("/operations/incidents?action=A004A-021", async (p, calls) => {
    await p.getByRole("dialog").waitFor();
    p.once("dialog", (d) => d.accept());
    await p
      .getByRole("dialog")
      .getByRole("button", { name: "Đóng", exact: true })
      .click();
    await p.getByRole("heading", { name: "Trang chủ", exact: true }).waitFor();
    assert.equal(
      calls.some((c) => c.method === "POST" || c.path === "/api/incidents"),
      false,
    );
  }));
test("permission hints never grant broad incident list or cross-branch source", () =>
  fixture("/home", async (p) => {
    const values = await p.evaluate(
      ({ grants, paths }) => [
        window.permittedOperations(grants, paths, "1"),
        window.permittedOperations(grants, paths, "1", true),
        window.permittedOperations(grants, paths, "2", true),
      ],
      { grants, paths },
    );
    assert.equal(values[0].actions.includes("A004A-023"), false);
    assert.equal(values[1].actions.includes("A004A-023"), true);
    assert.equal(values[1].actions.includes("A004A-022"), false);
    assert.equal(values[2].actions.includes("A004A-023"), false);
    for (const value of values)
      assert.equal(
        value.actions.some((a) =>
          ["A004A-040", "A004A-046", "A004A-047", "A004A-048"].includes(a),
        ),
        false,
      );
  }));

test("legacy cleaning displays its authorized record without execution controls", () =>
  fixture("/legacy-cleaning", async (p, calls) => {
    await p.getByRole("region", { name: "Hồ sơ vệ sinh lưu trữ" }).waitFor();
    assert.equal(await p.getByRole("alert").count(), 0);
    assert.equal(
      await p.getByRole("button", { name: "Bắt đầu", exact: true }).count(),
      0,
    );
    assert.equal(
      calls.some((c) => c.method === "POST"),
      false,
    );
  }));
test("denied legacy cleaning record remains denied", () =>
  fixture(
    "/legacy-cleaning",
    async (p) => {
      await p.getByRole("alert").waitFor();
      assert.equal(
        await p.getByRole("region", { name: "Hồ sơ vệ sinh lưu trữ" }).count(),
        0,
      );
    },
    { deniedRecord: true },
  ));
test("home does not probe reports denied by server navigation", () =>
  fixture("/home", async (p, calls) => {
    await p
      .getByRole("link", { name: "Công việc buồng phòng", exact: true })
      .waitFor();
    await p.waitForTimeout(100);
    assert.equal(
      calls.some((c) => c.path === "/api/reports/options"),
      false,
    );
  }));

test("legacy progress opens the authorized archive without a broken form", () =>
  fixture("/legacy-progress", async (p) => {
    await p.getByRole("region", { name: "Hồ sơ vệ sinh lưu trữ" }).waitFor();
    assert.equal(await p.getByRole("alert").count(), 0);
    assert.equal(await p.getByRole("dialog").count(), 0);
  }));
test("material links respect both availability and read grants", () =>
  fixture("/material-links", async (p, calls) => {
    await p
      .getByText(/Liên kết công việc vận hành chưa được cấu hình/)
      .waitFor();
    assert.equal(
      await p
        .getByText("Lần thực hiện bảo trì liên kết", { exact: true })
        .count(),
      0,
    );
    assert.equal(
      calls.some((c) => c.path.includes("/linked-")),
      false,
    );
  }));
