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
import React from 'react'; import {createRoot} from 'react-dom/client'; import {BrowserRouter} from 'react-router-dom';
import './src/styles.css'; import './src/styles/cacao.css'; import './src/styles/design-system.css';
import {AuthProvider} from './src/auth/AuthProvider'; import {createAuthStore} from './src/auth/authStore'; import {createAuthApi} from './src/auth/authApi';
import {RoomStatusDetails} from './src/screens/rooms/RoomStatusDetails'; import {detail,history} from './tests/room-status-fixtures.mjs';
import {InspectionHistoryAuthRoute} from './src/shared-host/InspectionHistoryAuthRoute'; import {TaskReadPage} from './src/operationalTasks/TaskReadPage';
import Cleaning from './src/screens/housekeeping/CleaningTaskListScreen';
import {hotelInputToUtc,utcToHotelInput,recordedTime} from './src/screens/housekeeping/OperationalReadView';
window.timeHelpers={hotelInputToUtc,utcToHotelInput,recordedTime}; window.calls=[];
const row={id:42,number:'INS-20261016-004',roomId:11,roomNumber:'101',branchId:7,cleaningTaskId:12,versionId:4,versionNumber:2,inspectorId:8,inspectorName:'Hoàng Thị Yến',status:'COMPLETED',result:'CONDITIONAL',at:'2026-10-16T10:00:00',summary:'Cần kiểm tra lại khăn tắm',reworkCount:1,defectCount:1,reinspectionCount:null,cycleFailedInspectionCount:0};
window.fetch=async(url,options={})=>{
 window.calls.push({url:String(url),method:options.method??'GET'}); const u=new URL(url,location.origin),q=u.searchParams;
 if(u.pathname==='/api/auth/session')return Response.json({user:{id:'1',username:'hung.nguyen',name:'Nguyễn Văn Hùng'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:false,csrfToken:'test'});
 if(u.pathname==='/api/auth/navigation/operation-grants')return Response.json({grants:['ROOM_VIEW','INSPECTION_VIEW','CLEANING_VIEW','TASK_VIEW'].map(permission=>({permission,branchId:7,scope:'BRANCH'}))});
 if(u.pathname==='/api/inspection-history/branches')return Response.json([{id:7,name:'Khách sạn Đức Vượng - Bùi Viện',isPrimary:true}]);
 if(u.pathname==='/api/inspection-history')return Response.json({items:[row],total:1,filters:{branchId:7,page:Number(q.get('page')),pageSize:Number(q.get('pageSize')),roomId:null,inspectorId:null,housekeeperId:null,from:q.get('from'),to:q.get('to'),result:q.get('result'),search:q.get('search')}});
 if(u.pathname==='/api/inspection-history/42')return Response.json({inspection:row,completedBy:null,housekeepers:[],criteria:[{id:null,itemId:1,criterion:'Giường, ga gối sạch',description:'Ga gối không vết bẩn',mandatory:true,passCondition:'Không có vết ố',outcome:null,note:null,at:null}],defects:[],reworks:[],cycleInspections:[row],evidence:[]});
 if(u.pathname==='/api/operational-tasks/branches')return Response.json([{id:7,name:'Khách sạn Đức Vượng - Bùi Viện',isPrimary:true,canCreate:false}]);
 if(u.pathname==='/api/branches/7/operational-tasks/3')return Response.json({task:{id:3,code:'OT-2026-0003',branchId:7,departmentId:2,title:'Kiểm tra lần cuối phòng 501',status:'TODO',progressPercent:0,dueAt:'2026-10-20T10:00:00Z'},description:'Kiểm tra toàn bộ phòng trước khi đón khách',priority:'Cao',completionNote:null,progress:[],progressTotalCount:0});
 if(u.pathname==='/api/rooms/workspace-options')return Response.json([{id:7,name:'Khách sạn Đức Vượng - Bùi Viện',isPrimary:true,canViewRooms:true,canMonitor:true,canCreate:false,canReadOwn:true}]);
 if(u.pathname.endsWith('/cleaning-monitor/options'))return Response.json({floors:[],assignees:[],priorities:[],statuses:['IN_PROGRESS']});
 if(u.pathname.endsWith('/cleaning-monitor'))return Response.json({items:[{kind:q.get('kind')||'cleaning',id:12,cleaningTaskId:12,taskNumber:'VS-012',roomId:11,roomNumber:'101',floorId:2,floorName:'Tầng 1',recordedStatus:'IN_PROGRESS',workflowState:null,priorityId:5,priorityCode:'HIGH',assigneeId:6,assigneeName:'Đặng Thị Lan',dueAtUtc:'2026-11-19T07:00:00Z',isOverdue:false,progressPercent:40,blockerCode:null,blockerNote:null,description:null}],total:1,page:1,pageSize:20,fromDate:'2026-11-19',toDate:'2026-11-19',timezone:'Asia/Ho_Chi_Minh',asOfUtc:'2026-11-19T05:00:00Z'});
 return Response.json({},{status:403});
};
const store=createAuthStore(createAuthApi(window.fetch));
function Room(){const [open,close]=React.useState(true);return open?<RoomStatusDetails detail={{...detail,currentCycle:{cleaningTaskId:12,cleaningState:null,inspectionId:42,inspectionState:null,reworkId:null}}} history={history} branchName="Khách sạn Đức Vượng - Bùi Viện" close={()=>close(false)} paginate={()=>{}} sourceLink={()=>null}/>:<p>Đã đóng</p>}
createRoot(document.getElementById('root')).render(<React.StrictMode><BrowserRouter><AuthProvider store={store}><main className="cacao-content">{window.scenario==='room'?<Room/>:window.scenario==='history'?<InspectionHistoryAuthRoute/>:window.scenario==='task'?<TaskReadPage/>:<Cleaning kindOnly={window.scenario==='rework'?'rework':undefined}/>}</main></AuthProvider></BrowserRouter></React.StrictMode>);
`,
  },
  bundle: true,
  write: false,
  outfile: "bundle.js",
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
  mkdirSync("../artifacts/operations-read-ui/screenshots", { recursive: true });
});
after(async () => browser?.close());
async function run(scenario, check, width = 1280) {
  const page = await browser.newPage({ viewport: { width, height: 900 } }),
    errors = [];
  page.on("pageerror", (e) => errors.push(e.message));
  page.setDefaultTimeout(10000);
  try {
    await page.route("**/*", (r) =>
      r.request().resourceType() === "document"
        ? r.fulfill({
            contentType: "text/html",
            body: '<html lang="vi"><meta charset="utf-8"><div id="root"></div></html>',
          })
        : r.abort(),
    );
    await page.goto(
      "https://local.test/" +
        {
          room: "rooms/status?branchId=7&roomId=11",
          history: "inspection-history",
          task: "operational-tasks?branchId=7&taskId=3",
          cleaning: "cleaning/tasks?branchId=7",
          rework: "rework?branchId=7",
        }[scenario],
    );
    await page.evaluate((v) => (window.scenario = v), scenario);
    for (const f of bundle.outputFiles.filter((f) => f.path.endsWith(".css")))
      await page.addStyleTag({ content: f.text });
    await page.addScriptTag({
      content: bundle.outputFiles.find((f) => f.path.endsWith(".js")).text,
    });
    await check(page);
    assert.deepEqual(errors, []);
    assert.ok(
      (await page.evaluate(() => window.calls)).every(
        (c) => c.method === "GET",
      ),
    );
  } finally {
    await page.close();
  }
}
test("room drawer stays read-only, opens inspection history, has one scroll area and closes", () =>
  run("room", async (p) => {
    const dialog = p.getByRole("dialog");
    await dialog.waitFor();
    assert.match(
      await dialog
        .getByRole("link", { name: "Kiểm tra #42" })
        .getAttribute("href"),
      /inspection-history\/42\?branchId=7/,
    );
    assert.equal(
      await dialog.getByText("Hành động tiếp theo", { exact: true }).count(),
      0,
    );
    assert.equal(
      await dialog.getByRole("button", { name: "Hủy", exact: true }).count(),
      0,
    );
    assert.equal(
      await dialog.evaluate((e) => getComputedStyle(e).overflowY),
      "hidden",
    );
    assert.ok(
      !(await p.evaluate(() => window.calls)).some((c) =>
        /cleaning-task-options|pending-assignment/.test(c.url),
      ),
    );
    await p.screenshot({
      path: "../artifacts/operations-read-ui/screenshots/room.png",
    });
    await dialog
      .getByRole("button", { name: "Đóng", exact: true })
      .last()
      .click();
    await p.getByText("Đã đóng").waitFor();
  }));
test("inspection history selects permitted named branch and renders compact Vietnamese details on mobile", () =>
  run(
    "history",
    async (p) => {
      await p
        .getByRole("button", { name: "INS-20261016-004", exact: true })
        .waitFor();
      assert.match(p.url(), /branchId=7/);
      assert.equal(await p.getByRole("columnheader").count(), 6);
      assert.equal(
        await p.getByRole("cell", { name: /^Đạt có điều kiện/ }).count(),
        1,
      );
      await p
        .getByRole("button", { name: "INS-20261016-004", exact: true })
        .click();
      const dialog = p.getByRole("dialog");
      await dialog.waitFor();
      await dialog
        .getByText("1. Giường, ga gối sạch", { exact: true })
        .waitFor();
      assert.equal(await dialog.getByText("Mã bằng chứng: —").count(), 0);
      assert.ok(
        await p.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
      );
      await p.screenshot({
        path: "../artifacts/operations-read-ui/screenshots/inspection-mobile.png",
      });
      await p.keyboard.press("Escape");
      await dialog.waitFor({ state: "hidden" });
    },
    390,
  ));
test("task viewer sees summary without executor or manager-only API calls", () =>
  run("task", async (p) => {
    await p
      .getByRole("heading", { name: "Kiểm tra lần cuối phòng 501" })
      .waitFor();
    await p
      .getByText("Bạn đang xem và theo dõi công việc.", { exact: false })
      .waitFor();
    assert.equal(await p.getByText("Chưa bắt đầu", { exact: true }).count(), 1);
    assert.equal(
      await p.getByRole("heading", { name: "Cập nhật tiến độ" }).count(),
      0,
    );
    assert.ok(
      !(await p.evaluate(() => window.calls)).some((c) =>
        /\/(progress|verification|plan|assignment|reopen)(\?|$)/.test(c.url),
      ),
    );
    await p.screenshot({
      path: "../artifacts/operations-read-ui/screenshots/task.png",
    });
  }));
for (const scenario of ["cleaning", "rework"])
  test(`${scenario}: branch monitor keeps canonical action query and no create link`, () =>
    run(scenario, async (p) => {
      const link = p.getByRole("link", { name: "VS-012", exact: true });
      await link.waitFor();
      assert.match(
        await link.getAttribute("href"),
        new RegExp(`kind=${scenario === "rework" ? "rework" : "cleaning"}$`),
      );
      assert.equal(
        await p.getByRole("link", { name: "Tạo công việc vệ sinh" }).count(),
        0,
      );
      assert.ok(
        !(await p.evaluate(() => window.calls)).some((c) =>
          c.url.includes("/my-"),
        ),
      );
    }));
test("history time conversion respects hotel timezone and preserves unspecified timestamps", () =>
  run("history", async (p) => {
    const values = await p.evaluate(() => ({
      utc: window.timeHelpers.hotelInputToUtc("2026-11-19T00:30"),
      local: window.timeHelpers.utcToHotelInput("2026-11-18T17:30:00.000Z"),
      recorded: window.timeHelpers.recordedTime("2026-10-16T10:00:00"),
    }));
    assert.deepEqual(values, {
      utc: "2026-11-18T17:30:00.000Z",
      local: "2026-11-19T00:30",
      recorded: "10:00 16/10/2026",
    });
  }));
