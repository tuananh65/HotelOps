import { chromium, expect } from "@playwright/test";
import fs from "node:fs";
const out = "../artifacts/workday/states";
fs.mkdirSync(out, { recursive: true });
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
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
try {
  await page.goto("http://127.0.0.1:5178");
  await page.getByLabel("Tên đăng nhập", { exact: true }).fill("demo_manager");
  await page.getByLabel("Mật khẩu", { exact: true }).fill(credentials.password);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await page
    .getByRole("navigation")
    .getByRole("button", { name: "Phòng", exact: true })
    .click();
  const tile = page.locator(".room-tile").first();
  await tile.waitFor();
  await tile.hover();
  await page.screenshot({ path: out + "/hover.png" });
  await page.getByPlaceholder("Số phòng…").fill("NO-MATCH-ROOM");
  await expect(page.locator(".room-tile")).toHaveCount(0);
  await expect(
    page.getByText("Không tìm thấy phòng phù hợp.", { exact: true }),
  ).toBeVisible();
  await page.screenshot({ path: out + "/empty.png" });
  await page.getByPlaceholder("Số phòng…").fill("");
  await page.route("**/api/v2/rooms?*", (route) => route.abort());
  await page.getByRole("button", { name: "Làm mới", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText(
    "Không kết nối được máy chủ",
  );
  await page.screenshot({ path: out + "/error.png" });
  await page.unroute("**/api/v2/rooms?*");
  await page.getByRole("button", { name: "Thử lại", exact: true }).click();
  await tile.waitFor();
  await tile.click();
  await page.locator("dialog[open]").waitFor();
  await page.screenshot({ path: out + "/overlay.png" });
  await page.keyboard.press("Escape");
  await expect(page.locator("dialog[open]")).toHaveCount(0);
  fs.writeFileSync(
    out + "/results.json",
    JSON.stringify(
      {
        at: new Date().toISOString(),
        passed: 4,
        checks: [
          "hover",
          "empty search",
          "network error and retry",
          "dialog Escape",
        ],
      },
      null,
      2,
    ),
  );
  console.log("PASS: hover, empty, network error/retry, dialog Escape");
} finally {
  await browser.close();
}
