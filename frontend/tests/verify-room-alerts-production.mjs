import assert from "node:assert/strict";
import fs from "node:fs";
import { chromium, expect } from "@playwright/test";
const out = "../artifacts/production-261124-room-alerts";
const browser = await chromium.launch({ executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  const errors = [];
  page.on("pageerror", error => errors.push(error.message));
  const response = await page.goto("https://127.0.0.1:5080/");
  assert.equal(response.status(), 200);
  assert.ok((await response.text()).includes("/assets/index-ChZfi19R.js"));
  assert.equal(await page.evaluate(async () => (await fetch("/assets/index-ChZfi19R.js")).status), 200);
  await expect(page.locator(".login-card")).toBeVisible();
  const anonymousApiStatus = await page.evaluate(async () => (await fetch("/api/v2/auth/me")).status);
  assert.equal(anonymousApiStatus, 401);
  assert.deepEqual(errors, []);
  await page.screenshot({ path: `${out}/login.png` });
  fs.writeFileSync(`${out}/verification.json`, JSON.stringify({ passed: true, trustedTls: true, url:page.url(), frontendStatus:200, bundle:"index-ChZfi19R.js", anonymousApiStatus, errors, verifiedAt:new Date().toISOString() },null,2));
  console.log("PASS Production HTTPS5080, expected bundle, login, anonymous API401; no runtime errors");
} finally { await browser.close(); }
