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
    define: { "process.env.NODE_ENV": '"production"' },
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
    } else if (url.pathname === "/api/auth/csrf")
      body = { csrfToken: "review-only-token", headerName: "X-CSRF-TOKEN" };
    else if (url.pathname === "/api/auth/navigation") {
      status = navigationStatus;
      body = { paths };
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
test("uncertain logout retains its feedback while removing protected navigation", async () => {
  const { page, writes } = await fixture();
  try {
    await page.goto(base + "/profile");
    await page.getByRole("button", { name: "Logout", exact: true }).click();
    await page
      .getByText(
        "Sign-out could not be confirmed. Authentication is currently unavailable. Please check your session before trying again.",
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
    await page.getByRole("button", { name: "Quick view Review user" }).click();
    assert.equal(
      await page
        .getByRole("link", { name: "View full profile" })
        .getAttribute("href"),
      "/users/7",
    );
    await page.getByRole("button", { name: "Close quick view" }).click();
    await page.getByLabel("Search name, username or email").fill("missing");
    await page.getByRole("button", { name: "Apply filters" }).click();
    await page
      .getByText("MSG10: No records match the applied filters.")
      .waitFor();
    await page.getByRole("button", { name: "Clear filters" }).click();
    await page.locator(".user-directory-name").waitFor();
    await page
      .getByRole("button", { name: "Buồng phòng", exact: true })
      .click();
    await page
      .getByRole("link", { name: "Room status board", exact: true })
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
      .getByRole("link", { name: "Users", exact: true })
      .click();
    await page.getByRole("heading", { name: "User directory" }).waitFor();
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
      .getByRole("heading", { name: "Sign In", exact: true })
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
      await page.getByRole('heading', {name:'User directory'}).waitFor();
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
      const filters=scope.locator('section[aria-label="Hotel structure selectors"]');
      assert.equal(await filters.evaluate(e=>getComputedStyle(e).display),'grid');
      await scope.locator('input[name="branchId"]').fill('1');
      assert.equal(await scope.getByRole('button',{name:'Load from server'}).isEnabled(),true);
      await scope.getByRole('combobox',{name:'Action',exact:true}).selectOption('detail');
      await scope.locator('input[name="recordId"]').fill('2');
      assert.equal(await scope.getByRole('button',{name:'Load from server'}).isEnabled(),true);
      await scope.getByRole('combobox',{name:'Action',exact:true}).selectOption('browse');
      assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
      await page.screenshot({path:`../.impeccable/review/cacao/room-directory-layout-${width}.png`,fullPage:true});
      assert.deepEqual(errors,[]);
    } finally {await page.close();}
  }
});
