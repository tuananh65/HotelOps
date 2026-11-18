import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync, mkdirSync } from "node:fs";
import { build } from "esbuild";
import { chromium, expect } from "@playwright/test";

const output = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `
import React,{useState} from 'react';import{createRoot}from'react-dom/client';
import{BrowserRouter,Routes,Route,Link}from'react-router-dom';
import{AuthProvider}from'./src/auth/AuthProvider';
import{CollapsibleFilterPanel,DeferredFilterPanel,ActionDialog,WorkflowFeedbackHost}from'./src/components/workspace-interactions';
import{ItemManagementPage}from'./src/inventory/ItemManagementPage';
import './src/styles/design-system.css';
function Filters(){const[draft,setDraft]=useState('');const[applied,setApplied]=useState('');const[page,setPage]=useState(1);const[calls,setCalls]=useState(0);
return <main><h1>Kiểm thử bộ lọc</h1><label>Tìm nhanh<input type="search"/></label>
<DeferredFilterPanel activeCount={Number(!!applied)} onOpen={()=>setDraft(applied)} onReset={()=>setDraft('')} onApply={()=>{setApplied(draft);setPage(1);setCalls(n=>n+1)}}>
<label>Trạng thái<select value={draft} onChange={e=>setDraft(e.target.value)}><option value="">Tất cả</option><option value="OPEN">Đang mở</option></select></label></DeferredFilterPanel>
<p data-testid="result">{applied||'Tất cả'} / {page} / {calls}</p><button onClick={()=>setPage(p=>p+1)}>Trang sau</button><Link to="/other">Trang khác</Link></main>}
function NestedDemo(){return <ActionDialog title="Chi tiết"><p>Thông tin tĩnh</p><ActionDialog title="Chỉnh sửa"><form onSubmit={e=>e.preventDefault()}><label>Nội dung<input/></label><button data-dialog-action>Lưu con</button></form></ActionDialog></ActionDialog>}
function ModalDemo(){const[busy,setBusy]=useState(false);return <main><ActionDialog title="Phân công" busy={busy}><form onSubmit={e=>{e.preventDefault();setBusy(true)}}><label>Lý do<input required/></label><button>Lưu</button></form></ActionDialog><button onClick={()=>setBusy(false)}>Dừng thử nghiệm</button><table><tbody><tr><td>Bảng phía sau</td></tr></tbody></table></main>}
createRoot(document.getElementById('root')).render(<AuthProvider><BrowserRouter><WorkflowFeedbackHost/><Routes><Route path="/filters" element={<Filters/>}/><Route path="/other" element={<Filters/>}/><Route path="/nested" element={<NestedDemo/>}/><Route path="/modal" element={<ModalDemo/>}/><Route path="*" element={<ItemManagementPage/>}/></Routes></BrowserRouter></AuthProvider>);
`,
  },
  outfile: "p1.js",
  bundle: true,
  write: false,
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"test"' },
});
let browser;
before(async () => {
  mkdirSync("../artifacts/ui-p1/screenshots", { recursive: true });
  const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
  browser = await chromium.launch({
    headless: true,
    ...(existsSync(edge) ? { executablePath: edge } : {}),
  });
});
after(async () => browser?.close());
async function fixture(
  path,
  run,
  { width = 1440, fail = false, denied = false } = {},
) {
  const context = await browser.newContext({
      viewport: { width, height: 960 },
    }),
    page = await context.newPage();
  page.setDefaultTimeout(12000);
  const calls = [],
    errors = [];
  let items = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await page.route("**/*", async (route) => {
    const req = route.request(),
      u = new URL(req.url());
    calls.push({ path: u.pathname, method: req.method() });
    if (u.pathname === "/api/auth/session")
      return route.fulfill({
        json: {
          user: { id: "1", username: "tester", name: "Người kiểm thử" },
          mustChangePassword: false,
          expiresAt: "2099-01-01T00:00:00Z",
          absoluteExpiresAt: "2099-01-02T00:00:00Z",
          csrfToken: "test",
        },
      });
    if (u.pathname === "/api/auth/csrf")
      return route.fulfill({
        json: { headerName: "X-CSRF-TOKEN", csrfToken: "test" },
      });
    if (u.pathname === "/api/item-management")
      return route.fulfill({
        status: denied ? 403 : 200,
        json: denied
          ? { detail: "Không có quyền" }
          : {
              items: { items, totalCount: items.length, page: 1, pageSize: 25 },
              categories: [{ id: 2, name: "Điện", isActive: true }],
              units: [{ id: 3, name: "Cái", isActive: true }],
            },
      });
    if (u.pathname === "/api/inventory/items" && req.method() === "POST") {
      await new Promise((r) => setTimeout(r, 100));
      if (fail)
        return route.fulfill({
          status: 400,
          json: { detail: "Tên vật tư không hợp lệ" },
        });
      items = [{ ...req.postDataJSON(), itemId: 7, rowVersion: "fixture" }];
      return route.fulfill({ status: 201, json: { itemId: 7 } });
    }
    if (u.pathname.startsWith("/api/"))
      return route.fulfill({ status: 403, json: {} });
    return route.fulfill({
      contentType: "text/html",
      body: '<!doctype html><html lang="vi"><meta name="viewport" content="width=device-width,initial-scale=1"><div id="root"></div></html>',
    });
  });
  async function mount() {
    await page.addStyleTag({
      content: output.outputFiles.find((f) => f.path.endsWith(".css")).text,
    });
    await page.addScriptTag({
      content: output.outputFiles.find((f) => f.path.endsWith(".js")).text,
    });
  }
  await page.goto("https://hotelops.test" + path);
  await mount();
  try {
    await run(page, calls, mount);
    assert.deepEqual(errors, []);
  } finally {
    await context.close();
  }
}
test("P1 filters start closed, toggle without requests and discard unapplied drafts", () =>
  fixture("/filters", async (page, calls) => {
    await expect(page.getByLabel("Trạng thái")).toBeHidden();
    const before = calls.length;
    await page.getByRole("button", { name: "Bộ lọc", exact: true }).click();
    await page.getByLabel("Trạng thái").selectOption("OPEN");
    await page.getByRole("button", { name: "Ẩn bộ lọc", exact: true }).click();
    await expect(page.getByTestId("result")).toHaveText("Tất cả / 1 / 0");
    await page.getByRole("button", { name: "Bộ lọc", exact: true }).click();
    await expect(page.getByLabel("Trạng thái")).toHaveValue("");
    assert.equal(calls.length, before);
  }));
