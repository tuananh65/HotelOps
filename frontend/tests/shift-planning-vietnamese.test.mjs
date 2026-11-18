import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
import { existsSync, mkdirSync } from "node:fs";
const session = {
  user: { id: "7", username: "ha.pham", name: "Phạm Thu Hà" },
  expiresAt: "2099-01-01T00:00:00Z",
  absoluteExpiresAt: "2099-01-02T00:00:00Z",
  mustChangePassword: false,
  csrfToken: "TEST-CSRF",
};
const bundle = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `
import React from 'react'; import {createRoot} from 'react-dom/client';
import {AuthProvider} from './src/auth/AuthProvider'; import {createAuthStore} from './src/auth/authStore'; import {createAuthApi} from './src/auth/authApi';
import {SecureShiftPlanWorkspace} from './src/workday/SecureShiftPlanWorkspace'; import './src/styles/tokens.css';
const calls=[]; const scenario=window.scenario; const tag='"'+'A'.repeat(64)+'"';
const templates=[{id:3,code:'SANG',name:'Ca sáng',start:'06:00:00',end:'14:00:00',crossesMidnight:false,breakMinutes:30,isActive:true,version:tag}];
window.fetch=async(url,init={})=>{
 calls.push({url,method:init.method??'GET',body:init.body});
 if(url==='/api/auth/session')return Response.json(${JSON.stringify(session)});
 if(url==='/api/auth/csrf')return Response.json({headerName:'X-CSRF-TOKEN',csrfToken:'TEST-CSRF'});
 if(url==='/api/shift-plans/options')return Response.json([{id:1,name:'Khách sạn Đức Vượng - Bùi Viện',timezone:'Asia/Ho_Chi_Minh',departments:[{id:2,name:'Buồng phòng'},{id:4,name:'Kỹ thuật - Bảo trì'}]}]);
 if(url.includes('/shift-definitions'))return Response.json(templates);
 if(url.includes('/shift-roster/employees'))return Response.json({branchId:1,departmentId:url.includes('/departments/4/')?4:2,employees:scenario.empty||url.includes('/departments/4/')?[]:[{id:6,name:'Đặng Thị Lan'},{id:8,name:'Nguyễn Thị Mai'}],nextCursor:null});
 if(url.endsWith('/preview'))return Response.json({rows:JSON.parse(init.body).drafts.map((_,index)=>({index,startUtc:'2099-02-28T23:00:00Z',endUtc:'2099-03-01T07:00:00Z',issues:scenario.conflict?[7]:[]})),requiresServerRevalidation:true});
 if(url.endsWith('/cancel'))return new Response(null,{status:204});
 if(url.includes('/shift-roster?'))return Response.json({branchId:1,departmentId:2,from:new URL('https://test'+url).searchParams.get('from'),to:'2099-03-07',timezone:'Asia/Ho_Chi_Minh',shifts:[{id:11,templateId:3,templateName:'Ca sáng',date:'2099-03-01',startUtc:'2099-02-28T23:00:00Z',endUtc:'2099-03-01T07:00:00Z',status:'PLANNED',requiredStaff:1,assignments:[{id:21,employeeId:6,employeeName:'Đặng Thị Lan',status:'ASSIGNED',version:tag}]}],nextCursor:null});
 if(init.method==='POST'){if(scenario.unknown)throw Error('Network failure');return Response.json({createdShifts:1,createdAssignments:JSON.parse(init.body).drafts.length,notificationCount:JSON.parse(init.body).drafts.length},{status:201});}
 throw Error('Unexpected URL: '+url);
};
window.calls=calls;const store=createAuthStore(createAuthApi(window.fetch));createRoot(document.getElementById('root')).render(<AuthProvider store={store}><SecureShiftPlanWorkspace saveEnabled /></AuthProvider>);
`,
  },
  bundle: true,
  write: false,
  outfile: "bundle.js",
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"production"' },
});
let browser;
before(async () => {
  const executablePath = [
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    "C:/Program Files/Google/Chrome/Application/chrome.exe",
  ].find(existsSync);
  browser = await chromium.launch({
    headless: true,
    ...(executablePath ? { executablePath } : {}),
  });
});
after(async () => browser?.close());
async function pageFor(scenario = {}, width = 1280) {
  const p = await browser.newPage({ viewport: { width, height: 1000 } });
  p.setDefaultTimeout(8000);
  p.errors = [];
  p.on("pageerror", (e) => p.errors.push(e.message));
  await p.route("**/*", (r) =>
    r.request().resourceType() === "document"
      ? r.fulfill({
          contentType: "text/html",
          body: '<html lang="vi"><meta charset="utf-8"><div id="root"></div></html>',
        })
      : r.abort(),
  );
  await p.goto("https://offline.test");
  await p.evaluate((s) => (window.scenario = s), scenario);
  await p.addStyleTag({
    content:
      "body {margin:0;padding:24px;background:#faf6f1;font-family:system-ui} *{box-sizing:border-box}",
  });
  for (const output of bundle.outputFiles)
    if (output.path.endsWith(".css"))
      await p.addStyleTag({ content: output.text });
  await p.addScriptTag({
    content: bundle.outputFiles.find((o) => o.path.endsWith(".js")).text,
  });
  try {
    await p.getByLabel("Bộ phận", { exact: true }).selectOption("2");
  } catch (e) {
    throw new Error(
      JSON.stringify({
        errors: p.errors,
        body: await p.locator("body").innerText(),
        calls: await p.evaluate(() => window.calls),
      }),
      { cause: e },
    );
  }
  if (!scenario.empty)
    await p.getByLabel("Nhân viên", { exact: true }).selectOption("6");
  return p;
}
async function add(p) {
  await p.getByLabel("Ngày làm việc", { exact: true }).fill("2099-03-01");
  await p.getByLabel("Ca làm việc", { exact: true }).selectOption("3");
  await p
    .getByRole("button", { name: "Thêm vào danh sách", exact: true })
    .click();
}
test("Vietnamese planning: names, duplicate protection, preserved draft, preview and single save", async () => {
  const p = await pageFor();
  try {
    assert.equal(
      await p.getByLabel("Chi nhánh", { exact: true }).isDisabled(),
      true,
    );
    await add(p);
    assert.equal(
      await p.getByLabel("Bộ phận", { exact: true }).isDisabled(),
      true,
    );
    await p.getByLabel("Nhân viên", { exact: true }).selectOption("6");
    await p
      .getByRole("button", { name: "Thêm vào danh sách", exact: true })
      .click();
    await p
      .getByText("Nhân viên này đã có trong danh sách cho cùng ca và ngày.", {
        exact: true,
      })
      .waitFor();
    await p.getByRole("button", { name: "Lịch đã phân", exact: true }).click();
    await p
      .getByRole("heading", { name: "Danh sách phân ca theo tuần" })
      .waitFor();
    await p.getByRole("button", { name: "Lập phân công", exact: true }).click();
    await p.getByRole("cell", { name: "Đặng Thị Lan", exact: true }).waitFor();
    await p
      .getByRole("button", { name: "Kiểm tra phân công", exact: true })
      .click();
    await p
      .getByRole("region", { name: "Kết quả kiểm tra phân công" })
      .waitFor();
    assert.match(
      await p
        .getByRole("region", { name: "Kết quả kiểm tra phân công" })
        .innerText(),
      /06:00/,
    );
    await p
      .getByRole("button", { name: "Tiếp tục lưu kế hoạch", exact: true })
      .click();
    await p
      .getByRole("button", { name: "Xác nhận lưu kế hoạch", exact: true })
      .dblclick();
    await p.getByText(/Đã lưu 1 phân công/).waitFor();
    const success = p.getByRole("status").filter({ hasText: "Đã lưu 1 phân công" });
    assert.deepEqual(await success.evaluate((el) => {
      const style = getComputedStyle(el);
      return [style.color, style.backgroundColor, style.borderLeftColor];
    }), ["rgb(22, 101, 52)", "rgb(240, 253, 244)", "rgb(22, 163, 74)"]);
    mkdirSync("../artifacts/shift-planning-ui", { recursive: true });
    await success.screenshot({ path: "../artifacts/shift-planning-ui/save-success-green.png" });
    const posts = await p.evaluate(() =>
      calls.filter((c) => c.method === "POST"),
    );
    assert.equal(posts.filter((c) => c.url.endsWith("/shift-plans")).length, 1);
    assert.deepEqual(
      JSON.parse(posts.find((c) => c.url.endsWith("/shift-plans")).body),
      { drafts: [{ employeeId: 6, templateId: 3, shiftDate: "2099-03-01" }] },
    );
    assert.deepEqual(p.errors, []);
  } finally {
    await p.close();
  }
});
test("empty employee scope is explained, scope change drops previous choices", async () => {
  const p = await pageFor({ empty: true });
  try {
    await p.getByText(/Bộ phận này chưa có nhân viên đủ điều kiện/).waitFor();
    assert.equal(
      await p
        .getByRole("button", { name: "Thêm vào danh sách", exact: true })
        .isDisabled(),
      true,
    );
    assert.deepEqual(p.errors, []);
  } finally {
    await p.close();
  }
  const q = await pageFor();
  try {
    await q.getByLabel("Bộ phận", { exact: true }).selectOption("4");
    await q.getByText(/Bộ phận này chưa có nhân viên đủ điều kiện/).waitFor();
    assert.equal(
      await q.getByLabel("Nhân viên", { exact: true }).inputValue(),
      "",
    );
    assert.deepEqual(q.errors, []);
  } finally {
    await q.close();
  }
});
test("conflicts block save and an unknown save cannot be repeated", async () => {
  for (const scenario of [{ conflict: true }, { unknown: true }]) {
    const p = await pageFor(scenario);
    try {
      await add(p);
      await p
        .getByRole("button", { name: "Kiểm tra phân công", exact: true })
        .click();
      await p
        .getByRole("region", { name: "Kết quả kiểm tra phân công" })
        .waitFor();
      if (scenario.conflict) {
        assert.equal(
          await p
            .getByRole("button", { name: "Tiếp tục lưu kế hoạch", exact: true })
            .count(),
          0,
        );
      } else {
        await p
          .getByRole("button", { name: "Tiếp tục lưu kế hoạch", exact: true })
          .click();
        await p
          .getByRole("button", { name: "Xác nhận lưu kế hoạch", exact: true })
          .click();
        await p.getByText(/Chưa xác định được kết quả lưu/).waitFor();
        assert.equal(await p.locator(".shift-notice-success").count(), 0);
        assert.equal(
          await p
            .getByRole("button", { name: "Thêm vào danh sách", exact: true })
            .isDisabled(),
          true,
        );
        assert.equal(
          await p
            .getByRole("button", { name: "Lập phân công khác", exact: true })
            .count(),
          0,
        );
      }
      assert.deepEqual(p.errors, []);
    } finally {
      await p.close();
    }
  }
});
test("roster and templates use the shared scope; desktop/mobile layout", async () => {
  mkdirSync("../artifacts/shift-planning-ui", { recursive: true });
  for (const width of [1280, 390]) {
    const p = await pageFor({}, width);
    try {
      await add(p);
      await p.screenshot({
        path: `../artifacts/shift-planning-ui/planning-${width}.png`,
        fullPage: true,
      });
      assert.equal(
        await p.evaluate(
          () => document.documentElement.scrollWidth > innerWidth,
        ),
        false,
      );
      await p.getByRole("button", { name: "Mẫu ca", exact: true }).click();
      await p.getByRole("cell", { name: "SANG", exact: true }).waitFor();
      await p.getByRole("button", { name: "Tạo mẫu ca", exact: true }).click();
      await p
        .getByRole("button", { name: "Lưu mẫu ca", exact: true })
        .waitFor();
      await p
        .getByRole("button", { name: "Lịch đã phân", exact: true })
        .click();
      await p
        .getByRole("button", { name: "Hủy phân công", exact: true })
        .click();
      await p
        .getByLabel("Lý do", { exact: true })
        .fill("Nhân viên được điều chỉnh lịch làm việc");
      await p.getByRole("checkbox").check();
      await p
        .getByRole("button", { name: "Xác nhận hủy", exact: true })
        .click();
      await p.getByText(/Đã hủy phân công; thông báo/).waitFor();
      assert.deepEqual(p.errors, []);
    } finally {
      await p.close();
    }
  }
});
