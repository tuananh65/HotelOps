import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
let browser, server, base;
const session = {
  user: { id: "7", username: "review", name: "Review user" },
  expiresAt: "2030-01-01T00:00:00Z",
  absoluteExpiresAt: "2030-01-02T00:00:00Z",
  mustChangePassword: false,
  csrfToken: "review-only-token",
};
const paths = [
  "/profile",
  "/notifications",
  "/users",
  "/roles",
  "/hotel",
  "/dashboard",
  "/cleaning/tasks",
  "/rooms/status",
  "/operations/incidents",
  "/assets",
  "/workday",
];
const account = {
  id: 7,
  employeeCode: "NV-007",
  username: "review",
  fullName: "Review user",
  email: "review@example.com",
  primaryBranch: null,
  department: null,
  roles: [],
  accountStatus: "ACTIVE",
};
before(async () => {
  await build({
    entryPoints: ["src/main.tsx"],
    bundle: true,
    format: "esm",
    jsx: "automatic",
    outdir: ".test-output/cacao-shell",
    define: { "process.env.NODE_ENV": '"production"', "import.meta.env": "{}" },
  });
  server = createServer(async (req, res) => {
    try {
      const name =
        req.url === "/main.js"
          ? "main.js"
          : req.url === "/main.css"
            ? "main.css"
            : null;
      res.setHeader(
        "Content-Type",
        name?.endsWith(".js")
          ? "text/javascript"
          : name
            ? "text/css"
            : "text/html",
      );
      res.end(
        name
          ? await readFile(".test-output/cacao-shell/" + name)
          : '<html><head><meta name="viewport" content="width=device-width,initial-scale=1"><link rel="stylesheet" href="/main.css"></head><body><div id="root"></div><script type="module" src="/main.js"></script></body></html>',
      );
    } catch {
      res.statusCode = 500;
      res.end();
    }
  });
  await new Promise((r) => server.listen(0, "127.0.0.1", r));
  base = `http://127.0.0.1:${server.address().port}`;
  browser = await chromium.launch({ channel: "msedge", headless: true });
});
after(async () => {
  await browser?.close();
  await new Promise((r) => server?.close(r));
});
async function fixture({
  anonymous = false,
  navigationStatus = 200,
  width = 1440,
  navigationPaths = paths,
  operationPermissions = ["INCIDENT_VIEW", "INCIDENT_CREATE", "MAINTENANCE_VIEW", "MAINTENANCE_EXECUTE", "MAINTENANCE_REVIEW", "INCIDENT_TRIAGE", "MR_CREATE", "MR_VIEW"],
} = {}) {
  const page = await browser.newPage({ viewport: { width, height: 960 } });
  page.setDefaultTimeout(10000);
  const errors = [],
    writes = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await page.route(base + "/api/**", async (route) => {
    const url = new URL(route.request().url());
    if (route.request().method() !== "GET") writes.push(url.pathname);
    let status = 200,
      body;
    if (url.pathname === "/api/auth/session") {
      status = anonymous ? 401 : 200;
      body = anonymous ? { code: "AUTHENTICATION_REQUIRED" } : session;
    } else if (url.pathname === "/api/auth/navigation/operation-grants")
      body = { grants: operationPermissions.map(permission => ({ branchId: null, permission, scope: "GLOBAL" })) };
    else if (url.pathname === "/api/auth/csrf")
      body = { csrfToken: "review-only-token", headerName: "X-CSRF-TOKEN" };
    else if (url.pathname === "/api/auth/navigation") {
      status = navigationStatus;
      body = { paths: navigationPaths };
    } else if (url.pathname === "/api/users")
      body = {
        items: url.searchParams.has("search") ? [] : [account],
        page: 1,
        pageSize: 20,
        totalCount: url.searchParams.has("search") ? 0 : 1,
      };
    else {
      status = 503;
      body = { status: 503, title: "Unavailable fixture" };
    }
    await route.fulfill({
      status,
      contentType: "application/json",
      body: JSON.stringify(body),
    });
  });
  return { page, errors, writes };
}
test("incident menu has one entry and reporting remains available inside the authorized page", async () => {
  const {page,errors}=await fixture({navigationPaths:["/operations/incidents","/incidents/new"]});
  try {
    await page.route(base+"/api/incidents?**",r=>r.fulfill({json:{items:[],page:1,pageSize:20,totalCount:0}}));
    await page.goto(base+"/operations/incidents?branchId=1");
    await page.getByRole("region",{name:"Danh sách sự cố",exact:true}).waitFor();
    const menu=page.locator('.cacao-subnav');
    assert.equal(await menu.getByRole('link',{name:'Sự cố',exact:true}).count(),1);
    assert.equal(await menu.getByRole('link',{name:'Báo sự cố',exact:true}).count(),0);
    const report=page.locator('.dev2-workspace').getByRole('link',{name:'Báo sự cố',exact:true});
    assert.equal(await report.getAttribute('href'),'/operations/incidents?action=A004A-021&branchId=1');
    await report.click();
    await page.locator('[name="incidentTitle"]').waitFor();
    assert.deepEqual(errors,[]);
  } finally { await page.close(); }
});

