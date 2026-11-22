import { chromium } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";
const root = path.resolve(".."),
  out = path.join(
    root,
    process.env.V10_VISUAL
      ? "artifacts/actor-lists/visual"
      : "artifacts/workday/visual",
  );
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
const source = fs.readFileSync(
  path.join(root, ".agents/skills/ak-frontend-design/scripts/render-check.mjs"),
  "utf8",
);
const checks = source.slice(
  source.indexOf("function pageChecks()"),
  source.indexOf("async function checkViewport("),
);
const browser = await chromium.launch({
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    headless: true,
  }),
  results = [];
try {
  for (const [user, pages] of [
    ["demo_head", ["workday", "rooms", "reports"]],
    ["demo_manager", ["rooms", "work", "assets", "dashboard", "reports"]],
    ["demo_store", ["documents"]],
    ["demo_admin", ["users"]],
    ...(process.env.V10_VISUAL
      ? [
          ["demo_admin", ["dashboard", "catalog"]],
          ["demo_hks", ["work", "templates"]],
          ["demo_store", ["stock", "catalog"]],
          ["demo_owner", ["dashboard", "reports"]],
        ]
      : []),
  ]) {
    const context = await browser.newContext({
        viewport: { width: 1440, height: 1000 },
        reducedMotion: "reduce",
      }),
      page = await context.newPage();
    await page.goto(process.env.WORKDAY_FE || "http://127.0.0.1:5178");
    await page.getByLabel("Tên đăng nhập", { exact: true }).fill(user);
    await page
      .getByLabel("Mật khẩu", { exact: true })
      .fill(credentials.password);
    const loginResponse = page.waitForResponse(
      (r) => r.url().endsWith("/auth/login") && r.request().method() === "POST",
    );
    await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
    if ((await loginResponse).status() === 429) {
      await page.waitForTimeout(61000);
      await page
        .getByRole("button", { name: "Đăng nhập", exact: true })
        .click();
    }
    await page.locator("main h1").waitFor();
    for (const view of pages) {
      await page.evaluate((v) => (location.hash = v), view);
      await page.locator("main h1").waitFor();
      await page.waitForTimeout(500);
      await page.locator("main .spinner").waitFor({ state: "hidden" });
      for (const width of [375, 768, 1440]) {
        await page.setViewportSize({ width, height: 900 });
        await page.waitForTimeout(150);
        const r = await page.evaluate(`(${checks})()`);
        results.push({ user, view, width, ...r });
        await page.screenshot({
          path: path.join(out, `${user}-${view}-${width}.png`),
        });
        if (process.env.V10_VISUAL) {
          const sticky = await page.evaluate(async () => {
            const results = [];
            for (const box of document.querySelectorAll(
              ".table-scroll,.calendar-scroll",
            )) {
              const row = box.querySelector("tbody tr"),
                header = box.querySelector("thead th");
              if (!row || !header) continue;
              const clones = Array.from({ length: 40 }, () =>
                row.cloneNode(true),
              );
              clones.forEach((r) => row.parentNode.appendChild(r));
              const spacer = document.createElement("div");
              spacer.style.height = "1600px";
              document.querySelector("main").appendChild(spacer);
              box.scrollTop = 180;
              box.scrollLeft = 80;
              scrollTo(0, box.getBoundingClientRect().top + scrollY + 40);
              await new Promise((r) => setTimeout(r, 80));
              const h = header.getBoundingClientRect(),
                c = row.children[0].getBoundingClientRect();
              results.push({ top: h.top, alignment: h.left - c.left });
              clones.forEach((r) => r.remove());
              spacer.remove();
              box.scrollTop = 0;
              box.scrollLeft = 0;
              scrollTo(0, 0);
            }
            return results;
          });
          for (const s of sticky)
            if (Math.abs(s.top) > 2 || Math.abs(s.alignment) > 1)
              throw Error(
                `Sticky header ${user} ${view} ${width}: ${JSON.stringify(s)}`,
              );
          results[results.length - 1].sticky = sticky;
        }
      }
    }
    await context.close();
  }
  const findings = results.flatMap((r) =>
    r.findings.map((f) => ({
      user: r.user,
      view: r.view,
      width: r.width,
      ...f,
    })),
  );
  console.log(
    JSON.stringify(
      {
        screens: results.length,
        errors: findings.filter((f) => f.severity === "error").length,
        warnings: findings.filter((f) => f.severity !== "error").length,
        findings: findings.slice(0, 30),
      },
      null,
      2,
    ),
  );
  fs.writeFileSync(
    path.join(out, "results.json"),
    JSON.stringify(results, null, 2),
  );
  if (findings.some((f) => f.severity === "error")) process.exitCode = 1;
} finally {
  await browser.close();
}