test("P1 apply/reset, search and pagination preserve applied filters", () =>
  fixture("/filters", async (page) => {
    await page.getByLabel("Tìm nhanh").fill("203");
    await page.getByRole("button", { name: "Bộ lọc", exact: true }).click();
    await page.getByLabel("Trạng thái").selectOption("OPEN");
    await page.getByRole("button", { name: "Áp dụng bộ lọc" }).click();
    await expect(page.getByTestId("result")).toHaveText("OPEN / 1 / 1");
    await page.getByRole("button", { name: "Ẩn bộ lọc (1)" }).click();
    await page.getByRole("button", { name: "Trang sau" }).click();
    await expect(page.getByTestId("result")).toHaveText("OPEN / 2 / 1");
    await expect(page.getByLabel("Tìm nhanh")).toHaveValue("203");
    await page.getByRole("button", { name: "Bộ lọc (1)" }).click();
    await expect(page.getByLabel("Trạng thái")).toHaveValue("OPEN");
    await page.getByRole("button", { name: "Xóa bộ lọc" }).click();
    await page.getByRole("button", { name: "Áp dụng bộ lọc" }).click();
    await expect(page.getByTestId("result")).toHaveText("Tất cả / 1 / 2");
  }));
test("P1 route change and refresh always close filters", () =>
  fixture("/filters", async (page, calls, mount) => {
    await page.getByRole("button", { name: "Bộ lọc", exact: true }).click();
    await page.getByRole("link", { name: "Trang khác" }).click();
    await expect(page.getByLabel("Trạng thái")).toBeHidden();
    await page.getByRole("button", { name: "Bộ lọc", exact: true }).click();
    await page.reload();
    await mount();
    await expect(page.getByLabel("Trạng thái")).toBeHidden();
  }));
async function fillItem(page) {
  await page.getByRole("button", { name: "Tạo vật tư", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "Tạo vật tư" });
  await expect(dialog).toBeVisible();
  await dialog.getByLabel("Mã", { exact: true }).fill("VT203");
  await dialog.getByLabel("Tên", { exact: true }).fill("Bóng đèn");
  await dialog.getByLabel("Loại", { exact: true }).selectOption("2");
  await dialog.getByLabel("Đơn vị cơ sở", { exact: true }).selectOption("3");
  return dialog;
}
test("P1 actual item create modal saves once, closes, shows success and refreshes table", () =>
  fixture("/items", async (page, calls) => {
    const dialog = await fillItem(page);
    await dialog.getByRole("button", { name: "Lưu vật tư" }).dblclick();
    await expect(dialog).not.toBeVisible();
    await expect(
      page.getByRole("cell", { name: "Bóng đèn", exact: true }),
    ).toBeVisible();
    await expect(page.locator(".ds-toast")).toContainText("Đã lưu vật tư");
    assert.equal(
      calls.filter(
        (x) => x.path === "/api/inventory/items" && x.method === "POST",
      ).length,
      1,
    );
  }));
test("P1 actual API failure keeps modal and user input, with a visible error", () =>
  fixture(
    "/items",
    async (page) => {
      const dialog = await fillItem(page);
      await dialog.getByRole("button", { name: "Lưu vật tư" }).click();
      await expect(dialog).toContainText("Tên vật tư không hợp lệ");
      await expect(dialog.getByLabel("Tên", { exact: true })).toHaveValue(
        "Bóng đèn",
      );
      await expect(dialog).toBeVisible();
    },
    { fail: true },
  ));