test("incident permission loading uses a spinning indicator without visible technical text", async () => {
  const {page,errors}=await fixture({navigationPaths:["/operations/incidents"]});
  let release;const gate=new Promise(resolve=>{release=resolve});
  try {
    await page.route(base+"/api/auth/navigation/operation-grants",async r=>{await gate;await r.fulfill({json:{grants:[{permission:'INCIDENT_VIEW',scope:'BRANCH',branchId:1}]}})});
    await page.route(base+"/api/incidents?**",r=>r.fulfill({json:{items:[],page:1,pageSize:20,totalCount:0}}));
    await page.goto(base+"/operations/incidents?branchId=1");
    const loading=page.getByRole('status',{name:'Đang kiểm tra quyền',exact:true});
    await loading.waitFor();
    assert.equal(await loading.innerText(),'');
    assert.equal(await loading.locator('.loading-indicator__ring').evaluate(e=>getComputedStyle(e).animationName),'hotelops-loading');
    await page.screenshot({path:'../artifacts/vietnamese-ui/menu-spinner.png',fullPage:true});
    release();
    await page.getByRole('region',{name:'Danh sách sự cố',exact:true}).waitFor();
    assert.equal(await loading.count(),0);
    assert.equal(await page.locator('.dev2-workspace').getByRole('link',{name:'Báo sự cố',exact:true}).count(),0);
    assert.deepEqual(errors,[]);
  } finally { release();await page.close(); }
});

test("incident report selects the assigned branch and hides technical fields", async () => {
  const { page, errors } = await fixture({ navigationPaths: ["/incidents/new"] });
  try {
    await page.route(base + "/api/incidents/creation-branches", route => route.fulfill({
      contentType: "application/json", body: JSON.stringify([{ branchId: 1, name: "Chi nhánh Hà Nội" }]),
    }));
    await page.goto(base + "/incidents/new");
    const branch = page.locator('select[name="branchId"]');
    await page.waitForFunction(() => document.querySelector('select[name="branchId"]')?.value === "1");
    assert.equal(await branch.locator('option:checked').textContent(), "Chi nhánh Hà Nội");
    assert.equal(await page.locator('input[name="branchId"], input[name="reportedBy"], input[name="priorityId"], input[name="severityId"]').count(), 0);
    for (const name of ["incidentTitle", "description", "discoveredAt"])
      assert.equal(await page.locator(`[name="${name}"]`).getAttribute("required"), "");
    assert.deepEqual(errors, []);
  } finally { await page.close(); }
});

