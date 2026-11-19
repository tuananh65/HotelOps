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
import React from 'react';import {createRoot} from 'react-dom/client';import {BrowserRouter} from 'react-router-dom';
import './src/styles.css';import './src/styles/cacao.css';import './src/styles/design-system.css';
import {AuthProvider} from './src/auth/AuthProvider';import {createAuthStore} from './src/auth/authStore';import {createAuthApi} from './src/auth/authApi';
import RoomBoard from './src/screens/rooms/RoomStatusBoardScreen';import Turnover from './src/screens/housekeeping/RoomTurnoverHistoryScreen';import Cleaning from './src/screens/housekeeping/CleaningTaskListScreen';
const s=window.scenario;window.calls=[];
const room={id:3,branchId:1,roomNumber:'203',floor:{id:2,code:'F2',name:'Tầng 2',number:2},area:{id:4,code:'E',name:'Khu Đông'},roomType:{id:6,code:'STD',name:'Phòng tiêu chuẩn'},operationalStatusCode:'DIRTY',effectiveReadiness:'PENDING_CLEANING',isActive:true};
window.fetch=async(url,o={})=>{
 window.calls.push({url,method:o.method??'GET'});const u=new URL(url,location.origin),q=u.searchParams;
 if(url==='/api/auth/session')return Response.json({user:{id:'8',username:'le.truong',name:'Trương Thị Lệ'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:false,csrfToken:'test'});
 if(url==='/api/auth/csrf')return Response.json({headerName:'X-CSRF-TOKEN',csrfToken:'test'});
 if(url==='/api/rooms/workspace-options')return s.scopeError?Response.json({},{status:503}):Response.json([{id:1,name:'Khách sạn Đức Vượng - Chi nhánh Bùi Viện',isPrimary:true,canViewRooms:true,canMonitor:!!s.supervisor,canCreate:!!s.supervisor,canReadOwn:true},...(s.multiple?[{id:2,name:'Chi nhánh thứ hai',isPrimary:false,canViewRooms:true,canMonitor:!!s.supervisor,canCreate:false,canReadOwn:true}]:[])]);
 if(u.pathname==='/api/rooms'){
   if(s.hang&&!window.recover)return new Promise((resolve,reject)=>o.signal?.addEventListener('abort',()=>reject(new DOMException('Aborted','AbortError')),{once:true}));
   if(s.delay)await new Promise(resolve=>setTimeout(resolve,s.delay));
   const b=Number(q.get('branchId'));const items=s.empty?[]:[{...room,branchId:b,roomNumber:b===1?'203':'401'}];
   if(s.floorTest)items.push({...room,id:4,roomNumber:'101',floor:{id:1,code:'F1',name:'Tầng 1',number:1},area:null});
   const total=s.paginated?21:items.length;
   return Response.json({items,page:Number(q.get('page')||1),pageSize:20,totalCount:total,statusCounts:items.length?[{operationalStatusCode:'DIRTY',count:total}]:[],effectiveReadinessCounts:items.length?{PENDING_CLEANING:total}:{}});
 }
 if(u.pathname==='/api/branches/1/cleaning-task-options')return Response.json({roomId:Number(q.get('roomId')),branchId:1,roomNumber:'203',cleaningItemCount:1,cleaningTypes:[{id:1,code:'CHECKOUT',name:'Sau trả phòng'}],priorities:[{id:1,code:'NORMAL',name:'Bình thường'}],existingTaskId:null});
 if(u.pathname.endsWith('/cleaning-monitor/options'))return Response.json({floors:[{id:2,name:'Tầng 2'}],assignees:[{id:6,name:'Đặng Thị Lan'}],priorities:[{id:5,name:'HIGH'}],statuses:['IN_PROGRESS','COMPLETED']});
 if(u.pathname.endsWith('/cleaning-monitor'))return Response.json({items:s.empty?[]:[{kind:q.get('kind')||'cleaning',id:12,cleaningTaskId:12,taskNumber:'VS-012',roomId:3,roomNumber:'203',floorId:2,floorName:'Tầng 2',recordedStatus:'IN_PROGRESS',workflowState:null,priorityId:5,priorityCode:'HIGH',assigneeId:6,assigneeName:'Đặng Thị Lan',dueAtUtc:'2026-11-18T07:00:00Z',isOverdue:false,progressPercent:40,blockerCode:null,blockerNote:null,description:null}],total:s.empty?0:1,page:1,pageSize:20,fromDate:q.get('fromDate')||'2026-11-18',toDate:q.get('toDate')||'2026-11-18',timezone:'Asia/Ho_Chi_Minh',asOfUtc:'2026-11-18T05:00:00Z'});
 if(u.pathname.includes('/my-'))return Response.json({items:s.empty?[]:[{id:12,branchId:1,assignmentId:15,taskNumber:'VS-012',roomNumber:'203',recordedStatus:'ASSIGNED'}],nextCursor:null});
 if(u.pathname.endsWith('/turnover-history'))return Response.json({roomId:3,branchId:1,items:[],total:0,filters:{page:1,pageSize:20},coverage:{cleaning:'PERSISTED_SOURCE',inspection:'NOT_ACCEPTED',rework:'PERSISTED_SOURCE_NOT_SHARED_ACCEPTED',readiness:'PERSISTED_EVENTS_NOT_SHARED_ACCEPTED',roomStatusHistory:'UNAVAILABLE_CYCLE_LINK_CONTRACT'}});
 return Response.json({},{status:403});
};
const store=createAuthStore(createAuthApi(window.fetch));window.store=store;
createRoot(document.getElementById('root')).render(<React.StrictMode><BrowserRouter><AuthProvider store={store}><main className="cacao-content">{s.page==='rooms'?<RoomBoard/>:s.page==='turnover'?<Turnover/>:<Cleaning kindOnly={s.page==='rework'?'rework':undefined}/>}</main></AuthProvider></BrowserRouter></React.StrictMode>);
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
  mkdirSync("../artifacts/room-workspace-fix/screenshots", { recursive: true });
});
after(async () => browser?.close());
async function run(scenario, check, width = 1300) {
  const p = await browser.newPage({ viewport: { width, height: 1000 } }),
    errors = [];
  p.on("pageerror", (e) => errors.push(e.message));
  p.setDefaultTimeout(8000);
  try {
    await p.route("**/*", (r) =>
      r.request().resourceType() === "document"
        ? r.fulfill({
            contentType: "text/html",
            body: '<html lang="vi"><meta charset="utf-8"><div id="root"></div></html>',
          })
        : r.abort(),
    );
    await p.goto(
      "https://local.test/" +
        {
          rooms: "rooms/status",
          turnover: "rooms/turnover",
          cleaning: "cleaning/tasks",
          rework: "rework",
        }[scenario.page] +
        (scenario.query ?? ""),
    );
    await p.evaluate((s) => (window.scenario = s), scenario);
    for (const f of bundle.outputFiles)
      if (f.path.endsWith(".css")) await p.addStyleTag({ content: f.text });
    await p.addScriptTag({
      content: bundle.outputFiles.find((f) => f.path.endsWith(".js")).text,
    });
    await check(p);
    assert.deepEqual(errors, []);
    assert.ok(
      (await p.evaluate(() => window.calls)).every((c) => c.method === "GET"),
    );
  } finally {
    await p.close();
  }
}
test("room board automatically selects account branch and loads named rooms", () =>
  run({ page: "rooms" }, async (p) => {
    await p.getByRole("link", { name: "203", exact: true }).waitFor();
    await p
      .getByRole("button", { name: /^(Bộ lọc|Ẩn bộ lọc)( \(\d+\))?$/ })
      .click();
    await p.waitForFunction(
      () =>
        document.querySelector("select[name=floorId]")?.options.length === 2,
    );
    assert.equal(
      await p.locator("input[name=branchId]").getAttribute("type"),
      "hidden",
    );
    assert.equal(
      await p
        .getByLabel("Tầng", { exact: true })
        .locator("option")
        .last()
        .textContent(),
      "Tầng 2",
    );
    await p
      .getByLabel("Trạng thái vận hành", { exact: true })
      .selectOption("PENDING_CLEANING");
    await p.getByRole("button", { name: "Lọc phòng", exact: true }).click();
    await p.getByRole("link", { name: "203", exact: true }).waitFor();
    assert.match(p.url(), /readiness=PENDING_CLEANING/);
    await p.screenshot({
      path: "../artifacts/room-workspace-fix/screenshots/room-board.png",
      fullPage: true,
    });
  }));