test("P1 rejected read permission never exposes create workflow", () =>
  fixture(
    "/items",
    async (page) => {
      await expect(
        page.getByText("Không có quyền", { exact: true }),
      ).toBeVisible();
      await expect(
        page.getByRole("button", { name: "Tạo vật tư", exact: true }),
      ).toHaveCount(0);
    },
    { denied: true },
  ));
test("P1 modal restores focus, validates required inputs and keeps table in place", () =>
  fixture("/modal", async (page) => {
    const button = page.getByRole("button", { name: "Phân công", exact: true }),
      table = page.getByRole("table"),
      before = await table.boundingBox();
    await button.click();
    const dialog = page.getByRole("dialog", { name: "Phân công" });
    await expect(dialog).toBeVisible();
    assert.deepEqual(await table.boundingBox(), before);
    await dialog.getByRole("button", { name: "Lưu", exact: true }).click();
    await expect(dialog).not.toHaveAttribute("aria-busy", "true");
    await page.keyboard.press("Escape");
    await expect(dialog).not.toBeVisible();
    await expect(button).toBeFocused();
  }));
test("P1 modal focus stays trapped and cannot close while saving", () =>
  fixture("/modal", async (page) => {
    await page.getByRole("button", { name: "Phân công", exact: true }).click();
    const dialog = page.getByRole("dialog", { name: "Phân công" });
    for (let i = 0; i < 12; i++) {
      await page.keyboard.press("Tab");
      assert.ok(
        await dialog.evaluate((el) => el.contains(document.activeElement)),
      );
    }
    await dialog.getByLabel("Lý do").fill("Kiểm tra phòng");
    await dialog.getByRole("button", { name: "Lưu", exact: true }).click();
    await page.keyboard.press("Escape");
    await expect(dialog).toBeVisible();
    await expect(
      dialog.getByRole("button", { name: "Đóng", exact: true }),
    ).toBeDisabled();
  }));
for (const width of [390, 800])
  test("P1 modal responsive at " + width, () =>
    fixture(
      "/items",
      async (page) => {
        const dialog = await fillItem(page),
          box = await dialog.boundingBox();
        await page.screenshot({
          path: "../artifacts/ui-p1/screenshots/item-modal-" + width + ".png",
          fullPage: true,
        });
        assert.ok(box.x >= 0 && box.x + box.width <= width);
        assert.ok(
          await dialog
            .locator(".ds-dialog-body")
            .evaluate(
              (element) => element.scrollWidth <= element.clientWidth + 1,
            ),
        );
        assert.ok(
          await page.evaluate(
            () => document.documentElement.scrollWidth <= innerWidth,
          ),
        );
      },
      { width },
    ),
  );

test("P1 nested modal owns its footer and releases all scroll locks on unmount", () =>
  fixture("/nested", async (page) => {
    await page.getByRole("button", { name: "Chi tiết", exact: true }).click();
    const parent = page.getByRole("dialog", { name: "Chi tiết", exact: true });
    await expect(
      parent.locator(":scope > .ds-dialog-layout > footer"),
    ).not.toContainText("Lưu con");
    await parent
      .getByRole("button", { name: "Chỉnh sửa", exact: true })
      .click();
    const child = page.getByRole("dialog", { name: "Chỉnh sửa", exact: true });
    await expect(child).toBeVisible();
    await expect(
      child.locator(":scope > .ds-dialog-layout > footer"),
    ).toContainText("Lưu con");
    await page.keyboard.press("Escape");
    await expect(child).not.toBeVisible();
    await expect(parent).toBeVisible();
    assert.equal(
      await page.evaluate(() => document.body.style.overflow),
      "hidden",
    );
    await parent
      .getByRole("button", { name: "Chỉnh sửa", exact: true })
      .click();
    await expect(child).toBeVisible();
    await page.evaluate(() => {
      history.pushState(null, "", "/filters");
      window.dispatchEvent(new PopStateEvent("popstate"));
    });
    await expect(
      page.getByRole("heading", { name: "Kiểm thử bộ lọc" }),
    ).toBeVisible();
    assert.equal(await page.evaluate(() => document.body.style.overflow), "");
  }));
test("P1 dirty close requires confirmation and safe overlay click closes modal", () =>
  fixture("/modal", async (page) => {
    await page.getByRole("button", { name: "Phân công", exact: true }).click();
    const dialog = page.getByRole("dialog", { name: "Phân công" });
    await dialog.getByLabel("Lý do").fill("Chưa lưu");
    page.once("dialog", (prompt) => prompt.dismiss());
    await page.keyboard.press("Escape");
    await expect(dialog).toBeVisible();
    await expect(dialog.getByLabel("Lý do")).toHaveValue("Chưa lưu");
    page.once("dialog", (prompt) => prompt.accept());
    await dialog.getByRole("button", { name: "Hủy", exact: true }).click();
    await expect(dialog).not.toBeVisible();
    await page.getByRole("button", { name: "Phân công", exact: true }).click();
    await page.mouse.click(1, 1);
    await expect(dialog).not.toBeVisible();
  }));