test("incident report blocks missing branch access and preserves a source branch", async () => {
  for (const query of ["", "&branchId=2&roomId=19"]) {
    const { page } = await fixture({ navigationPaths: ["/incidents/new"] });
    try {
      await page.route(base + "/api/incidents/creation-branches", route => route.fulfill({
        contentType: "application/json", body: JSON.stringify(query ? [{ branchId: 1, name: "Assigned" }] : []),
      }));
      await page.goto(base + "/operations/incidents?action=A004A-021" + query);
      await page.getByText("Bạn chưa có quyền báo sự cố tại chi nhánh này.", { exact: true }).waitFor();
      assert.equal(await page.locator('select[name="branchId"]').evaluate(e => e.checkValidity()), false);
      if (query) assert.equal(await page.locator('[name="roomNumber"]').count(), 1);
    } finally { await page.close(); }
  }
});

test("incident rooms filter by prefix and assets follow the selected room", async () => {
  const { page, errors } = await fixture({ navigationPaths: ["/incidents/new"] });
  try {
    await page.route(base + "/api/incidents/creation-branches", route => route.fulfill({ contentType: "application/json", body: JSON.stringify([{ branchId: 1, name: "Branch" }]) }));
    await page.route(base + "/api/incidents/creation-branches/1/locations", route => route.fulfill({ contentType: "application/json", body: JSON.stringify({
      rooms: [{id: 10, name: "203", areaId: 1}, {id: 11, name: "204", areaId: 1}, {id: 12, name: "302", areaId: null}],
      areas: [{id: 1, name: "Tầng 2"}, {id: 2, name: "Sảnh"}],
      assets: [{id: 20, name: "Điều hòa 203", code: "AC203", roomId: 10, areaId: null}, {id: 21, name: "TV 204", roomId: 11, areaId: 1}],
    }) }));
    await page.goto(base + "/incidents/new");
    const room = page.locator('[name="roomNumber"]');
    await page.waitForFunction(() => document.querySelectorAll('datalist option').length === 3);
    await room.fill("2");
    assert.deepEqual(await page.locator('datalist option').evaluateAll(nodes => nodes.map(n => n.value)), ["203", "204"]);
    assert.equal(await room.evaluate(e => e.checkValidity()), false);
    await room.fill("203");
    assert.equal(await room.evaluate(e => e.checkValidity()), true);
    const asset = page.locator('[name="assetId"]');
    assert.deepEqual(await asset.locator('option').allTextContents(), ["Không chọn tài sản", "Điều hòa 203 (AC203)"]);
    await asset.selectOption("20");
    await room.fill("204");
    assert.equal(await asset.inputValue(), "");
    assert.deepEqual(await asset.locator('option').allTextContents(), ["Không chọn tài sản", "TV 204"]);
    assert.equal(await page.locator('[name="areaId"]').isDisabled(), true);
    await room.fill("");
    await page.locator('[name="areaId"]').selectOption("2");
    assert.equal(await room.evaluate(e => e.checkValidity()), true);
    assert.deepEqual(errors, []);
  } finally { await page.close(); }
});