test("unauthorized branch URL does not query room data", () =>
  run({ page: "rooms", query: "?branchId=999" }, async (p) => {
    await p.getByRole("alert").waitFor();
    assert.equal(
      (await p.evaluate(() => window.calls)).filter((c) =>
        c.url.startsWith("/api/rooms?"),
      ).length,
      0,
    );
  }));

test("delayed room read settles once; focus does not clear the board or fetch again", () =>
  run({ page: "rooms", delay: 300 }, async (p) => {
    await p
      .getByRole("status", { name: "Đang tải phòng", exact: true })
      .waitFor();
    await p.getByRole("link", { name: "203", exact: true }).waitFor();
    assert.equal(
      (await p.evaluate(() => window.calls)).filter((c) =>
        c.url.startsWith("/api/rooms?"),
      ).length,
      2,
    );
    await p.evaluate(() => window.dispatchEvent(new Event("focus")));
    assert.equal(
      await p.getByRole("link", { name: "203", exact: true }).count(),
      1,
    );
    assert.equal(
      (await p.evaluate(() => window.calls)).filter((c) =>
        c.url.startsWith("/api/rooms?"),
      ).length,
      2,
    );
    assert.equal(
      await p
        .getByRole("status", { name: "Đang tải phòng", exact: true })
        .count(),
      0,
    );
    const heading = await p
      .getByRole("heading", { name: "Danh sách phòng", exact: true })
      .boundingBox();
    assert.ok(heading.y < 550, "Room list should appear near the top");
    await p.screenshot({
      path: "../artifacts/room-workspace-fix/screenshots/room-board-compact.png",
      fullPage: true,
    });
  }));

