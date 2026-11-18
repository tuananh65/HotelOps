import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync, mkdirSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
const bundle = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `
import React from 'react'; import {createRoot} from 'react-dom/client'; import {BrowserRouter} from 'react-router-dom';
import {AuthProvider} from './src/auth/AuthProvider'; import {createAuthStore} from './src/auth/authStore'; import {createAuthApi} from './src/auth/authApi';
import {WorkdayRoute} from './src/workday/WorkdayRoute'; import './src/styles/tokens.css';
const s=window.scenario; const calls=[]; let working=!!s.working;
const session={user:{id:'6',username:'lan.dang',name:'Đặng Thị Lan'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:false,csrfToken:'TEST'};
const shift={shiftAssignmentId:10,shiftId:5,branchId:1,departmentId:2,shiftDate:'2026-11-18',scheduledStartAtUtc:'2026-11-17T23:00:00Z',scheduledEndAtUtc:'2026-11-18T07:00:00Z'};
const day={workdayId:20,branchId:1,departmentId:2,workDate:'2026-11-18',shiftAssignmentId:10,isUnscheduled:false,startedAtUtc:'2026-11-17T23:00:00Z',rowVersion:'AAAAAAAAAAE=',openInterval:null};
window.fetch=async(url,o={})=>{
 calls.push({url,method:o.method??'GET',body:o.body});
 if(url==='/api/auth/session')return Response.json(session);
 if(url==='/api/workdays/branch-options'){
  if(s.optionError)return Response.json({}, {status:503});
  const rows=[{branchId:1,name:'Khách sạn Đức Vượng - Chi nhánh Bùi Viện',isPrimary:true}];
  if(s.multiple)rows.push({branchId:2,name:'Chi nhánh Cao Thắng',isPrimary:false});
  return Response.json(s.noBranches?[]:rows);
 }
 if(url.endsWith('/own-shifts')){if(s.shiftError)return Response.json({}, {status:503});return Response.json(s.empty||working?[]:[shift]);}
 if(url.endsWith('/current'))return working?Response.json({...day,branchId:Number(url.split('/')[3])}):new Response(null,{status:204});
 if(url.endsWith('/own-departments'))return Response.json([{departmentId:2,code:'HK',name:'Buồng phòng'}]);
 if(url==='/api/auth/csrf')return Response.json({headerName:'X-CSRF-TOKEN',csrfToken:'TEST'});
 if(o.method==='POST'){
  if(s.unknown)throw Error('network');
  if(s.blockedEnd && url.endsWith('/end'))return Response.json({title:'PAUSE_ALL_CLEANING_RECONCILIATION_REQUIRED'},{status:409});
  const end=url.endsWith('/end');working=!end;
  return Response.json({...day,status:end?'ENDED':'WORKING',endedAtUtc:end?'2026-11-18T07:00:00Z':null},{status:end?200:201,headers:{ETag:'"AAAAAAAAAAE="'}});
 }
 throw Error('Unexpected route '+url);
};
window.calls=calls; const store=createAuthStore(createAuthApi(window.fetch));
createRoot(document.getElementById('root')).render(<BrowserRouter><AuthProvider store={store}><WorkdayRoute scheduled /></AuthProvider></BrowserRouter>);
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
async function run(scenario, callback, width = 1280) {
  const page = await browser.newPage({ viewport: { width, height: 1050 } });
  const errors = [];
  page.on("pageerror", (e) => errors.push(e.message));
  page.setDefaultTimeout(8000);
  try {
    await page.route("**/*", (r) =>
      r.request().resourceType() === "document"
        ? r.fulfill({
            contentType: "text/html",
            body: '<html lang="vi"><meta charset="utf-8"><div id="root"></div></html>',
          })
        : r.abort(),
    );
    await page.goto("https://offline.test/workday/scheduled?branchId=999");
    await page.evaluate((s) => (window.scenario = s), scenario);
    await page.addStyleTag({
      content:
        "body{margin:0;padding:24px;background:#faf6f1;font-family:system-ui}*{box-sizing:border-box}",
    });
    for (const output of bundle.outputFiles)
      if (output.path.endsWith(".css"))
        await page.addStyleTag({ content: output.text });
    await page.addScriptTag({
      content: bundle.outputFiles.find((f) => f.path.endsWith(".js")).text,
    });
    await callback(page);
    assert.deepEqual(errors, []);
  } finally {
    await page.close();
  }
}
test("Lan auto-loads her branch, today's shifts and Vietnamese layout without exposing IDs", async () => {
  for (const width of [1280, 390])
    await run(
      {},
      async (p) => {
        await p
          .getByRole("button", { name: "Bắt đầu ca đã phân", exact: true })
          .waitFor();
        await p.getByLabel("Ca đã lên lịch").waitFor();
        assert.equal(await p.getByLabel("Ca đã lên lịch").inputValue(), "10");
        assert.match(
          await p.getByLabel("Ca đã lên lịch").innerText(),
          /06:00.*18\/11\/2026.*14:00/,
        );
        assert.equal(await p.getByRole("heading", { level: 1 }).count(), 1);
        assert.equal(
          await p.getByRole("textbox", { name: "Mã chi nhánh" }).count(),
          0,
        );
        assert.equal(await p.locator("main main").count(), 0);
        assert.equal(
          await p
            .getByRole("region", {
              name: "Bắt đầu ngoài kế hoạch",
              exact: true,
            })
            .count(),
          0,
        );
        assert.ok(
          (await p.evaluate(() => calls)).every(
            (c) => !c.url.includes("/999/"),
          ),
        );
        assert.ok(
          await p.evaluate(
            () => document.documentElement.scrollWidth <= innerWidth,
          ),
        );
        mkdirSync("../artifacts/scheduled-workday-ui", { recursive: true });
        await p.screenshot({
          path: `../artifacts/scheduled-workday-ui/lan-${width}.png`,
          fullPage: true,
        });
      },
      width,
    );
});
test("start and end require confirmation, preserve server receipt and prevent duplicate writes", () =>
  run({ multiple: true }, async (p) => {
    await p.getByLabel("Ca đã lên lịch").waitFor();
    await p.getByText("Chưa bắt đầu ca", { exact: true }).waitFor();
    await p
      .getByRole("button", { name: "Bắt đầu ca đã phân", exact: true })
      .click();
    assert.equal(
      (await p.evaluate(() => calls.filter((c) => c.method === "POST"))).length,
      0,
    );
    assert.equal(
      await p.getByLabel("Chọn chi nhánh", { exact: true }).isDisabled(),
      true,
    );
    await p
      .getByRole("button", { name: "Xác nhận bắt đầu ca", exact: true })
      .dblclick();
    await p.getByText("Đang làm việc", { exact: true }).waitFor();
    await p
      .getByRole("status")
      .filter({ hasText: "Đã ghi nhận bắt đầu ca thành công" })
      .waitFor();
    assert.equal(
      (await p.evaluate(() => calls.filter((c) => c.method === "POST"))).length,
      1,
    );
    await p
      .getByRole("button", { name: "Kết thúc ngày làm việc", exact: true })
      .click();
    await p
      .getByRole("button", {
        name: "Xác nhận kết thúc ngày làm việc",
        exact: true,
      })
      .click();
    await p
      .getByRole("status")
      .filter({ hasText: "Đã ghi nhận kết thúc ca thành công" })
      .waitFor();
    assert.equal(
      (await p.evaluate(() => calls.filter((c) => c.method === "POST"))).length,
      2,
    );
  }));
test("running cleaning work shows actionable rejection without locking end-shift; retry succeeds after resolution", () =>
  run({ working:true, blockedEnd:true }, async p => {
    const end = p.getByRole('button',{name:'Kết thúc ngày làm việc',exact:true});
    await end.click();
    await p.getByRole('button',{name:'Xác nhận kết thúc ngày làm việc',exact:true}).click();
    await p.getByRole('alert').filter({hasText:'công việc dọn phòng'}).waitFor();
    assert.equal(await end.isDisabled(),false);
    assert.equal(await p.getByText('Chưa xác định kết quả.',{exact:false}).count(),0);
    assert.equal((await p.evaluate(()=>calls.filter(c=>c.method==='POST'))).length,1);
    await p.evaluate(()=>window.scenario.blockedEnd=false);
    await p.getByRole('button',{name:'Tải lại dữ liệu',exact:true}).click();
    await end.click();
    await p.getByRole('button',{name:'Xác nhận kết thúc ngày làm việc',exact:true}).click();
    await p.getByRole('status').filter({hasText:'Đã ghi nhận kết thúc ca thành công'}).waitFor();
    assert.equal((await p.evaluate(()=>calls.filter(c=>c.method==='POST'))).length,2);
  }));
test("empty, failed reads and unavailable branches are distinct; unscheduled only after successful reads", async () => {
  await run({ empty: true }, async (p) => {
    await p
      .getByText("Hôm nay chưa có ca có thể bắt đầu", { exact: true })
      .waitFor();
    const details = p.locator("details");
    assert.equal(await details.getAttribute("open"), null);
    await details.locator("summary").click();
    await p.getByLabel("Bộ phận", { exact: true }).waitFor();
  });
  await run({ shiftError: true }, async (p) => {
    await p
      .getByRole("alert")
      .filter({ hasText: "Chưa tải được ca làm việc" })
      .waitFor();
    assert.equal(await p.locator("details").count(), 0);
    assert.equal(
      await p.getByRole("button", { name: "Bắt đầu ca đã phân" }).isDisabled(),
      true,
    );
  });
  await run({ optionError: true }, async (p) => {
    await p.getByRole("button", { name: "Thử lại", exact: true }).waitFor();
    assert.equal(
      (
        await p.evaluate(() =>
          calls.filter((c) => c.url.includes("/workdays/own-")),
        )
      ).length,
      0,
    );
  });
});
test("unknown start cannot be repeated after refreshing", () =>
  run({ unknown: true }, async (p) => {
    await p.getByLabel("Ca đã lên lịch").waitFor();
    await p.getByText("Chưa bắt đầu ca", { exact: true }).waitFor();
    await p
      .getByRole("button", { name: "Bắt đầu ca đã phân", exact: true })
      .click();
    await p
      .getByRole("button", { name: "Xác nhận bắt đầu ca", exact: true })
      .click();
    await p
      .getByRole("alert")
      .filter({ hasText: "Chưa xác định kết quả" })
      .waitFor();
    await p
      .getByRole("button", { name: "Tải lại dữ liệu", exact: true })
      .click();
    await p.getByText("Chưa bắt đầu ca", { exact: true }).waitFor();
    assert.equal(
      await p
        .getByRole("button", { name: "Bắt đầu ca đã phân", exact: true })
        .isDisabled(),
      true,
    );
    assert.equal(
      (await p.evaluate(() => calls.filter((c) => c.method === "POST"))).length,
      1,
    );
  }));