test("background permission refresh preserves drafts and still enforces revocation", async () => {
  const {page,errors}=await fixture({navigationPaths:['/operations/incidents'],operationPermissions:['INCIDENT_VIEW']});
  try {
    await page.clock.install();
    await page.route(base+'/api/incidents?**',r=>r.fulfill({json:{items:[],page:1,pageSize:20,totalCount:0}}));
    await page.goto(base+'/operations/incidents?branchId=1');
    const input=page.getByLabel('Tìm theo mã hoặc tiêu đề',{exact:true});await input.fill('bản nháp đang nhập');
    await page.evaluate(()=>{window.savedInput=document.querySelector('input[name="keyword"]');window.savedMenu=document.querySelector('.cacao-subnav nav');});
    let release;const gate=new Promise(r=>release=r);let reads=0;
    await page.route(base+'/api/auth/navigation/operation-grants',async r=>{reads++;await gate;await r.fulfill({json:{grants:[{permission:'INCIDENT_VIEW',branchId:1,scope:'BRANCH'}]}});});
    await page.clock.fastForward(31000);
    await page.waitForFunction(()=>document.querySelector('input[name="keyword"]')===window.savedInput);
    await page.evaluate(()=>{window.dispatchEvent(new Event('focus'));window.dispatchEvent(new Event('focus'));});
    assert.equal(await input.inputValue(),'bản nháp đang nhập');assert.equal(await input.isVisible(),true);
    release();await page.waitForResponse(r=>r.url().endsWith('/operation-grants'));assert.equal(reads,1);
    await page.route(base+'/api/auth/navigation/operation-grants',r=>r.fulfill({status:503,json:{}}));
    await page.evaluate(()=>window.dispatchEvent(new Event('focus')));
    await page.getByRole('status').filter({hasText:'Không xác nhận được quyền'}).waitFor();
    assert.equal(await input.isVisible(),false);
    await page.route(base+'/api/auth/navigation/operation-grants',r=>r.fulfill({json:{grants:[{permission:'INCIDENT_VIEW',branchId:1,scope:'BRANCH'}]}}));
    await page.getByRole('button',{name:'Thử lại',exact:true}).click();await input.waitFor({state:'visible'});
    assert.equal(await input.inputValue(),'bản nháp đang nhập');assert.equal(await page.evaluate(()=>window.savedInput===document.querySelector('input[name="keyword"]')),true);
    await page.route(base+'/api/auth/navigation/operation-grants',r=>r.fulfill({json:{grants:[]}}));
    await page.evaluate(()=>window.dispatchEvent(new Event('focus')));
    await page.getByRole('alert').filter({hasText:'Bạn không có quyền'}).waitFor();assert.equal(await input.count(),0);assert.deepEqual(errors,[]);
  } finally {await page.close();}
});

test("slow background navigation keeps the menu visible and 401 hides it", async () => {
 const {page,errors}=await fixture({navigationPaths:['/operations/incidents'],operationPermissions:['INCIDENT_VIEW']});
 try {
  await page.route(base+'/api/incidents?**',r=>r.fulfill({json:{items:[],page:1,pageSize:20,totalCount:0}}));
  await page.goto(base+'/operations/incidents?branchId=1');
  const menu=page.locator('.cacao-subnav').getByRole('link',{name:'Sự cố',exact:true});await menu.waitFor();
  let release;const gate=new Promise(r=>release=r);let requests=0;
  await page.route(base+'/api/auth/navigation',async r=>{requests++;await gate;await r.fulfill({json:{paths:['/operations/incidents']}});});
  const requested=page.waitForRequest(r=>r.url().endsWith('/api/auth/navigation'));
  await page.evaluate(()=>window.dispatchEvent(new Event('focus')));
  await requested;
  assert.equal(await menu.isVisible(),true);assert.equal(await page.getByRole('status',{name:'Đang tải điều hướng'}).count(),0);
  const responded=page.waitForResponse(r=>r.url().endsWith('/api/auth/navigation'));release();await responded;assert.ok(requests<=2);
  await page.route(base+'/api/auth/navigation',r=>r.fulfill({status:401,json:{}}));
  await page.evaluate(()=>window.dispatchEvent(new Event('focus')));
  await menu.waitFor({state:'hidden'});assert.equal(await page.locator('.cacao-navigation').count(),0);assert.deepEqual(errors,[]);
 } finally {await page.close();}
});

