import { chromium, expect } from "@playwright/test";
import fs from "node:fs";
const credentials = JSON.parse(
  fs
    .readFileSync(
      "../artifacts/compact-v2/seed/HotelOpsCompactDB/credentials.json",
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
    viewport: { width: 1440, height: 900 },
  }),
  page = await context.newPage(),
  results = [];
const origin = "http://127.0.0.1:5770/api/v2";
let token;
async function call(path, body, method = body ? "POST" : "GET") {
  const r = await fetch(origin + "/" + path, {
    method,
    headers: {
      "Content-Type": "application/json",
      Authorization: "Bearer " + token,
    },
    body: body ? JSON.stringify(body) : undefined,
  });
  expect(r.status, path).toBe(200);
  return r.json();
}
async function check(name, run) {
  try {
    await run();
    results.push({ name, pass: true });
    console.log("PASS " + name);
  } catch (e) {
    results.push({ name, pass: false, error: e.message });
    await page.screenshot({
      path: "../artifacts/actor-lists/admin-failure.png",
      fullPage: true,
    });
    throw e;
  }
}
try {
  token = (
    await call("auth/login", {
      username: "demo_admin",
      password: credentials.password,
    })
  ).accessToken;
  await context.addInitScript(
    (t) => sessionStorage.setItem("hotelops.compact.session", t),
    token,
  );
  await page.goto("http://127.0.0.1:5179/#catalog?resource=branches");
  await page.locator("main h1").waitFor();
  await check(
    "Admin can open every catalog tab without API or UI errors",
    async () => {
      const names = await page
        .locator("main .tabs")
        .getByRole("button")
        .allTextContents();
      for (const name of names) {
        await page
          .locator("main .tabs")
          .getByRole("button", { name: name.trim(), exact: true })
          .click();
        await page.waitForLoadState("networkidle");
        await expect(page.locator("main [role=alert]")).toHaveCount(0);
      }
    },
  );
  await check(
    "New role can receive a scoped permission through FE; built-in Admin remains protected",
    async () => {
      const stamp = Date.now().toString().slice(-8),
        role = (
          await call("catalog/roles", {
            fields: { code: "QA" + stamp, name: "QA role " + stamp },
          })
        ).fields;
      await page.reload();
      await page
        .locator("main .tabs")
        .getByRole("button", { name: "Vai trò", exact: true })
        .click();
      await page
        .locator("tbody tr")
        .filter({ hasText: "QA role " + stamp })
        .getByRole("button", { name: "Phân quyền", exact: true })
        .click();
      const dialog = page.locator("dialog[open]");
      await dialog.getByLabel("Cấp ROOM_VIEW", { exact: true }).check();
      await dialog
        .getByLabel("Phạm vi ROOM_VIEW", { exact: true })
        .selectOption("BRANCH");
      const geometry = await dialog.evaluate(async (d) => {
        const box = d.querySelector(".table-scroll");
        box.scrollTop = 350;
        d.scrollTop = 150;
        await new Promise((r) => setTimeout(r, 120));
        return {
          header: box.querySelector("th").getBoundingClientRect().top,
          anchor: Math.max(
            d.getBoundingClientRect().top + 1,
            box.getBoundingClientRect().top + 1,
          ),
        };
      });
      expect(Math.abs(geometry.header - geometry.anchor)).toBeLessThanOrEqual(
        2,
      );
      await dialog.getByRole("button", { name: "Lưu phân quyền" }).click();
      await expect(dialog).toHaveCount(0);
      const grants = await call(`users/roles/${role.roleId}/permissions`);
      expect(grants.permissions.map((p) => [p.code, p.dataScope])).toEqual([
        ["ROOM_VIEW", "BRANCH"],
      ]);
      await page
        .locator("tbody tr")
        .filter({ hasText: "SYS_ADMIN" })
        .getByRole("button", { name: "Phân quyền", exact: true })
        .click();
      await expect(
        page
          .locator("dialog[open]")
          .getByText("Quyền Admin được bảo vệ.", { exact: false }),
      ).toBeVisible();
      await expect(
        page
          .locator("dialog[open]")
          .getByRole("button", { name: "Lưu phân quyền" }),
      ).toHaveCount(0);
      results.push({ name: "Dialog header geometry", pass: true, geometry });
    },
  );
} finally {
  fs.writeFileSync(
    "../artifacts/actor-lists/admin-tests.json",
    JSON.stringify(results, null, 2),
  );
  await browser.close();
}