test("hung room request ends with retry and can recover", () =>
  run({ page: "rooms", hang: true }, async (p) => {
    await p.clock.install();
    await p
      .getByRole("status", { name: "Đang tải phòng", exact: true })
      .waitFor();
    await p.clock.fastForward(26000);
    await p
      .getByRole("alert")
      .filter({ hasText: "Tải danh sách phòng quá lâu" })
      .waitFor();
    assert.equal(
      await p
        .getByRole("status", { name: "Đang tải phòng", exact: true })
        .count(),
      0,
    );
    await p.evaluate(() => {
      window.recover = true;
    });
    await p.getByRole("button", { name: "Thử lại", exact: true }).click();
    await p.getByRole("link", { name: "203", exact: true }).waitFor();
    assert.equal(await p.getByRole("alert").count(), 0);
  }));

test("room pagination sends page parameter and can return to page one", () =>
  run({ page: "rooms", paginated: true }, async (p) => {
    await p.getByRole("link", { name: "203", exact: true }).waitFor();
    await p.getByRole("button", { name: "Trang sau", exact: true }).click();
    await p.getByText("21 phòng phù hợp · Trang 2").waitFor();
    assert.match(p.url(), /page=2/);
    await p.getByRole("button", { name: "Trang trước", exact: true }).click();
    await p.getByText("21 phòng phù hợp · Trang 1").waitFor();
  }));

test("room filters preserve typed search when advanced catalogue loads", () =>
  run({ page: "rooms", delay: 150 }, async (p) => {
    await p.getByRole("link", { name: "203", exact: true }).waitFor();
    await p.getByLabel("Số phòng", { exact: true }).fill("203");
    await p
      .getByRole("button", { name: /^(Bộ lọc|Ẩn bộ lọc)( \(\d+\))?$/ })
      .click();
    await p.waitForFunction(
      () =>
        document.querySelector("select[name=floorId]")?.options.length === 2,
    );
    assert.equal(
      await p.getByLabel("Số phòng", { exact: true }).inputValue(),
      "203",
    );
    await p.getByLabel("Tầng", { exact: true }).selectOption("2");
    await p
      .getByRole("button", { name: /^(Bộ lọc|Ẩn bộ lọc)( \(\d+\))?$/ })
      .click();
    await p.getByRole("button", { name: "Lọc phòng", exact: true }).click();
    await p.getByRole("link", { name: "203", exact: true }).waitFor();
    assert.equal(new URL(p.url()).searchParams.has("floorId"), false);
    await p.getByRole("button", { name: "Bộ lọc", exact: true }).click();
    await p.getByLabel("Tầng", { exact: true }).selectOption("2");
    await p.getByRole("button", { name: "Lọc phòng", exact: true }).click();
    await p.getByRole("link", { name: "203", exact: true }).waitFor();
    assert.match(p.url(), /floorId=2/);
    await p.getByRole("button", { name: "Xóa bộ lọc", exact: true }).click();
    await p.getByRole("link", { name: "203", exact: true }).waitFor();
    assert.equal(new URL(p.url()).searchParams.has("floorId"), false);
  }));

