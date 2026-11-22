import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";

const out = "../artifacts/storekeeper";
fs.mkdirSync(out, { recursive: true });
const reads = [],
  writes = [],
  errors = [];
let conflict = false,
  reduced = false;
const permissions =
  "ATTENDANCE_RECORD INVENTORY_VIEW ITEM_MASTER_MANAGE MR_FULFILL MR_VIEW REPLENISHMENT_CREATE REPLENISHMENT_VIEW REPLENISHMENT_RECEIVE REPLENISHMENT_CLOSE STOCK_RECEIPT_POST STOCK_ISSUE_POST STOCK_RETURN_POST STOCK_TRANSFER_POST STOCK_ADJUST_POST STOCK_REVERSE STOCKTAKE_PERFORM".split(
    " ",
  );
const names = {
  RECEIPT: "Nhập kho",
  ISSUE: "Xuất kho",
  MATERIAL_REQUEST: "Yêu cầu vật tư",
  STOCKTAKE: "Kiểm kê",
  ADJUSTMENT: "Điều chỉnh",
  REPLENISHMENT: "Bổ sung vật tư",
  RETURN: "Trả vật tư",
  REVERSAL: "Đảo giao dịch",
  TRANSFER: "Chuyển kho",
  TRANSFER_RECEIPT: "Nhận chuyển kho",
};
const pageOf = (rows, page = 1, total = rows.length) => ({
  items: rows.map((fields) => ({ fields })),
  page,
  total,
  pageSize: 25,
});
const line = {
  lineId: 1,
  itemId: 1,
  unitId: 1,
  actualQty: 4,
  requestedQty: null,
  conversionFactor: 1,
  rowVersion: "l1",
};
const docs = {
  1: {
    documentId: 1,
    documentNo: "RECEIPT-001",
    kind: "RECEIPT",
    status: "DRAFT",
    createdBy: 1,
    destinationWarehouseId: 1,
    rowVersion: "v1",
    createdAt: "2026-11-23T00:00:00Z",
  },
  2: {
    documentId: 2,
    documentNo: "MR-002",
    kind: "MATERIAL_REQUEST",
    status: "APPROVED",
    createdBy: 8,
    approvedBy: 9,
    sourceWarehouseId: 1,
    rowVersion: "v2",
  },
  3: {
    documentId: 3,
    documentNo: "REPL-003",
    kind: "REPLENISHMENT",
    status: "REQUESTED",
    createdBy: 1,
    destinationWarehouseId: 1,
    rowVersion: "v3",
  },
  4: {
    documentId: 4,
    documentNo: "COUNT-004",
    kind: "STOCKTAKE",
    status: "COUNTED",
    createdBy: 1,
    sourceWarehouseId: 1,
    rowVersion: "v4",
  },
  5: {
    documentId: 5,
    documentNo: "RETURN-005",
    kind: "RETURN",
    status: "DRAFT",
    createdBy: 2,
    destinationWarehouseId: 1,
    sourceDocumentId: 8,
    rowVersion: "v5",
  },
  6: {
    documentId: 6,
    documentNo: "ISSUE-006",
    kind: "ISSUE",
    status: "POSTED",
    createdBy: 1,
    sourceWarehouseId: 1,
    postedAt: "2026-11-23T00:00:00Z",
    rowVersion: "v6",
  },
};
const docLines = Object.fromEntries(
  Object.keys(docs).map((id) => [
    id,
    [
      {
        ...line,
        requestedQty: ["2", "3"].includes(id) ? 4 : null,
        approvedQty: id === "2" ? 4 : null,
        expectedQty: id === "4" ? 20 : null,
        countedQty: id === "4" ? 18 : null,
      },
    ],
  ]),
);
function detail(d) {
  const derivations = reduced
    ? []
    : d.kind === "MATERIAL_REQUEST"
      ? ["ISSUE"]
      : d.kind === "REPLENISHMENT"
        ? ["RECEIPT"]
        : d.kind === "STOCKTAKE" && d.status === "COUNTED"
          ? ["ADJUSTMENT"]
          : d.kind === "ISSUE" && d.postedAt
            ? ["RETURN"]
            : d.postedAt
              ? ["REVERSAL"]
              : [];
  return {
    document: { fields: d },
    canApprove: false,
    lines: (docLines[d.documentId] || []).map((fields) => ({ fields })),
    fulfillment:
      d.kind === "MATERIAL_REQUEST"
        ? [
            {
              fields: {
                lineId: 1,
                requestedQty: 4,
                approvedQty: 4,
                issuedBaseQty: 0,
                missingBaseQty: 4,
              },
            },
          ]
        : [],
    events: [
      { fields: { eventType: "CREATED", occurredAt: "2026-11-23T00:00:00Z" } },
    ],
    inventory: {
      creator: "Thủ kho QA",
      approver: d.approvedBy ? "Người duyệt QA" : null,
      sourceWarehouse: d.sourceWarehouseId ? "Kho chính" : null,
      destinationWarehouse: d.destinationWarehouseId ? "Kho chính" : null,
      sourceNumber: d.sourceDocumentId ? "Nguồn đã duyệt" : null,
      children: [],
      derivations,
      canEdit:
        !reduced &&
        !d.approvedBy &&
        !d.postedAt &&
        ["DRAFT", "REQUESTED", "IN_PROGRESS"].includes(d.status),
      canApprove:
        !reduced && d.kind === "RETURN" && d.createdBy !== 1 && !d.approvedBy,
      canPost:
        !reduced &&
        d.status === "DRAFT" &&
        ["RECEIPT", "ISSUE", "TRANSFER", "TRANSFER_RECEIPT"].includes(d.kind),
      lines: (docLines[d.documentId] || []).map((l) => ({
        lineId: l.lineId,
        name: "Khăn tắm",
        code: "TOWEL",
        unit: "Cái",
        balance: 20,
        remaining: Object.fromEntries(
          derivations.map((k) => [k, k === "ADJUSTMENT" ? 2 : 4]),
        ),
      })),
    },
  };
}
const stock = Array.from({ length: 26 }, (_, i) => ({
  warehouseId: 1,
  warehouse: "Kho chính",
  itemId: i + 1,
  code: `VT${i}`,
  item: i ? `Vật tư ${i}` : "Khăn tắm",
  baseQty: i ? 0 : 20,
  unit: "Cái",
  minimumQty: 10,
  lastMovement: "2026-11-23T00:00:00Z",
}));
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
try {
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
    timezoneId: "Asia/Ho_Chi_Minh",
  });
  await context.addInitScript(() =>
    sessionStorage.setItem("hotelops.compact.session", "ui-fixture"),
  );
  await context.route("**/api/v2/**", async (route) => {
    const req = route.request(),
      url = new URL(req.url()),
      path = url.pathname.replace("/api/v2/", ""),
      p = url.searchParams;
    let json;
    if (req.method() !== "GET") {
      const body = req.postDataJSON();
      writes.push({ path, body, method: req.method() });
      if (conflict) {
        conflict = false;
        await route.fulfill({
          status: 409,
          json: { title: "STOCK_INSUFFICIENT" },
        });
        return;
      }
      if (path.endsWith("/derive")) {
        const source = docs[path.split("/")[1]],
          id = 90 + writes.length;
        docs[id] = {
          documentId: id,
          documentNo: `LINKED-${id}`,
          kind: body.kind,
          status: "DRAFT",
          createdBy: 1,
          sourceDocumentId: source.documentId,
          sourceWarehouseId:
            body.kind === "ISSUE" || body.kind === "ADJUSTMENT" ? 1 : null,
          destinationWarehouseId:
            body.kind === "RECEIPT" || body.kind === "RETURN" ? 1 : null,
          rowVersion: `v${id}`,
        };
        docLines[id] = body.lines.map((l) => ({
          ...line,
          actualQty: l.quantity,
          sourceLineId: l.lineId,
        }));
        json = { fields: docs[id] };
      } else if (path === "documents") {
        const id = 100 + writes.length;
        docs[id] = {
          ...body,
          documentId: id,
          documentNo: `NEW-${id}`,
          status: "DRAFT",
          createdBy: 1,
          rowVersion: `v${id}`,
        };
        docLines[id] = [];
        json = { fields: docs[id] };
      } else if (path.includes("/lines")) {
        const d = docs[path.split("/")[1]];
        d.rowVersion += "x";
        docLines[d.documentId] = [{ ...line, ...body }];
        json = {
          document: { fields: d },
          line: { fields: docLines[d.documentId][0] },
        };
      } else if (path.includes("/actions/")) {
        const d = docs[path.split("/")[1]],
          command = path.split("/").at(-1);
        d.rowVersion += "x";
        if (command === "post") {
          d.status = "POSTED";
          d.postedAt = "2026-11-23T00:00:00Z";
        }
        if (command === "approve") d.approvedBy = 1;
        json = { fields: d };
      } else throw new Error(`Unexpected mutation ${path}`);
    } else {
      reads.push({ path, params: url.search });
      if (path === "auth/me")
        json = {
          user: { fields: { userId: 1, fullName: "Thủ kho QA" } },
          branches: [
            { branchId: 1, name: "Chi nhánh Bùi Viện" },
            { branchId: 2, name: "Chi nhánh Cao Thắng" },
          ],
          roles: [1, 2].map((branchId) => ({
            code: "STOREKEEPER",
            name: "Thủ kho",
            branchId,
          })),
          memberships: [],
          grants: (reduced
            ? ["INVENTORY_VIEW", "ATTENDANCE_RECORD"]
            : permissions
          ).flatMap((code) =>
            [1, 2].map((branchId) => ({
              code,
              branchId,
              dataScope: code === "ATTENDANCE_RECORD" ? "OWN" : "BRANCH",
            })),
          ),
          requiresPasswordChange: false,
        };
      else if (path === "workspace/options")
        json = {
          departments: [{ departmentId: 1, name: "Kho" }],
          warehouses: [
            { warehouseId: 1, name: "Kho chính" },
            { warehouseId: 2, name: "Kho phụ" },
          ],
          items: [
            { itemId: 1, name: "Khăn tắm", code: "TOWEL", baseUnitId: 1 },
          ],
          references: [
            { referenceId: 1, kind: "UOM", name: "Cái" },
            { referenceId: 2, kind: "ITEM_CATEGORY", name: "Vải" },
          ],
        };
      else if (path === "insights/inventory")
        json = {
          metrics: {
            trackedItems: 26,
            stockedItems: 1,
            emptyItems: 25,
            lowStock: 25,
            pendingRequests: 1,
            receipts: 3,
            issues: 2,
            transfers: 1,
            openStocktakes: 1,
            openReplenishments: 1,
          },
          timeline: [
            { day: "2026-11-23", kind: "RECEIPT", count: 3 },
            { day: "2026-11-23", kind: "ISSUE", count: 2 },
          ],
          lowStock: [stock[1]],
          pending: [docs[2]],
          recent: [{ ...docs[6], postedAt: "2026-11-23T00:00:00Z" }],
        };
      else if (path === "insights/stock") json = stock;
      else if (path === "insights/ledger")
        json = {
          items: [
            {
              movementId: 1,
              documentId: 1,
              documentNo: "RECEIPT-001",
              kind: "RECEIPT",
              signedBaseQty: 20,
              occurredAt: "2026-11-23T00:00:00Z",
              actor: "Thủ kho QA",
            },
          ],
          total: 1,
        };
      else if (path === "documents") {
        const kind = p.get("kind"),
          rows = Object.values(docs).filter(
            (d) =>
              d.kind === kind &&
              (!p.get("search") || d.documentNo.includes(p.get("search"))),
          );
        json = pageOf(
          rows,
          Number(p.get("page") || 1),
          p.get("search") ? rows.length : 26,
        );
      } else if (/^documents\/\d+$/.test(path))
        json = detail(docs[path.split("/")[1]]);
      else if (path === "catalog/items")
        json = pageOf(
          Array.from({ length: 51 }, (_, i) => ({
            itemId: i + 1,
            code: `VT${i}`,
            name: `Vật tư ${i}`,
            baseUnitId: 1,
            isActive: true,
          })),
          1,
          51,
        );
      else if (path === "catalog/warehouses")
        json = pageOf([
          { warehouseId: 1, name: "Kho chính", code: "MAIN", isActive: true },
        ]);
      else if (path === "catalog/stock-policies")
        json = pageOf([
          {
            inventoryPolicyId: 1,
            warehouseId: 1,
            itemId: 1,
            minimumQty: 10,
            targetQty: 30,
          },
        ]);
      else if (path === "notifications") json = { items: [], hasMore: false };
      else if (path === "workday")
        json = {
          weekStart: "2026-11-23",
          today: "2026-11-23",
          canManage: false,
          active: null,
          shifts: [],
          assignments: [],
        };
      else if (path.startsWith("catalog/")) json = pageOf([]);
      else throw new Error(`Unexpected GET ${path}`);
    }
    await route.fulfill({ status: 200, json });
  });
  const page = await context.newPage();
  page.on("pageerror", (e) => {
    errors.push(e.message);
    console.error("PAGE ERROR", e.message);
  });
  const open = async (hash) => {
    await page.goto(`http://127.0.0.1:5186/#${hash}`);
    await expect(page.locator(".topbar h1")).toBeVisible();
    await expect(
      page.locator("main [role=status]").filter({ hasText: "Đang tải" }),
    ).toHaveCount(0);
  };
  await open("dashboard");
  await expect(page.locator(".sidebar nav button")).toHaveCount(8);
  await expect(page.locator(".branch-metric-value")).toHaveCount(10);
  assert(
    !reads.some(
      (r) => r.path === "insights/dashboard" || r.path === "insights/personal",
    ),
  );
  await open("stock");
  await expect(page.locator("tbody tr")).toHaveCount(25);
  await page.getByRole("button", { name: "Tiếp", exact: true }).click();
  await expect(page.locator("tbody tr")).toHaveCount(1);
  await page.getByRole("button", { name: "Trước", exact: true }).click();
  await page
    .getByRole("button", { name: "Biến động", exact: true })
    .first()
    .click();
  await expect(page.locator("dialog.room-drawer")).toBeVisible();
  await expect(page.locator("dialog")).toContainText("Thủ kho QA");
  await page.keyboard.press("Escape");
  await page.getByRole("button", { name: "Bộ lọc", exact: true }).click();
  await page.getByLabel("Dưới định mức").check();
  await expect(page.locator("tbody tr")).toHaveCount(25);
  await open("supplies");
  await page.getByRole("textbox").fill("Vật tư 50");
  await expect(page.locator("tbody tr")).toHaveCount(1);
  await expect(page.locator("tbody")).toContainText("Vật tư 50");
  await page.getByRole("button", { name: "Chi tiết", exact: true }).click();
  await expect(page.locator("dialog.room-drawer")).toContainText("Cái");
  await expect(
    page
      .locator("dialog")
      .getByRole("link", { name: "Xem tồn và lịch sử biến động" }),
  ).toHaveAttribute("href", "#stock?search=VT50");
  await page.keyboard.press("Escape");
  await open("transactions?kind=RECEIPT");
  await page.getByRole("button", { name: "Tạo nhập kho", exact: true }).click();
  await expect(
    page
      .locator("dialog")
      .last()
      .getByRole("combobox", { name: "Kho nhận", exact: true }),
  ).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.getByLabel("Từ ngày")).toHaveCount(0);
  await page.getByRole("button", { name: "Bộ lọc", exact: true }).click();
  await page.getByLabel("Từ ngày").fill("2026-11-01");
  await page.getByLabel("Sắp xếp").selectOption("oldest");
  await expect
    .poll(() =>
      reads.some(
        (r) =>
          r.path === "documents" &&
          r.params.includes("sort=oldest") &&
          r.params.includes("from="),
      ),
    )
    .toBeTruthy();
  await page
    .getByRole("button", { name: "Chi tiết", exact: true })
    .first()
    .click();
  await expect(page.locator("dialog.room-drawer")).toBeVisible();
  await page
    .getByRole("button", { name: "Xác nhận ghi sổ", exact: true })
    .click();
  await expect(page.locator("dialog").last()).toContainText("20 → 24");
  conflict = true;
  await page
    .locator("dialog")
    .last()
    .getByRole("button", { name: "Xác nhận", exact: true })
    .click();
  await expect(page.getByRole("alert")).toContainText("không đủ số lượng");
  assert.equal(docs[1].status, "DRAFT");
  await page.keyboard.press("Escape");
  await page
    .getByRole("button", { name: "Xác nhận ghi sổ", exact: true })
    .click();
  await page
    .locator("dialog")
    .last()
    .getByRole("button", { name: "Xác nhận", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Xác nhận ghi sổ", exact: true }),
  ).toHaveCount(0);
  await open("fulfillment?kind=MATERIAL_REQUEST&id=2");
  await expect(
    page.getByRole("button", { name: "Duyệt chứng từ", exact: true }),
  ).toHaveCount(0);
  await page
    .getByRole("button", { name: "Cấp phát vật tư", exact: true })
    .click();
  await page.locator("dialog").last().getByRole("spinbutton").fill("2");
  await page
    .locator("dialog")
    .last()
    .getByLabel("Lý do", { exact: true })
    .fill("Cấp phát theo yêu cầu đã duyệt");
  await page
    .getByRole("button", { name: "Tạo chứng từ liên kết", exact: true })
    .click();
  await expect.poll(() => page.url()).toContain("kind=ISSUE");
  await expect(page.locator("dialog.room-drawer")).toContainText(
    "Nguồn đã duyệt",
  );
  assert.equal(
    writes.find((w) => w.path === "documents/2/derive").body.lines[0].quantity,
    2,
  );
  await open("replenishment?kind=REPLENISHMENT&id=3");
  await page
    .getByRole("button", { name: "Tiếp nhận hàng", exact: true })
    .click();
  await page
    .locator("dialog")
    .last()
    .getByLabel("Lý do", { exact: true })
    .fill("Nhận hàng từ nhà cung cấp");
  await page
    .getByRole("button", { name: "Tạo chứng từ liên kết", exact: true })
    .click();
  await expect.poll(() => page.url()).toContain("kind=RECEIPT");
  await open("stocktaking?kind=STOCKTAKE&id=4");
  await page.getByRole("button", { name: "Điều chỉnh", exact: true }).click();
  await expect(
    page.locator("dialog").last().getByRole("spinbutton"),
  ).toHaveAttribute("readonly", "");
  await page
    .locator("dialog")
    .last()
    .getByLabel("Lý do", { exact: true })
    .fill("Điều chỉnh theo kiểm kê đã đếm");
  await page
    .getByRole("button", { name: "Tạo chứng từ liên kết", exact: true })
    .click();
  await expect.poll(() => page.url()).toContain("kind=ADJUSTMENT");
  await expect(
    page.getByRole("button", { name: "Xác nhận ghi sổ", exact: true }),
  ).toHaveCount(0);
  await open("transactions?kind=RETURN&id=5");
  await page
    .getByRole("button", { name: "Duyệt chứng từ", exact: true })
    .click();
  await page
    .locator("dialog")
    .last()
    .getByRole("button", { name: "Xác nhận", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Duyệt chứng từ", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "Xác nhận ghi sổ", exact: true }),
  ).toHaveCount(0);
  await open("workday");
  await expect(
    page.getByRole("button", { name: "+ Phân ca", exact: true }),
  ).toHaveCount(0);
  await expect(page.getByLabel("Đối tượng", { exact: true })).toHaveCount(0);
  for (const width of [1440, 768, 375]) {
    await page.setViewportSize({ width, height: 1000 });
    for (const hash of [
      "dashboard",
      "supplies",
      "stock",
      "transactions",
      "fulfillment",
      "stocktaking",
      "replenishment",
      "history",
      "workday",
    ]) {
      await open(hash);
      await expect(page.locator("main h1")).toHaveCount(0);
      assert(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
        `${hash} overflow ${width}`,
      );
    }
    await open("dashboard");
    await page.screenshot({
      path: `${out}/dashboard-${width}.png`,
      fullPage: true,
    });
  }
  reduced = true;
  await open("transactions?kind=RECEIPT&id=1");
  await page.reload();
  await expect(page.locator(".topbar h1")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Tạo nhập kho", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "Xác nhận ghi sổ", exact: true }),
  ).toHaveCount(0);
  assert.equal(errors.length, 0, errors.join("\n"));
  fs.writeFileSync(
    `${out}/ui-verification.json`,
    JSON.stringify(
      { passed: true, responsiveViews: 27, writes, reads, errors },
      null,
      2,
    ),
  );
  console.log(
    "PASS Storekeeper: 8 groups, real API contracts, paging/filter/sort, source-linked fulfillment/receiving/adjustment, independent approval, conflict handling, readonly Workday and 27 responsive views",
  );
} finally {
  await browser.close();
}
