import { test, expect } from "@playwright/test";
import { readFileSync } from "node:fs";
const catalog = JSON.parse(
  readFileSync(new URL("../src/catalog.json", import.meta.url), "utf8"),
);
test("all 80 screens open directly without a session or permission fixture", async ({
  page,
}) => {
  test.setTimeout(180000);
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto("/dev/screens");
  const links = await page.locator("a[data-screen-id]").evaluateAll((nodes) =>
    nodes.map((node) => ({
      id: Number(node.getAttribute("data-screen-id")),
      href: node.getAttribute("href")!,
    })),
  );
  expect(links).toHaveLength(80);
  for (const link of links) {
    await page.goto(link.href);
    await expect(
      page.locator(link.id <= 3 ? ".auth-panel h1" : ".page-header h1"),
    ).toHaveText(catalog.screens[link.id - 1].name);
    await expect(
      page.getByRole("heading", { name: "Record unavailable", exact: true }),
    ).toHaveCount(0);
    await expect(
      page.getByText("Access restricted", { exact: true }),
    ).toHaveCount(0);
    await expect(
      page.getByRole("heading", {
        name: "This screen could not load",
        exact: true,
      }),
    ).toHaveCount(0);
    expect(
      await page.evaluate(() => sessionStorage.getItem("hotelops-session")),
    ).toBeNull();
  }
  expect(errors).toEqual([]);
});
test("EN/VI persists and directory searches Vietnamese names", async ({
  page,
}) => {
  await page.goto("/dev/screens");
  await page.getByRole("button", { name: "VI", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: /Thư viện màn hình/ }),
  ).toBeVisible();
  await page
    .getByRole("textbox", { name: "Tìm kiếm màn hình" })
    .fill("kiểm kê");
  await expect(page.locator("a[data-screen-id]")).toHaveCount(3);
  await page.goto("/rooms");
  await expect(page.locator(".page-header h1")).toHaveText("Danh sách phòng");
  await page.reload();
  await expect(page.locator(".page-header h1")).toHaveText("Danh sách phòng");
  await page.getByRole("button", { name: "EN", exact: true }).click();
  await expect(page.locator(".page-header h1")).toHaveText("Room List");
});
test("populated reporting and design system render on desktop and mobile", async ({
  page,
}) => {
  await page.goto("/reports");
  await expect(page.locator("tbody tr").first()).toBeVisible();
  await page.goto("/dev/design-system");
  await expect(page.locator(".swatch")).toHaveCount(63);
  await page.screenshot({
    path: "docs/verification/public-design-system.png",
    fullPage: true,
  });
  await page.goto("/dashboard");
  await page.screenshot({
    path: "docs/verification/public-dashboard.png",
    fullPage: true,
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/rooms");
  await expect(page.locator(".page-header h1")).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
  await page.screenshot({
    path: "docs/verification/public-rooms-mobile.png",
    fullPage: true,
  });
});

test("all 80 Vietnamese screen titles and default tables render", async ({
  page,
}) => {
  test.setTimeout(180000);
  await page.goto("/dev/screens");
  await page.getByRole("button", { name: "VI", exact: true }).click();
  const links = await page.locator(".screen-card").evaluateAll((cards) =>
    cards.map((card) => ({
      id: Number(
        card.querySelector("a[data-screen-id]")!.getAttribute("data-screen-id"),
      ),
      title: card.querySelector("h2")!.textContent!,
      href: card.querySelector("a[data-screen-id]")!.getAttribute("href")!,
    })),
  );
  for (const link of links) {
    await page.goto(link.href);
    await expect(
      page.locator(link.id <= 3 ? ".auth-panel h1" : ".page-header h1"),
    ).toHaveText(link.title);
    await expect(page.locator(".table-section .empty")).toHaveCount(0);
  }
});

test("public mock user create and edit work without signing in", async ({
  page,
}) => {
  await page.goto("/users");
  await page.getByRole("button", { name: "Create user", exact: true }).click();
  await page.getByLabel("Full name").fill("Demo reviewer Alpha");
  await page
    .getByLabel("Username", { exact: false })
    .fill("demo.reviewer.alpha");
  await page
    .getByLabel("Email", { exact: false })
    .fill("alpha@hotelops.example");
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Create user", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Demo reviewer Alpha", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Edit information", exact: true })
    .click();
  await page.getByLabel("Full name").fill("Demo reviewer Beta");
  await page.getByRole("button", { name: "Save changes", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Demo reviewer Beta", exact: true }),
  ).toBeVisible();
});

test("operational UX keeps selected filters, clear actions and mobile records visible", async ({
  page,
}) => {
  await page.goto("/rooms");
  await expect(page.locator('.nav-group a[aria-current="page"]')).toHaveText(
    "Room List",
  );
  await expect(
    page.getByRole("combobox", { name: "Filter by department" }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "VI", exact: true }).click();
  const status = page.locator(".advanced-filters select").first();
  await status.selectOption("Ready");
  await expect(status).toHaveValue("Ready");
  await expect(page.locator("tbody tr")).toHaveCount(16);
  await expect(page.locator(".active-filter-summary")).toContainText(
    "Sẵn sàng",
  );
  await page.locator(".advanced-filters > button").click();
  await expect(status).toHaveValue("");
  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload();
  await expect(page.locator(".advanced-filters")).toBeHidden();
  await expect(page.locator(".domain-filter-panel")).not.toHaveAttribute(
    "open",
  );
  const metrics = await page.evaluate(() => ({
    overflow: document.documentElement.scrollWidth > innerWidth,
    firstRow: document.querySelector("tbody tr")!.getBoundingClientRect().top,
    font: getComputedStyle(document.querySelector("tbody td")!).fontSize,
  }));
  expect(metrics.overflow).toBe(false);
  expect(metrics.firstRow).toBeLessThan(744);
  expect(parseFloat(metrics.font)).toBeGreaterThanOrEqual(14);
  await page.locator(".filter-toggle").click();
  await page.getByLabel("Sắp xếp theo").selectOption("name");
  await expect(
    page.locator("tbody tr").first().locator(".record-link"),
  ).toBeVisible();
});

test("form validation identifies and focuses the invalid field without losing sample data", async ({
  page,
}) => {
  await page.goto("/users");
  await page.getByRole("button", { name: "Create user", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByLabel("Full name")).not.toBeEmpty();
  await dialog.getByLabel("Full name").fill("");
  await dialog.getByLabel("Email", { exact: false }).fill("invalid-email");
  await dialog
    .getByRole("button", { name: "Create user", exact: true })
    .click();
  await expect(dialog.getByLabel("Full name")).toBeFocused();
  await expect(dialog.getByLabel("Full name")).toHaveAttribute(
    "aria-invalid",
    "true",
  );
  await expect(dialog.locator(".field-error")).toHaveCount(2);
  await expect(dialog.getByLabel("Email", { exact: false })).toHaveAttribute(
    "aria-describedby",
    "field-error-email",
  );
  await dialog.getByLabel("Full name").fill("Demo reviewer");
  await expect(dialog.getByLabel("Full name")).toHaveAttribute(
    "aria-invalid",
    "false",
  );
  await page.keyboard.press("Escape");
  await expect(dialog).toHaveCount(0);
});

test("content-first desktop lists show at least five rows and a one-row toolbar in EN and VI", async ({
  page,
}) => {
  test.setTimeout(240000);
  await page.goto("/dev/screens");
  const links = await page.locator("a[data-screen-id]").evaluateAll((nodes) =>
    nodes.map((node) => ({
      id: Number(node.getAttribute("data-screen-id")),
      href: node.getAttribute("href")!,
    })),
  );
  const ids = [
    6, 8, 10, 13, 15, 17, 19, 21, 24, 28, 30, 31, 33, 38, 39, 43, 45, 47, 48,
    50, 51, 54, 56, 57, 59, 61, 62, 64, 66, 69, 70, 71, 73, 74, 76, 79,
  ];
  for (const viewport of [
    { width: 1440, height: 900 },
    { width: 1536, height: 864 },
  ]) {
    await page.setViewportSize(viewport);
    for (const language of ["en", "vi"]) {
      await page.evaluate(
        (lang) => localStorage.setItem("hotelops-language", lang),
        language,
      );
      for (const link of links.filter((link) => ids.includes(link.id))) {
        await page.goto(link.href);
        await expect(page.locator(".page-header h1")).toBeVisible();
        const geometry = await page
          .locator(".table-section")
          .first()
          .evaluate((section) => {
            const viewportHeight = innerHeight;
            const scroll = section
              .querySelector(".table-scroll")!
              .getBoundingClientRect();
            const rows = Array.from(section.querySelectorAll("tbody tr"));
            const toolbar = section
              .querySelector(".filter-bar")!
              .getBoundingClientRect();
            return {
              count: rows.length,
              visible: rows.filter((row) => {
                const rect = row.getBoundingClientRect();
                return (
                  rect.top >= 0 &&
                  rect.bottom <= Math.min(viewportHeight, scroll.bottom)
                );
              }).length,
              toolbarHeight: toolbar.height,
              overflow: document.documentElement.scrollWidth > innerWidth,
              topbar: document.querySelector(".topbar")!.getBoundingClientRect()
                .height,
            };
          });
        expect(geometry.count, link.href + " populated").toBeGreaterThan(0);
        expect(
          geometry.visible,
          link.href + " " + language + " " + viewport.width,
        ).toBeGreaterThanOrEqual(Math.min(5, geometry.count));
        expect(
          geometry.toolbarHeight,
          link.href + " compact toolbar",
        ).toBeLessThanOrEqual(64);
        expect(geometry.topbar).toBeGreaterThanOrEqual(56);
        expect(geometry.topbar).toBeLessThanOrEqual(64);
        expect(geometry.overflow, link.href + " page overflow").toBe(false);
      }
    }
  }
});

test("sticky table headers and compact filter popovers retain their interactions", async ({
  page,
}) => {
  await page.goto("/users");
  const scroll = page.locator(".table-scroll").first();
  const before = await page.locator("thead").boundingBox();
  await scroll.evaluate((element) => (element.scrollTop = 300));
  const after = await page.locator("thead th").first().boundingBox();
  expect(Math.abs(after!.y - before!.y)).toBeLessThan(2);
  await scroll.evaluate((element) => (element.scrollTop = 0));
  await page.getByLabel("From date", { exact: true }).fill("2026-01-01");
  await page.keyboard.press("Escape");
  await expect(page.getByLabel("From date", { exact: true })).toBeVisible();
  await expect(page.locator(".active-filter-summary")).toContainText(
    "2026-01-01",
  );
  await page.locator(".advanced-filters > button").click();
  await expect(page.locator(".active-filter-summary")).toHaveCount(0);
  await page.getByLabel("Filter by role").selectOption("Housekeeping Staff");
  await expect(page.locator("tbody tr").first()).toContainText(
    "Housekeeping Staff",
  );
  await page
    .getByRole("button", { name: "Collapse summary", exact: true })
    .click();
  await expect(page.locator(".summary-grid")).toBeHidden();
  await page
    .getByRole("button", { name: "Expand summary", exact: true })
    .click();
  expect(
    (await page.locator(".summary-grid").boundingBox())!.height,
  ).toBeLessThanOrEqual(95);
  await page.goto("/room-readiness");
  await expect(page.locator('.nav-group a[aria-current="page"]')).toHaveCount(
    1,
  );
  await expect(page.locator('.nav-group a[aria-current="page"]')).toHaveText(
    "Room Readiness Board",
  );
});

test("enterprise record navigation, semantic colors and compact dates", async ({
  page,
}) => {
  await page.goto("/users");
  await expect(page.locator(".open-record")).toHaveCount(0);
  await expect(page.locator("thead th")).toHaveCount(
    await page.locator("tbody tr").first().locator("td").count(),
  );
  const primary = page.locator(".record-link").first();
  const style = await primary.evaluate((e) => ({
    underline: getComputedStyle(e).textDecorationLine,
    weight: getComputedStyle(e).fontWeight,
    color: getComputedStyle(e).color,
  }));
  expect(style.underline).toBe("none");
  expect(Number(style.weight)).toBeGreaterThanOrEqual(600);
  expect(style.color).toBe(
    await primary
      .evaluate((e) =>
        getComputedStyle(e).getPropertyValue("--interaction-link").trim(),
      )
      .then((hex) => {
        const rgb = hex
          .slice(1)
          .match(/../g)!
          .map((c) => parseInt(c, 16));
        return `rgb(${rgb.join(", ")})`;
      }),
  );
  await primary.click();
  await expect(page).toHaveURL(/\/users\/.+/);
  await page.goto("/users");
  const from = await page
    .getByLabel("From date", { exact: true })
    .boundingBox();
  const to = await page.getByLabel("To date", { exact: true }).boundingBox();
  expect(Math.abs(from!.y - to!.y)).toBeLessThan(2);
  expect(
    (await page.locator(".inline-dates").boundingBox())!.height,
  ).toBeLessThan(90);
  const tones = await page.locator(".metric").evaluateAll((es) =>
    es.map((e) => ({
      tone: e.getAttribute("data-tone"),
      bg: getComputedStyle(e).backgroundColor,
    })),
  );
  expect(tones.map((t) => t.tone)).toEqual([
    "info",
    "success",
    "neutral",
    "warning",
  ]);
  expect(new Set(tones.map((t) => t.bg)).size).toBe(1);
  await page.goto("/dashboard");
  await expect(page.locator(".metric").nth(1)).toHaveAttribute(
    "data-tone",
    "danger",
  );
  await page.goto("/dev/design-system");
  const colors = await page
    .locator(".token-examples .status")
    .evaluateAll((es) =>
      Object.fromEntries(
        es.map((e) => [e.textContent, getComputedStyle(e).backgroundColor]),
      ),
    );
  expect(colors.Assigned).not.toBe(colors["In Progress"]);
  expect(colors.Accepted).toBe(colors.Completed);
  expect(colors.Paused).toBe(colors.Pending);
  expect(colors.Cancelled).toBe(colors.Inactive);
});

test("bright refinement keeps clean defaults and independent row controls", async ({
  page,
}) => {
  await page.goto("/users");
  const row = page.locator("tbody tr").first();
  const link = row.locator(".record-link");
  await page.mouse.move(0, 0);
  await expect(link).toHaveCSS("text-decoration-line", "none");
  await row.locator("td").nth(1).hover();
  await expect(link).toHaveCSS("text-decoration-line", "underline");
  await expect(row.locator("td").first()).toHaveCSS(
    "background-color",
    "rgb(225, 233, 243)",
  );
  await row.evaluate((el) => {
    const input = document.createElement("input");
    input.type = "checkbox";
    input.setAttribute("aria-label", "Independent checkbox");
    el.querySelector("td")!.append(input);
    const select = document.createElement("select");
    select.setAttribute("aria-label", "Independent selection");
    select.innerHTML = "<option>A</option><option>B</option>";
    el.querySelector("td")!.append(select);
  });
  await page.getByLabel("Independent checkbox").check();
  await page.getByLabel("Independent selection").selectOption("B");
  await expect(page).toHaveURL(/\/users$/);
  await row.locator("td").nth(1).click();
  await expect(page).toHaveURL(/\/users\/.+/);
  await page.goto("/users");
  await page.locator(".record-link").first().focus();
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL(/\/users\/.+/);
});

test("light and dark modes persist across routes, reload and language", async ({
  page,
}) => {
  await page.goto("/users");
  await page
    .getByRole("button", { name: "Switch to dark mode", exact: true })
    .click();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await expect(page.locator(".status.success").first()).toHaveCSS(
    "background-color",
    "rgb(27, 73, 59)",
  );
  await expect(page.locator(".nav-group a").first()).toHaveCSS(
    "color",
    "rgb(226, 232, 240)",
  );
  await expect(page.locator(".record-link").first()).toHaveCSS(
    "color",
    "rgb(157, 196, 255)",
  );
  await page.reload();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await page.getByRole("button", { name: "VI", exact: true }).click();
  await page
    .getByRole("button", { name: "Chuyển sang sáng", exact: true })
    .click();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "light");
  await page.goto("/dev/screens");
  await page
    .getByRole("button", { name: "Chuyển sang tối", exact: true })
    .click();
  await page.goto("/dev/design-system");
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/users");
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await expect(page.locator(".theme-toggle")).toBeVisible();
});

test("room state chips filter rooms and inline dates share the desktop toolbar", async ({
  page,
}) => {
  await page.goto("/room-readiness");
  const ready = page.locator(".state-filter").filter({ hasText: /^Ready/ });
  await ready.click();
  await expect(ready).toHaveAttribute("aria-pressed", "true");
  for (const badge of await page.locator(".room-tile .status").all())
    await expect(badge).toHaveText("Ready");
  await page
    .getByRole("button", { name: "Switch to dark mode", exact: true })
    .click();
  await expect(page.locator(".room-tile").first()).toHaveCSS(
    "background-color",
    "rgb(25, 38, 59)",
  );
  await page.goto("/users");
  const input = await page
    .getByLabel("From date", { exact: true })
    .boundingBox();
  const search = await page.locator(".search-input input").boundingBox();
  expect(
    Math.abs(input!.y + input!.height - search!.y - search!.height),
  ).toBeLessThan(5);
  await expect(page.locator(".date-range")).toHaveCount(0);
  await page.getByLabel("From date", { exact: true }).fill("2026-01-01");
  await expect(page.locator(".active-filter-summary")).toContainText(
    "2026-01-01",
  );
});