test("room board fits a narrow viewport", () =>
  run(
    { page: "rooms" },
    async (p) => {
      await p.getByRole("link", { name: "203", exact: true }).waitFor();
      assert.ok(
        await p.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
      );
    },
    390,
  ));
test("branch change clears room filters and loads the new branch", () =>
  run({ page: "rooms", multiple: true }, async (p) => {
    await p.getByRole("link", { name: "203", exact: true }).waitFor();
    await p
      .getByLabel("Chọn chi nhánh làm việc", { exact: true })
      .selectOption("2");
    await p.getByRole("link", { name: "401", exact: true }).waitFor();
    assert.equal(
      await p.getByRole("link", { name: "203", exact: true }).count(),
      0,
    );
  }));
test("turnover selects room number and converts Vietnamese date range to UTC", () =>
  run({ page: "turnover" }, async (p) => {
    await p.locator("#turnover-rooms option").waitFor({ state: "attached" });
    await p.getByLabel("Số phòng", { exact: true }).fill("2");
    assert.equal(
      await p.locator("#turnover-rooms option").first().getAttribute("value"),
      "203",
    );
    await p.getByLabel("Số phòng", { exact: true }).fill("203");
    await p.getByText("0 lần vệ sinh").waitFor();
    await p.getByRole("button", { name: "Bộ lọc", exact: true }).click();
    await p.getByLabel("Từ ngày", { exact: true }).fill("2026-11-18");
    await p.getByLabel("Đến ngày", { exact: true }).fill("2026-11-18");
    await p.getByRole("button", { name: "Xem lịch sử", exact: true }).click();
    await p.waitForFunction(() =>
      window.calls.some(
        (c) =>
          c.url.includes("turnover-history") &&
          decodeURIComponent(c.url).includes("2026-11-17T17:00:00.000Z"),
      ),
    );
    await p.screenshot({
      path: "../artifacts/room-workspace-fix/screenshots/turnover.png",
      fullPage: true,
    });
  }));
test("supervisor gets named filters and create action", () =>
  run({ page: "cleaning", supervisor: true }, async (p) => {
    await p.getByRole("link", { name: "VS-012", exact: true }).waitFor();
    await p
      .getByRole("button", { name: "Tạo công việc", exact: true })
      .waitFor();
    await p.getByRole("button", { name: "Bộ lọc", exact: true }).click();
    await p.getByLabel("Nhân viên", { exact: true }).selectOption("6");
    await p.getByRole("button", { name: "Lọc công việc", exact: true }).click();
    await p.waitForFunction(() =>
      window.calls.some((c) => c.url.includes("assigneeId=6")),
    );
    await p.screenshot({
      path: "../artifacts/room-workspace-fix/screenshots/cleaning-monitor.png",
      fullPage: true,
    });
  }));
test("le.truong opens own assignments without calling supervisor monitor", () =>
  run({ page: "cleaning" }, async (p) => {
    await p.getByRole("link", { name: "VS-012", exact: true }).waitFor();
    assert.equal(
      await p
        .getByRole("link", { name: "Tạo công việc vệ sinh", exact: true })
        .count(),
      0,
    );
    assert.equal(
      (await p.evaluate(() => window.calls)).filter((c) =>
        c.url.includes("cleaning-monitor"),
      ).length,
      0,
    );
    assert.match(
      await p
        .getByRole("link", { name: "VS-012", exact: true })
        .getAttribute("href"),
      /assignmentId=15/,
    );
    await p.screenshot({
      path: "../artifacts/room-workspace-fix/screenshots/own-cleaning.png",
      fullPage: true,
    });
  }));
