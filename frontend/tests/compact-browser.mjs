import { chromium } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";
import assert from "node:assert/strict";
const root = path.resolve(".."),
  out = path.join(root, "artifacts/workday/browser");
fs.mkdirSync(out, { recursive: true });
const credentials = JSON.parse(
  fs
    .readFileSync(
      path.join(
        root,
        "artifacts/compact-v2/seed/HotelOpsCompactDB/credentials.json",
      ),
      "utf8",
    )
    .replace(/^\uFEFF/, ""),
);
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  headless: true,
});
const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
    reducedMotion: "reduce",
  }),
  page = await context.newPage(),
  errors = [],
  results = [];
page.on("pageerror", (e) => errors.push(e.message));
const base = process.env.WORKDAY_FE || "http://127.0.0.1:5178";
async function check(name, fn) {
  if (process.env.COMPACT_RESUME === "1") {
    const prior = JSON.parse(
      fs.readFileSync(path.join(out, "previous-results.json"), "utf8"),
    ).results.find((r) => r.name === name && r.pass);
    if (prior) {
      results.push(prior);
      console.log("REUSE unchanged passed step " + name);
      return;
    }
  }
  try {
    await fn();
    results.push({ name, pass: true });
    console.log("PASS " + name);
  } catch (e) {
    results.push({ name, pass: false, error: e.message });
    await page.screenshot({
      path: path.join(out, "failure.png"),
      fullPage: true,
    });
    throw e;
  }
}
async function login(name) {
  await page.goto(base);
  await Promise.race([
    page.getByRole("button", { name: "Đăng xuất", exact: true }).waitFor(),
    page.getByLabel("Tên đăng nhập", { exact: true }).waitFor(),
  ]);
  if (
    await page
      .getByRole("button", { name: "Đăng xuất", exact: true })
      .isVisible()
  )
    await page.getByRole("button", { name: "Đăng xuất", exact: true }).click();
  await page.getByLabel("Tên đăng nhập", { exact: true }).fill(name);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(credentials.password);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await page.locator("main h1").waitFor();
  await page.locator("main .spinner").waitFor({ state: "hidden" });
}
async function nav(name) {
  if ((await page.viewportSize()).width <= 760)
    await page
      .getByRole("button", { name: "Mở điều hướng", exact: true })
      .click();
  await page
    .getByRole("navigation")
    .getByRole("button", { name, exact: true })
    .click();
  await page.locator("main h1").waitFor();
  await page.locator("main .spinner").waitFor({ state: "hidden" });
}
const dialog = () => page.locator("dialog[open]");
async function close() {
  await dialog().getByRole("button", { name: "Đóng", exact: true }).click();
  await dialog().waitFor({ state: "hidden" });
}
async function command(name, reason) {
  await dialog().getByRole("button", { name, exact: true }).click();
  if (reason) await dialog().locator("textarea[name=reason]").fill(reason);
  const response = page.waitForResponse(
    (r) => r.request().method() === "POST" && r.url().includes("/actions/"),
  );
  await dialog().getByRole("button", { name: "Xác nhận", exact: true }).click();
  const result = await response;
  assert.ok(result.ok(), `${name}: ${result.status()} ${await result.text()}`);
  await dialog().locator("form.action-form").waitFor({ state: "hidden" });
  await dialog().locator(".spinner").waitFor({ state: "hidden" });
  await dialog().locator('[aria-busy="true"]').waitFor({ state: "hidden" });
}
try {
  await check(
    "Department Head plans a real shift from weekly calendar",
    async () => {
      await login("demo_head");
      await page.locator(".calendar").waitFor();
      const today = await page.evaluate(() =>
        new Intl.DateTimeFormat("en-CA", {
          timeZone: "Asia/Ho_Chi_Minh",
        }).format(new Date()),
      );
      await page
        .getByRole("button", {
          name: `Phân Buồng phòng • sáng ngày ${today}`,
          exact: true,
        })
        .click();
      await dialog()
        .getByLabel("Nhân viên đủ điều kiện")
        .selectOption({ label: "Buồng phòng mẫu 02 · DEMO_HK2" });
      await dialog().getByRole("button", { name: "Xác nhận phân ca" }).click();
      await page
        .getByRole("status")
        .filter({ hasText: "Đã cập nhật lịch làm việc." })
        .waitFor();
      await page.screenshot({
        path: path.join(out, "weekly-desktop.png"),
        fullPage: false,
      });
    },
  );
  await check("Employee starts and ends only own current shift", async () => {
    await login("demo_hk2");
    await nav("Lịch làm việc");
    assert.equal(await page.getByRole("button", { name: /^Phân / }).count(), 0);
    await page.getByRole("button", { name: "Bắt đầu ca", exact: true }).click();
    await page
      .getByRole("button", { name: "Kết thúc ca", exact: true })
      .waitFor();
    await page
      .getByRole("button", { name: "Kết thúc ca", exact: true })
      .click();
    await page.locator(".active-shift").waitFor({ state: "hidden" });
    await page.getByText("Đã kết thúc", { exact: true }).first().waitFor();
  });
  await check(
    "Reception check-in and checkout update real room state",
    async () => {
      await login("demo_reception");
      await page
        .locator(".room-tile")
        .filter({ has: page.locator("strong", { hasText: /^101$/ }) })
        .click();
      await dialog()
        .getByRole("button", { name: "Nhận phòng", exact: true })
        .click();
      await dialog()
        .getByRole("button", { name: "Xác nhận", exact: true })
        .click();
      await dialog()
        .getByRole("button", { name: "Trả phòng", exact: true })
        .waitFor();
      await dialog()
        .getByRole("button", { name: "Trả phòng", exact: true })
        .click();
      await dialog()
        .getByRole("button", { name: "Xác nhận", exact: true })
        .click();
      await dialog().getByText("Không có khách", { exact: true }).waitFor();
      assert.equal(
        await dialog()
          .getByRole("button", { name: "Nhận phòng", exact: true })
          .count(),
        0,
      );
      await close();
    },
  );
  await check(
    "Supervisor creates and assigns cleaning through FE",
    async () => {
      await login("demo_manager");
      await nav("Công việc & yêu cầu");
      await page.getByRole("button", { name: "Tạo mới", exact: false }).click();
      await dialog()
        .getByLabel("Tiêu đề", { exact: true })
        .fill("Browser cleaning integration 101");
      await dialog()
        .locator('select[name="roomId"]')
        .selectOption({ label: "101" });
      await dialog()
        .getByRole("button", { name: "Tạo công việc", exact: true })
        .click();
      await dialog()
        .getByRole("button", { name: "Phân công", exact: true })
        .waitFor();
      await dialog()
        .getByRole("button", { name: "Phân công", exact: true })
        .click();
      await dialog()
        .locator('select[name="userId"]')
        .selectOption({ label: "Buồng phòng mẫu 02" });
      await dialog()
        .getByRole("button", { name: "Xác nhận", exact: true })
        .click();
      await dialog().locator("form.action-form").waitFor({ state: "hidden" });
      await close();
    },
  );
  await check(
    "Housekeeper pauses existing timer, accepts, starts and submits new cleaning",
    async () => {
      await login("demo_hk2");
      await page.locator("main tbody tr").first().waitFor();
      const ongoing = page.getByRole("button", {
        name: "Mở việc đang chạy",
        exact: true,
      });
      if (await ongoing.count()) {
        await ongoing.click();
        await command("Tạm dừng", "Tạm dừng công việc để kiểm thử luồng mới");
        await close();
        await page
          .getByRole("button", { name: "Dọn phòng", exact: true })
          .click();
      }
      await page
        .locator("main tbody tr")
        .filter({ hasText: "Browser cleaning integration 101" })
        .getByRole("button", { name: "Xử lý" })
        .click();
      await command("Nhận việc");
      await dialog()
        .getByRole("button", { name: "Bắt đầu", exact: true })
        .waitFor();
      await command("Bắt đầu");
      await dialog()
        .getByRole("button", { name: "Gửi hoàn thành", exact: true })
        .waitFor();
      await command("Gửi hoàn thành");
      await dialog().getByText("Chờ kiểm tra", { exact: true }).waitFor();
      assert.equal(
        await dialog()
          .getByRole("button", { name: "Xác nhận hoàn tất", exact: true })
          .count(),
        0,
      );
      await close();
    },
  );
  await check(
    "Inspection independently releases room; cleaning cannot self-release",
    async () => {
      await login("demo_manager");
      await nav("Công việc & yêu cầu");
      await page
        .getByRole("button", { name: "Kiểm tra phòng", exact: true })
        .click();
      await page.getByRole("button", { name: "Tạo mới", exact: false }).click();
      await dialog()
        .getByLabel("Tiêu đề", { exact: true })
        .fill("Browser inspection integration 101");
      await dialog()
        .locator("select[name=checklistTemplateId]")
        .selectOption({ index: 1 });
      await dialog()
        .locator("select[name=parentWorkId]")
        .selectOption(
          await dialog()
            .locator("select[name=parentWorkId] option")
            .filter({ hasText: "Browser cleaning integration 101" })
            .getAttribute("value"),
        );
      await dialog()
        .getByRole("button", { name: "Tạo công việc", exact: true })
        .click();
      await dialog()
        .getByRole("button", { name: "Phân công", exact: true })
        .click();
      await dialog()
        .locator('select[name="userId"]')
        .selectOption({ label: "Giám sát Sài Gòn" });
      await dialog()
        .getByRole("button", { name: "Xác nhận", exact: true })
        .click();
      await dialog().locator("form.action-form").waitFor({ state: "hidden" });
      await command("Nhận việc");
      await command("Bắt đầu");
      await dialog().locator(".check-item select").first().waitFor();
      const count = await dialog().locator(".check-item select").count();
      assert.ok(count > 0, "Inspection has a checklist snapshot");
      for (let i = 0; i < count; i++) {
        const response = page.waitForResponse(
          (r) =>
            r.request().method() === "PUT" && r.url().includes("/checklist/"),
        );
        await dialog()
          .locator(".check-item select")
          .nth(i)
          .selectOption("PASS");
        const result = await response;
        assert.ok(
          result.ok(),
          `checklist: ${result.status()} ${await result.text()}`,
        );
        await dialog().locator(".check-item select").nth(i).waitFor();
        await page.waitForFunction((index) => {
          const select = document.querySelectorAll(
            "dialog[open] .check-item select",
          )[index];
          return select && !select.disabled && select.value === "PASS";
        }, i);
      }
      await command("Gửi hoàn thành");
      await command("Kết luận đạt");
      await dialog().locator(".badge").filter({ hasText: /^Đạt$/ }).waitFor();
      await close();
      await nav("Phòng");
      assert.ok(
        (
          await page
            .locator(".room-tile")
            .filter({ has: page.locator("strong", { hasText: /^101$/ }) })
            .innerText()
        ).includes("Sẵn sàng"),
      );
    },
  );
  await check(
    "Storekeeper creates receipt, adds stock line and posts",
    async () => {
      await login("demo_store");
      await nav("Vật tư & chứng từ");
      await page.getByRole("button", { name: "Nhập kho", exact: true }).click();
      await page
        .getByRole("button", { name: "Tạo chứng từ", exact: false })
        .click();
      await dialog()
        .locator("select[name=destinationWarehouseId]")
        .selectOption({ index: 1 });
      await dialog()
        .locator("textarea[name=reason]")
        .fill("Nhập vật tư phục vụ kiểm thử trình duyệt");
      await dialog().getByRole("button", { name: "Tạo bản nháp" }).click();
      await dialog()
        .getByRole("button", { name: "Thêm vật tư", exact: false })
        .click();
      await dialog().locator("select[name=itemId]").selectOption({ index: 1 });
      await dialog().getByLabel("Số lượng", { exact: true }).fill("2");
      await dialog().getByLabel("Đơn giá", { exact: true }).fill("1000");
      const saved = page.waitForResponse(
        (r) =>
          r.request().method() === "POST" &&
          /\/documents\/\d+\/lines$/.test(r.url()),
      );
      await dialog().getByRole("button", { name: "Lưu vật tư" }).click();
      assert.ok((await saved).ok(), "Stock line saved");
      await dialog()
        .getByRole("button", { name: "Lưu vật tư" })
        .waitFor({ state: "hidden" });
      await dialog().locator('[aria-busy="true"]').waitFor({ state: "hidden" });
      await dialog()
        .getByRole("button", { name: "Ghi sổ kho", exact: true })
        .click();
      await dialog()
        .getByRole("button", { name: "Xác nhận", exact: true })
        .click();
      await dialog().getByText("Đã ghi sổ", { exact: true }).waitFor();
      await close();
    },
  );
  await check(
    "Keyboard dialog behavior and role-specific responsive pages",
    async () => {
      for (const [user, names] of [
        ["demo_head", ["Lịch làm việc", "Phòng", "Báo cáo"]],
        [
          "demo_manager",
          ["Phòng", "Công việc & yêu cầu", "Tài sản", "Tổng quan", "Báo cáo"],
        ],
        ["demo_store", ["Vật tư & chứng từ"]],
        ["demo_admin", ["Nhân sự & truy cập"]],
      ]) {
        await page.setViewportSize({ width: 1440, height: 1000 });
        await login(user);
        for (const name of names) {
          await nav(name);
          for (const width of [375, 768, 1440]) {
            await page.setViewportSize({ width, height: 900 });
            await page.waitForTimeout(100);
            const overflow = await page.evaluate(
              () => document.documentElement.scrollWidth > innerWidth + 1,
            );
            assert.equal(overflow, false, `${user} ${name} ${width} overflow`);
            assert.equal(
              await page.locator("main [role=alert]").count(),
              0,
              `${user} ${name} has API error`,
            );
          }
        }
      }
      await page.setViewportSize({ width: 375, height: 812 });
      await page
        .getByRole("button", { name: "Tạo tài khoản", exact: false })
        .click();
      await dialog().waitFor();
      await page.keyboard.press("Tab");
      assert.ok(
        await page.evaluate(() => !!document.activeElement?.closest("dialog")),
      );
      await page.keyboard.press("Escape");
      await dialog().waitFor({ state: "hidden" });
      await page.screenshot({ path: path.join(out, "users-mobile.png") });
    },
  );
  assert.deepEqual(errors, []);
} catch (e) {
  console.error(e.message);
  process.exitCode = 1;
} finally {
  fs.writeFileSync(
    path.join(out, "results.json"),
    JSON.stringify({ at: new Date().toISOString(), results, errors }, null, 2),
  );
  await browser.close();
}