test("maintenance lead only sees permitted modules and direct forbidden actions are blocked", async () => {
  const { page, errors } = await fixture({ navigationPaths: ["/profile", "/operations/incidents", "/operations/maintenance", "/incidents/new"],
    operationPermissions: ["INCIDENT_VIEW", "INCIDENT_CREATE", "INCIDENT_TRIAGE", "MAINTENANCE_VIEW", "MAINTENANCE_EXECUTE", "MAINTENANCE_REVIEW", "PM_MANAGE", "MR_CREATE"] });
  try {
    await page.route(base + "/api/auth/navigation/operation-grants", route => route.fulfill({json:{grants:["INCIDENT_VIEW", "INCIDENT_CREATE", "INCIDENT_TRIAGE", "MAINTENANCE_VIEW", "MAINTENANCE_EXECUTE", "MAINTENANCE_REVIEW", "PM_MANAGE", "MR_CREATE"].map(permission => ({branchId:1,permission,scope:"BRANCH"}))}}));
    let listReads = 0;
    await page.route(base + "/api/incidents?**", route => { listReads++; return route.fulfill({json:{items:[],page:1,pageSize:20,totalCount:0}}); });
    await page.goto(base + "/operations/incidents");
    await page.waitForURL("**/operations/incidents?branchId=1");
    await page.getByRole("region", {name:"Danh sách sự cố",exact:true}).waitFor();
    assert.ok(listReads > 0);
    const nav = page.getByRole("navigation", { name: "Vận hành", exact: true });
    await nav.waitFor();
    assert.deepEqual(await nav.locator("a").allTextContents(), ["Sự cố", "Công việc bảo trì"]);
    assert.equal(await page.getByLabel("Thao tác", {exact:true}).count(), 0);
    assert.equal(await page.locator('.dev2-workspace').getByRole("link", {name:"Tạo mới",exact:true}).count(), 0);
    await page.route(base + "/api/auth/navigation/operation-grants", route => route.fulfill({ status: 503, contentType: "application/json", body: "{}" }));
    await page.evaluate(() => window.dispatchEvent(new Event("focus")));
    await page.getByRole("status").filter({hasText:"Không xác nhận được quyền"}).waitFor();
    assert.equal(await page.getByLabel("Thao tác", {exact:true}).count(), 0);
    await page.unroute(base + "/api/auth/navigation/operation-grants");
    await page.goto(base + "/operations/material-requests?action=A004A-043");
    await page.getByRole("alert").filter({hasText:"Bạn không có quyền"}).waitFor();
    assert.equal(await page.locator('form[data-operation]').count(), 0);
    await page.goto(base + "/operations/incidents?action=A004A-019");
    await page.getByRole("alert").filter({hasText:"Bạn không có quyền"}).waitFor();
    assert.deepEqual(errors, []);
  } finally { await page.close(); }
});

test("room detail hides cleaning and inspection actions without access", async () => {
  const { page, errors } = await fixture({ navigationPaths: ["/rooms/status", "/incidents/new"] });
  const data = await import("./room-status-fixtures.mjs");
  try {
    await page.route("**/api/rooms/11?**", r => r.fulfill({json: {...data.detail, currentCycle:null, effectiveBlocks:[]}}));
    await page.route("**/api/rooms/11/history?**", r => r.fulfill({json: data.history}));
    await page.route("**/api/branches/**/cleaning-task-options?**", r => r.fulfill({status:403,json:{}}));
    await page.goto(base + "/rooms/status?branchId=7&roomId=11");
    const region = page.getByRole("region", {name:"Hành động tiếp theo",exact:true});
    await region.waitFor();
    assert.equal(await region.getByRole("button", {name:"Tạo công việc vệ sinh",exact:true}).count(), 0);
    assert.equal(await region.getByRole("link", {name:"Tạo công việc vệ sinh",exact:true}).count(), 0);
    assert.equal(await region.getByRole("button", {name:"Assign room inspection",exact:true}).count(), 0);
    assert.deepEqual(errors, []);
  } finally { await page.close(); }
});