test("own rework links to executor page and never calls monitor", () =>
  run({ page: "rework" }, async (p) => {
    const link = p.getByRole("link", { name: /VS-012/ });
    await link.waitFor();
    assert.match(await link.getAttribute("href"), /^\/rework\/12\?branchId=1$/);
    assert.equal(
      (await p.evaluate(() => window.calls)).filter((c) =>
        c.url.includes("cleaning-monitor"),
      ).length,
      0,
    );
  }));
test("rework monitor always preserves kind filter", () =>
  run({ page: "rework", supervisor: true }, async (p) => {
    await p.getByRole("link", { name: "VS-012", exact: true }).waitFor();
    await p.getByRole("button", { name: "Bộ lọc", exact: true }).click();
    await p.getByRole("button", { name: "Lọc công việc", exact: true }).click();
    await p.getByRole("link", { name: "VS-012", exact: true }).waitFor();
    assert.ok(
      (await p.evaluate(() => window.calls))
        .filter((c) => /cleaning-monitor\?/.test(c.url))
        .every((c) => c.url.includes("kind=rework")),
    );
  }));
test("scope failure shows retry and does not load records", () =>
  run({ page: "cleaning", scopeError: true }, async (p) => {
    await p.getByRole("alert").waitFor();
    assert.equal(
      (await p.evaluate(() => window.calls)).filter((c) =>
        c.url.includes("/branches/"),
      ).length,
      0,
    );
  }));
test("empty own list is understandable and mobile layout fits viewport", () =>
  run(
    { page: "rework", empty: true },
    async (p) => {
      await p.getByText("Bạn chưa có công việc làm lại được giao").waitFor();
      assert.ok(
        await p.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
      );
      await p.screenshot({
        path: "../artifacts/room-workspace-fix/screenshots/mobile-rework.png",
        fullPage: true,
      });
    },
    390,
  ));

test('floor change clears incompatible corridor and excludes it from request',()=>run({page:'rooms',floorTest:true},async p=>{
 await p.getByRole('link',{name:'203',exact:true}).waitFor();await p.getByRole('button',{name:'Bộ lọc',exact:true}).click();
 await p.getByLabel('Tầng',{exact:true}).selectOption('2');await p.getByLabel('Khu vực',{exact:true}).selectOption('4');
 await p.getByLabel('Tầng',{exact:true}).selectOption('1');assert.equal(await p.getByLabel('Khu vực',{exact:true}).inputValue(),'');assert.equal(await p.locator('select[name=areaId] option').count(),1);
 await p.getByRole('button',{name:'Lọc phòng',exact:true}).click();assert.equal(new URL(p.url()).searchParams.get('floorId'),'1');assert.equal(new URL(p.url()).searchParams.has('areaId'),false);
}));
test('turnover automatically requests first authorized room without typing',()=>run({page:'turnover'},async p=>{await p.getByText('0 lần vệ sinh').waitFor();assert.equal(new URL(p.url()).searchParams.get('roomId'),'3');}));
test('cleaning creation opens popup, resolves room number to ID and cancels without POST',()=>run({page:'cleaning',supervisor:true},async p=>{
 await p.getByRole('button',{name:'Tạo công việc',exact:true}).click();const d=p.getByRole('dialog');await d.waitFor();await d.getByLabel('Số phòng',{exact:true}).fill('203');await d.getByRole('heading',{name:/Phòng 203/}).waitFor();
 assert.ok((await p.evaluate(()=>window.calls)).some(c=>c.url.includes('cleaning-task-options?roomId=3')));assert.ok(!(await p.evaluate(()=>window.calls)).some(c=>c.url.includes('roomId=203')));
 await p.screenshot({path:'../artifacts/room-workspace-fix/screenshots/cleaning-create-popup.png'});p.once('dialog',d=>d.accept());await d.getByRole('button',{name:'Đóng',exact:true}).click();await p.getByRole('link',{name:'VS-012',exact:true}).waitFor();assert.equal((await p.evaluate(()=>window.calls)).filter(c=>c.method==='POST').length,0);
}));
