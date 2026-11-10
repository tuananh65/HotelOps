import { test, expect, type Page } from "@playwright/test";
const tag = '"' + "A".repeat(64) + '"';
const criterion = {
  id: 10,
  text: "Tiêu chí cũ",
  group: "Phòng",
  mandatory: true,
  passCondition: "Sạch",
  description: null,
  order: 1,
};
function checklist(status = "DRAFT", used = false) {
  return {
    id: 42,
    branchId: 7,
    code: "CL1",
    name: "Checklist phòng",
    description: null,
    active: true,
    roomTypeIds: [11],
    versions: [
      {
        id: 1,
        number: 1,
        status,
        effectiveAt: null,
        publishedBy: null,
        usedByInspection: used,
        criteria: [criterion],
      },
    ],
  };
}
async function backend(
  page: Page,
  body = checklist(),
  failure = 0,
  catalog = false,
  extra: ReturnType<typeof checklist>[] = [],
) {
  let current = structuredClone(body);
  let exists = catalog;
  const reads: string[] = [];
  const writes: { method: string; path: string; input: any; etag?: string }[] =
    [];
  await page.route("**/api/**", async (route) => {
    const req = route.request();
    const path = new URL(req.url()).pathname;
    const fulfill = (value: unknown, status = 200) =>
      route.fulfill({
        status,
        contentType: "application/json",
        headers: { ETag: tag },
        body: JSON.stringify(value),
      });
    if (path === "/api/auth/csrf")
      return fulfill({ csrfToken: "test-only", headerName: "X-CSRF-TOKEN" });
    if (path === "/api/room-types")
      return fulfill({
        items: [
          {
            details: {
              id: 11,
              branchId: 7,
              code: "DLX",
              name: "Deluxe",
              isActive: true,
            },
          },
        ],
        total: 1,
      });
    if (req.method() === "GET" && path.includes("inspection-checklists"))
      reads.push(req.url());
    if (req.method() === "GET" && path.endsWith("inspection-checklists")) {
      if (failure) return fulfill({}, failure);
      const q = new URL(req.url()).searchParams;
      const pageNo = Number(q.get("page") || 1);
      const pageSize = Number(q.get("pageSize") || 20);
      let rows = exists ? [current, ...extra] : [];
      if (q.get("search"))
        rows = rows.filter(
          (r) =>
            r.code.includes(q.get("search")!) ||
            r.name.includes(q.get("search")!),
        );
      if (q.has("isActive"))
        rows = rows.filter((r) => r.active === (q.get("isActive") === "true"));
      if (q.get("status"))
        rows = rows.filter((r) =>
          r.versions.some((v) => v.status === q.get("status")),
        );
      if (q.get("roomTypeId"))
        rows = rows.filter((r) =>
          r.roomTypeIds.includes(Number(q.get("roomTypeId"))),
        );
      const summary = rows
        .slice((pageNo - 1) * pageSize, pageNo * pageSize)
        .map((r) => ({
          id: r.id,
          branchId: r.branchId,
          code: r.code,
          name: r.name,
          active: r.active,
          roomTypeIds: r.roomTypeIds,
          versions: r.versions.map(({ criteria, ...v }) => v),
        }));
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          items: summary,
          total: rows.length,
          page: pageNo,
          pageSize,
          branchId: 7,
          search: q.get("search"),
          isActive: q.has("isActive") ? q.get("isActive") === "true" : null,
          status: q.get("status"),
          roomTypeId: q.has("roomTypeId") ? Number(q.get("roomTypeId")) : null,
        }),
      });
    }
    if (req.method() === "GET") return fulfill(current, failure || 200);
    const input = req.postDataJSON();
    writes.push({
      method: req.method(),
      path,
      input,
      etag: req.headers()["if-match"],
    });
    if (failure) return fulfill({}, failure);
    exists = true;
    if (req.method() === "POST" && path.endsWith("inspection-checklists")) {
      current = { ...checklist(), code: input.code, name: input.name };
      current.versions[0].criteria = input.criteria.map(
        (c: any, i: number) => ({ ...c, id: i + 10, order: i + 1 }),
      );
    }
    if (req.method() === "PUT") {
      const draft = current.versions.find((v) => v.status === "DRAFT");
      const next = {
        id: 2,
        number: 2,
        status: "DRAFT",
        effectiveAt: null,
        publishedBy: null,
        usedByInspection: false,
        criteria: input.criteria.map((c: any, i: number) => ({
          ...c,
          id: i + 20,
          order: i + 1,
        })),
      };
      if (draft) draft.criteria = next.criteria;
      else current.versions.push(next);
    }
    if (path.endsWith("/publish"))
      current.versions.forEach((v) => {
        if (v.status === "PUBLISHED") v.status = "RETIRED";
        else if (v.status === "DRAFT") v.status = "PUBLISHED";
      });
    if (path.endsWith("/retire"))
      current.versions.forEach((v) => {
        if (v.status === "PUBLISHED") v.status = "RETIRED";
      });
    return fulfill(current);
  });
  return Object.assign(writes, { reads });
}
test("create Draft uses API, populates loaded list and detail without mock records", async ({
  page,
}) => {
  const writes = await backend(page);
  await page.goto("/checklists?branchId=7");
  await expect(
    page.getByText("Không có checklist phù hợp trên trang này."),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Tạo checklist", exact: true })
    .click();
  await page.getByLabel("Mã checklist", { exact: true }).last().fill("CL1");
  await page
    .getByLabel("Tên checklist", { exact: true })
    .fill("Checklist phòng");
  await page
    .getByLabel("Mã loại phòng áp dụng (phân cách bằng dấu phẩy)")
    .fill("11");
  await page.getByLabel("Nội dung", { exact: true }).fill("Kiểm tra giường");
  await page.getByRole("button", { name: "Lưu Draft", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "CL1 — Checklist phòng" }),
  ).toBeVisible();
  await expect(
    page
      .getByRole("row")
      .filter({
        has: page.getByRole("button", { name: "Mở CL1", exact: true }),
      }),
  ).toBeVisible();
  expect(
    writes.reads.filter((url) =>
      new URL(url).pathname.endsWith("inspection-checklists"),
    ).length,
  ).toBeGreaterThanOrEqual(2);
  expect(
    writes.reads.some((url) => new URL(url).pathname.endsWith("/42")),
  ).toBeTruthy();
  expect(writes).toHaveLength(1);
  expect(writes[0].input.roomTypeIds).toEqual([11]);
  expect(writes[0].input.criteria[0].text).toBe("Kiểm tra giường");
});
test("edit creates next Draft while used Published criteria stay read-only", async ({
  page,
}) => {
  const writes = await backend(page, checklist("PUBLISHED", true));
  await page.goto("/checklists/42?branchId=7");
  await page.getByRole("button", { name: "Tạo Draft kế tiếp" }).click();
  await page.getByLabel("Nội dung", { exact: true }).fill("Tiêu chí mới");
  await page.getByLabel("Lý do cập nhật").fill("Cập nhật tiêu chuẩn");
  await page.getByRole("button", { name: "Lưu Draft", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Phiên bản 2 — DRAFT" }),
  ).toBeVisible();
  await expect(page.getByText("Tiêu chí cũ", { exact: true })).toBeVisible();
  expect(writes[0].etag).toBe(tag);
  expect(Object.keys(writes[0].input).sort()).toEqual(["criteria", "reason"]);
  expect(writes[0].input.criteria[0].id).toBeUndefined();
});
test("publish and retire require confirmation and preserve exact version transport", async ({
  page,
}) => {
  const writes = await backend(page);
  await page.goto("/checklists/42?branchId=7");
  await page
    .getByRole("button", { name: "Công bố Draft", exact: true })
    .click();
  const form = page.getByRole("form", { name: "Xác nhận phiên bản" });
  await expect(
    form.getByRole("button", { name: "Xác nhận", exact: true }),
  ).toBeDisabled();
  await form.getByLabel("Lý do", { exact: true }).fill("Duyệt tiêu chuẩn");
  await form.getByLabel("Tôi xác nhận thao tác").check();
  await form.getByRole("button", { name: "Xác nhận", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Phiên bản 1 — PUBLISHED" }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Ngừng sử dụng bản công bố", exact: true })
    .click();
  await form.getByLabel("Lý do", { exact: true }).fill("Thay tiêu chuẩn");
  await form.getByLabel("Tôi xác nhận thao tác").check();
  await form.getByRole("button", { name: "Xác nhận", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Phiên bản 1 — RETIRED" }),
  ).toBeVisible();
  await expect(
    page
      .getByRole("row")
      .filter({
        has: page.getByRole("button", { name: "Mở CL1", exact: true }),
      }),
  ).toContainText("RETIRED");
  expect(
    writes.reads.filter((url) =>
      new URL(url).pathname.endsWith("inspection-checklists"),
    ).length,
  ).toBeGreaterThanOrEqual(3);
  expect(writes.map((w) => w.path.split("/").pop())).toEqual([
    "publish",
    "retire",
  ]);
  expect(
    writes.every((w) => w.etag === tag && w.input.confirmation === true),
  ).toBeTruthy();
});
for (const status of [401, 403, 503])
  test(`API ${status} shows explicit error and no fabricated detail`, async ({
    page,
  }) => {
    await backend(page, checklist(), status);
    await page.goto("/checklists/42?branchId=7");
    await expect(page.getByRole("alert").first()).toBeVisible();
    await expect(
      page.getByRole("heading", { name: "CL1 — Checklist phòng" }),
    ).toHaveCount(0);
  });
test("used Draft cannot edit or publish", async ({ page }) => {
  await backend(page, checklist("DRAFT", true));
  await page.goto("/checklists/42?branchId=7");
  await expect(
    page.getByRole("button", { name: "Chỉnh sửa Draft" }),
  ).toBeDisabled();
  await expect(
    page.getByRole("button", { name: "Công bố Draft", exact: true }),
  ).toBeDisabled();
});
test("invalid client input does not POST", async ({ page }) => {
  const writes = await backend(page);
  await page.goto("/checklists?branchId=7");
  await page
    .getByRole("button", { name: "Tạo checklist", exact: true })
    .click();
  await page
    .getByLabel("Mã checklist", { exact: true })
    .last()
    .fill("bad code");
  await page.getByLabel("Tên checklist", { exact: true }).fill("Name");
  await page
    .getByLabel("Mã loại phòng áp dụng (phân cách bằng dấu phẩy)")
    .fill("11");
  await page.getByLabel("Nội dung", { exact: true }).fill("Text");
  await page.getByRole("button", { name: "Lưu Draft", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("Mã checklist");
  expect(writes).toHaveLength(0);
});
test("stale update blocks further writes until authoritative reload", async ({
  page,
}) => {
  const writes = await backend(page);
  await page.route(
    "**/api/branches/7/inspection-checklists/42",
    async (route) => {
      if (route.request().method() === "PUT")
        return route.fulfill({
          status: 412,
          contentType: "application/json",
          body: "{}",
        });
      return route.fallback();
    },
  );
  await page.goto("/checklists/42?branchId=7");
  await page.getByRole("button", { name: "Ch\u1ec9nh s\u1eeda Draft" }).click();
  await page.getByLabel("L\u00fd do c\u1eadp nh\u1eadt").fill("Review");
  const save = page.getByRole("button", {
    name: "L\u01b0u Draft",
    exact: true,
  });
  await save.click();
  await expect(page.getByRole("alert").first()).toBeVisible();
  await expect(save).toBeDisabled();
  await page
    .getByRole("button", { name: "T\u1ea3i l\u1ea1i checklist", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Ch\u1ec9nh s\u1eeda Draft" }),
  ).toBeEnabled();
  expect(writes).toHaveLength(0);
});

test("collection loads summaries and opens fresh detail instead of treating summary as authority", async ({
  page,
}) => {
  const data = checklist("PUBLISHED", true);
  data.versions.push({
    ...data.versions[0],
    id: 2,
    number: 2,
    status: "DRAFT",
    usedByInspection: false,
  });
  const calls = await backend(page, data, 0, true);
  await page.goto("/checklists?branchId=7");
  const row = page
    .getByRole("row")
    .filter({ has: page.getByRole("button", { name: "Mở CL1", exact: true }) });
  await expect(row).toContainText("PUBLISHED");
  await expect(row).toContainText("DRAFT");
  await expect(
    page.getByRole("heading", { name: "Chi tiết checklist #42", exact: true }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "Mở CL1", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Chi tiết checklist #42", exact: true }),
  ).toBeVisible();
  const before = calls.reads.filter((url) =>
    new URL(url).pathname.endsWith("/42"),
  ).length;
  await page.getByRole("button", { name: "Mở CL1", exact: true }).click();
  await expect
    .poll(
      () =>
        calls.reads.filter((url) => new URL(url).pathname.endsWith("/42"))
          .length,
    )
    .toBeGreaterThan(before);
});
test("server pagination and filters reset page and preserve any historical status semantics", async ({
  page,
}) => {
  const root = checklist("RETIRED", true);
  root.versions.push({
    ...root.versions[0],
    id: 2,
    number: 2,
    status: "PUBLISHED",
    usedByInspection: false,
  });
  const extra = Array.from({ length: 20 }, (_, i) => ({
    ...checklist(),
    id: 100 + i,
    code: `X${i}`,
    name: `Other ${i}`,
  }));
  const calls = await backend(page, root, 0, true, extra);
  await page.goto("/checklists?branchId=7");
  await expect(
    page.getByText("Tổng cộng 21 checklist · Trang 1/2"),
  ).toBeVisible();
  await page.getByRole("button", { name: "Trang sau", exact: true }).click();
  await expect(
    page.getByText("Tổng cộng 21 checklist · Trang 2/2"),
  ).toBeVisible();
  await page.getByLabel("Tìm mã hoặc tên").fill("CL1");
  await page.getByRole("combobox", { name: /Hoạt động/ }).selectOption("true");
  await page.getByLabel("Trạng thái phiên bản").selectOption("RETIRED");
  await page.getByLabel("ID loại phòng", { exact: true }).fill("11");
  await page.getByLabel("Số dòng mỗi trang").selectOption("50");
  await page
    .getByRole("button", { name: "Áp dụng bộ lọc", exact: true })
    .click();
  await expect(
    page.getByText("Tổng cộng 1 checklist · Trang 1/1"),
  ).toBeVisible();
  const row = page
    .getByRole("row")
    .filter({ has: page.getByRole("button", { name: "Mở CL1", exact: true }) });
  await expect(row).toContainText("RETIRED");
  await expect(row).toContainText("PUBLISHED");
  const query = new URL(calls.reads.at(-1)!).searchParams;
  expect(Object.fromEntries(query)).toEqual({
    page: "1",
    pageSize: "50",
    search: "CL1",
    isActive: "true",
    status: "RETIRED",
    roomTypeId: "11",
  });
  await page.getByRole("button", { name: "Xóa bộ lọc", exact: true }).click();
  await expect(
    page.getByText("Tổng cộng 21 checklist · Trang 1/2"),
  ).toBeVisible();
});
for (const status of [401, 403, 503])
  test(`collection ${status} clears records and refresh recovers`, async ({
    page,
  }) => {
    await backend(page, checklist(), 0, true);
    let failing = false;
    await page.route("**/api/branches/7/inspection-checklists?*", (route) =>
      failing
        ? route.fulfill({
            status,
            contentType: "application/problem+json",
            body: "{}",
          })
        : route.fallback(),
    );
    await page.goto("/checklists?branchId=7");
    await expect(
      page.getByRole("button", { name: "Mở CL1", exact: true }),
    ).toBeVisible();
    failing = true;
    await page
      .getByRole("button", { name: "Tải lại danh sách", exact: true })
      .click();
    await expect(page.getByRole("alert").first()).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Mở CL1", exact: true }),
    ).toHaveCount(0);
    if (status !== 503)
      await expect(
        page.getByRole("button", { name: "Tạo checklist", exact: true }),
      ).toBeDisabled();
    failing = false;
    await page
      .getByRole("button", { name: "Tải lại danh sách", exact: true })
      .click();
    await expect(
      page.getByRole("button", { name: "Mở CL1", exact: true }),
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Tạo checklist", exact: true }),
    ).toBeEnabled();
  });
test("loading and empty results are explicit, filters do not retain stale records", async ({
  page,
}) => {
  await backend(page, checklist(), 0, true);
  let release!: () => void;
  const gate = new Promise<void>((resolve) => (release = resolve));
  await page.route(
    "**/api/branches/7/inspection-checklists?*",
    async (route) => {
      await gate;
      return route.fallback();
    },
  );
  await page.goto("/checklists?branchId=7");
  await expect(page.getByText("Đang tải danh sách checklist…")).toBeVisible();
  release();
  await expect(
    page.getByRole("button", { name: "Mở CL1", exact: true }),
  ).toBeVisible();
  await page.getByLabel("Tìm mã hoặc tên").fill("NOT_FOUND");
  await page
    .getByRole("button", { name: "Áp dụng bộ lọc", exact: true })
    .click();
  await expect(
    page.getByText("Không có checklist phù hợp trên trang này."),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Mở CL1", exact: true }),
  ).toHaveCount(0);
});