test("incident table keeps identifiers intact and scrolls within desktop and mobile", async () => {
  for (const width of [1440, 390]) {
    const {page, errors} = await fixture({width,navigationPaths:["/operations/incidents"]});
    try {
      await page.route(base + "/api/incidents?**", r => r.fulfill({json:{page:1,pageSize:20,totalCount:1,items:[{
        incidentId:1,branchId:1,incidentNo:"INC-20261118-001",incidentTitle:"Test incident",status:"OPEN",isOverdue:null,
        branchName:"Khách sạn Đức Vượng - Chi nhánh Bùi Viện",roomNumber:"201",assetName:"Tivi Samsung 43 inch phòng 201",slaUnavailableReason:"INCIDENT_SLA_NOT_RECORDED_OR_INVALID"
      }]}}));
      await page.goto(base + "/operations/incidents?branchId=1");
      const table = page.locator('.incident-table-scroll');
      await table.waitFor();
      assert.equal(await table.evaluate(e => e.scrollWidth > e.clientWidth), true);
      assert.equal(await page.locator('.incident-reference').evaluate(e => getComputedStyle(e).whiteSpace), "nowrap");
      assert.equal(await page.locator('.incident-status').textContent(), "Đang mở");
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
      assert.equal(await table.getByText("N/A", {exact:true}).count(), 0);
      assert.deepEqual(errors, []);
    } finally { await page.close(); }
  }
});

test("housekeeping reporter can open incident creation from home and the work menu", async () => {
  const { page, errors, writes } = await fixture({
    navigationPaths: ["/profile", "/notifications", "/cleaning/tasks", "/incidents/new"],
  });
  try {
    await page.goto(base + "/home");
    const report = page.locator(".home-workspace").getByRole("link", { name: "Báo sự cố", exact: true });
    await report.waitFor();
    await page.getByRole("button", { name: "Công việc", exact: true }).click();
    const menu = page.locator(".cacao-subnav");
    assert.equal(await menu.getByRole("link", { name: "Báo sự cố", exact: true }).getAttribute("href"), "/incidents/new");
    assert.equal(await menu.getByRole("link", { name: "Sự cố", exact: true }).count(), 0);
    await report.click();
    await page.waitForURL("**/operations/incidents?action=A004A-021");
    await page.getByRole("button", { name: "Công việc", exact: true }).click();
    await menu.getByRole("link", { name: "Báo sự cố", exact: true }).click();
    await page.waitForURL("**/operations/incidents?action=A004A-021");
    assert.deepEqual(errors, []);
    assert.deepEqual(writes, []);
  } finally {
    await page.close();
  }
});

test("incident creation is absent when navigation does not grant the entry", async () => {
  const { page } = await fixture({ navigationPaths: ["/profile", "/notifications", "/cleaning/tasks"] });
  try {
    await page.goto(base + "/home");
    await page.locator(".home-workspace").getByRole("link", { name: "Công việc buồng phòng", exact: true }).waitFor();
    assert.equal(await page.getByRole("link", { name: "Báo sự cố", exact: true }).count(), 0);
    assert.equal(await page.getByRole("button", { name: "Công việc", exact: true }).count(), 0);
  } finally {
    await page.close();
  }
});

test("housekeeping menu exposes requests, workday and account destinations", async () => {
  const navigationPaths = ["/profile", "/change-password", "/notifications", "/cleaning/tasks", "/rework",
    "/operational-tasks", "/incidents/new", "/internal-requests/new", "/internal-requests",
    "/material-requests/new", "/operations/material-requests", "/workday", "/workday/scheduled",
    "/my-time-log", "/time-corrections"];
  const { page, errors, writes } = await fixture({ navigationPaths });
  try {
    await page.goto(base + "/home");
    await page.locator('.home-workspace a[href="/material-requests/new"]').waitFor();
    for (const path of navigationPaths) {
      assert.equal(await page.locator(`.home-workspace a[href="${path}"]`).count(), 1, path);
    }
    await page.getByRole("button", { name: "Nguồn lực", exact: true }).click();
    await page.locator('.cacao-subnav').getByRole("link", { name: "Tạo yêu cầu vật tư", exact: true }).click();
    await page.waitForURL("**/operations/material-requests?action=A004A-043");
    assert.deepEqual(errors, []);
    assert.deepEqual(writes, []);
  } finally { await page.close(); }
});

test("uncertain logout retains its feedback while removing protected navigation", async () => {
  const { page, writes } = await fixture();
  try {
    await page.goto(base + "/profile");
    await page.getByRole("button", { name: "Đăng xuất", exact: true }).click();
    await page
      .getByText(
        "Chưa xác nhận đăng xuất. Xác thực chưa khả dụng. Kiểm tra phiên trước khi thử lại.",
        { exact: true },
      )
      .waitFor();
    assert.equal(await page.locator(".cacao-shell").count(), 0);
    assert.deepEqual(writes, ["/api/auth/logout"]);
  } finally {
    await page.close();
  }
});
test("C+8 stylesheet, quick view, directory filters and two-level routing", async () => {
  const { page, errors, writes } = await fixture();
  try {
    await page.goto(base + "/users");
    await page.locator(".user-directory-name").waitFor();
    assert.equal(
      await page
        .locator(".cacao-rail")
        .evaluate((e) => getComputedStyle(e).backgroundColor),
      "rgb(57, 43, 38)",
    );
    assert.equal(
      await page
        .locator(".cacao-shell")
        .evaluate((e) => getComputedStyle(e).gridTemplateColumns.split(" ")[0]),
      "292px",
    );
    await page.getByRole("button", { name: "Xem nhanh Review user" }).click();
    assert.equal(
      await page
        .getByRole("link", { name: "Xem hồ sơ đầy đủ" })
        .getAttribute("href"),
      "/users/7",
    );
    await page.getByRole("button", { name: "Đóng xem nhanh" }).click();
    await page.getByLabel("Tìm theo tên, tài khoản hoặc email").fill("missing");
    await page.getByRole("button", { name: "Áp dụng bộ lọc" }).click();
    await page
      .getByText("MSG10: Không có hồ sơ phù hợp bộ lọc đã áp dụng.")
      .waitFor();
    await page.getByRole("button", { name: "Xóa bộ lọc" }).click();
    await page.locator(".user-directory-name").waitFor();
    await page
      .getByRole("button", { name: "Buồng phòng", exact: true })
      .click();
    await page
      .getByRole("link", { name: "Trạng thái phòng", exact: true })
      .click();
    await page
      .getByRole("heading", { name: "Trạng thái vận hành phòng" })
      .waitFor();
    assert.equal(
      await page.locator('[aria-label="Authorized home navigation"]').count(),
      0,
    );
    assert.deepEqual(errors, []);
    assert.deepEqual(writes, []);
  } finally {
    await page.close();
  }
});
test("mobile menu and representative routes have no page overflow", async () => {
  const { page, errors } = await fixture({ width: 390 });
  try {
    for (const path of [
      "/users",
      "/rooms/status",
      "/cleaning/tasks",
      "/hotel",
      "/operations/incidents",
      "/inventory/items",
      "/profile",
    ]) {
      await page.goto(base + path);
      await page.locator(".cacao-shell").waitFor();
      assert.equal(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth,
        ),
        true,
        path,
      );
    }
    await page.getByRole("button", { name: "Mở điều hướng" }).click();
    await page.getByRole("button", { name: "Người dùng", exact: true }).click();
    await page
      .getByRole("navigation", { name: "Chức năng" })
      .getByRole("link", { name: "Người dùng", exact: true })
      .click();
    await page.getByRole("heading", { name: "Danh mục người dùng" }).waitFor();
    assert.equal(await page.locator(".cacao-navigation").isVisible(), false);
    assert.deepEqual(errors, []);
  } finally {
    await page.close();
  }
});
test("navigation error ends loading and anonymous login has no protected shell", async () => {
  const a = await fixture({ navigationStatus: 503 });
  try {
    await a.page.goto(base + "/users");
    await a.page.getByText("Chưa có chức năng khả dụng.").waitFor();
    assert.equal(await a.page.locator(".cacao-rail button").count(), 0);
  } finally {
    await a.page.close();
  }
  const b = await fixture({ anonymous: true, width: 390 });
  try {
    await b.page.goto(base + "/login");
    await b.page
      .getByRole("heading", { name: "Đăng nhập", exact: true })
      .waitFor();
    assert.equal(await b.page.locator(".cacao-shell").count(), 0);
    assert.equal(
      await b.page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
      true,
    );
  } finally {
    await b.page.close();
  }
});

test("home groups authorized functions, filters and fits desktop/mobile", async () => {
  for (const width of [1440, 390]) {
    const { page, errors } = await fixture({ width });
    try {
      await page.goto(base + '/home');
      const home = page.locator('.home-workspace');
      await home.getByRole('link', { name: 'Quản lý người dùng' }).waitFor();
      assert.equal(await home.getByRole('link', { name: 'Bổ sung vật tư' }).count(), 0);
      await home.getByRole('searchbox', { name: 'Tìm chức năng' }).fill('nguoi dung');
      assert.equal(await home.locator('.home-group a').count(), 1);
      await home.getByRole('searchbox').fill('zzzz');
      await home.getByRole('status').filter({hasText:'Không tìm thấy'}).waitFor();
      await home.getByRole('searchbox').fill('');
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
      await page.screenshot({path:`../.impeccable/review/cacao/home-${width}.png`,fullPage:true});
      await home.getByRole('link', { name: 'Quản lý người dùng' }).click();
      await page.getByRole('heading', {name:"Danh mục người dùng"}).waitFor();
      assert.deepEqual(errors, []);
    } finally { await page.close(); }
  }
});

test('home fixed personal button persists and makes no statistics requests', async () => {
  for(const width of [1440,390]) {
    const {page,errors}=await fixture({width});
    const requests=[];
    page.on('request', r=>{if(r.url().includes('/api/')) requests.push(new URL(r.url()).pathname);});
    try {
      await page.goto(base+'/home');
      const button=page.locator('.home-personal-toggle');
      await button.waitFor();
      assert.equal(await button.evaluate(e=>getComputedStyle(e).position),'fixed');
      const expanded=await button.getAttribute('aria-expanded');
      await button.click();
      assert.equal(await button.getAttribute('aria-expanded'),expanded==='true'?'false':'true');
      await page.reload();
      await button.waitFor();
      assert.equal(await button.getAttribute('aria-expanded'),expanded==='true'?'false':'true');
      assert.equal(await page.locator('.home-statistics').count(),0);
      assert.equal(requests.some(p=>p.startsWith('/api/dashboards') || p==='/api/users'),false);
      assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
      await page.screenshot({path:`../.impeccable/review/cacao/home-fixed-${width}.png`,fullPage:true});
      assert.deepEqual(errors,[]);
    } finally {await page.close();}
  }
});

test('room directory filters align and remain usable at desktop and mobile widths', async () => {
  for(const width of [1440,390]) {
    const {page,errors}=await fixture({width});
    try {
      await page.goto(base+'/room-directory');
      const scope=page.locator('.room-directory-workspace');
      await scope.waitFor();
      const filters=scope.locator('section[aria-label="Chọn cấu trúc khách sạn"]');
      assert.equal(await filters.evaluate(e=>getComputedStyle(e).display),'grid');
      await scope.locator('input[name="branchId"]').fill('1');
      assert.equal(await scope.getByRole('button',{name:"Tải dữ liệu"}).isEnabled(),true);
      await scope.getByRole('combobox',{name:"Thao tác",exact:true}).selectOption('detail');
      await scope.locator('input[name="recordId"]').fill('2');
      assert.equal(await scope.getByRole('button',{name:"Tải dữ liệu"}).isEnabled(),true);
      await scope.getByRole('combobox',{name:"Thao tác",exact:true}).selectOption('browse');
      assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
      await page.screenshot({path:`../.impeccable/review/cacao/room-directory-layout-${width}.png`,fullPage:true});
      assert.deepEqual(errors,[]);
    } finally {await page.close();}
  }
});
