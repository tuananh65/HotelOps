import { chromium } from "@playwright/test";
let input = "";
for await (const chunk of process.stdin) input += chunk;
const fixture = JSON.parse(input);
input = "";
let browser,
  stage = "login";
try {
  browser = await chromium.launch({
    executablePath:
      "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    headless: true,
  });
  const page = await browser.newPage();
  page.setDefaultTimeout(45000);
  const origin = "https://localhost:7142",
    path = `/inspections/${fixture.branch}/${fixture.inspection}/execution`,
    api = `/api/branches/${fixture.branch}/inspections/${fixture.inspection}`;
  let writes = 0;
  page.on("request", (request) => {
    if (
      request.url() === origin + api + "/reject" &&
      request.method() === "POST"
    )
      writes++;
  });
  await page.goto(origin + path);
  await page.waitForURL("**/login?*");
  await page.getByLabel("Username or email").fill(fixture.username);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password);
  fixture.password = "";
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL(origin + path);
  await page.getByText(/^State: PENDING_CONCLUSION\./).waitFor();
  const before = await page.evaluate(async (api) => {
    const r = await fetch(api + "/execution", { credentials: "include" });
    return {
      status: r.status,
      tag: r.headers.get("ETag"),
      body: await r.json(),
    };
  }, api);
  if (before.status !== 200 || !before.tag)
    throw Error("Source read unavailable");
  await page.getByLabel("Reason", { exact: true }).fill("Synthetic rejection");
  await page
    .getByLabel("Confirm inspection failure and require cleaning rework")
    .check();
  const button = page.getByRole("button", {
    name: "Reject inspection",
    exact: true,
  });
  if (fixture.mode === "NO_DEFECT") {
    stage = "no-defect UI and HTTPS guard";
    if (before.body.defects.length !== 0 || !(await button.isDisabled()))
      throw Error("No-defect UI admitted rejection");
    await page
      .getByText(
        "MSG03: Defects: record a defect for each failed criterion before rejecting the inspection. Please check and try again.",
        { exact: true },
      )
      .waitFor();
    const status = await page.evaluate(
      async ({ api, tag }) => {
        const csrf = await (
          await fetch("/api/auth/csrf", { credentials: "include" })
        ).json();
        return (
          await fetch(api + "/reject", {
            method: "POST",
            credentials: "include",
            headers: {
              "Content-Type": "application/json",
              "X-CSRF-TOKEN": csrf.csrfToken,
              "If-Match": tag,
            },
            body: JSON.stringify({
              reason: "Synthetic rejection",
              confirmed: true,
            }),
          })
        ).status;
      },
      { api, tag: before.tag },
    );
    if (status !== 400)
      throw Error("No-defect endpoint expected400, received" + status);
  } else if (fixture.mode === "SHORT_REASON") {
    stage = "short reason UI and HTTPS guard";
    await page.getByLabel("Reason", { exact: true }).fill(" 123456789 ");
    if (!before.body.defects.length || !(await button.isDisabled()))
      throw Error("Short reason admitted rejection");
    await page
      .getByText(
        "MSG03: Rejection reason must contain at least 10 characters.",
        { exact: true },
      )
      .waitFor();
    const status = await page.evaluate(
      async ({ api, tag }) => {
        const csrf = await (
          await fetch("/api/auth/csrf", { credentials: "include" })
        ).json();
        return (
          await fetch(api + "/reject", {
            method: "POST",
            credentials: "include",
            headers: {
              "Content-Type": "application/json",
              "X-CSRF-TOKEN": csrf.csrfToken,
              "If-Match": tag,
            },
            body: JSON.stringify({ reason: " 123456789 ", confirmed: true }),
          })
        ).status;
      },
      { api, tag: before.tag },
    );
    if (status !== 400)
      throw Error("Short reason expected400, received" + status);
  } else if (fixture.mode === "AUDIT_FAULT") {
    stage = "canonical Reject with audit fault";
    if (!before.body.defects.length) throw Error("Valid defect missing");
    const pending = page.waitForResponse(
      (r) =>
        r.url() === origin + api + "/reject" && r.request().method() === "POST",
    );
    await button.click();
    const response = await pending;
    if (response.status() !== 500)
      throw Error("Audit fault expected500, received" + response.status());
    await page
      .getByText(
        "Write outcome unknown. Verify server state in a fresh authorized read; do not automatically retry.",
        { exact: true },
      )
      .waitFor();
    if (
      await page
        .getByRole("button", { name: "Reject inspection", exact: true })
        .count()
    )
      throw Error("Uncertain mutation remains actionable");
  } else throw Error("Unknown trial mode");
  stage = "unchanged HTTPS source and no replay";
  const after = await page.evaluate(async (api) => {
    const r = await fetch(api + "/execution", { credentials: "include" });
    return {
      status: r.status,
      tag: r.headers.get("ETag"),
      body: await r.json(),
    };
  }, api);
  if (JSON.stringify(after) !== JSON.stringify(before) || writes !== 1)
    throw Error("Rejected request changed source or replayed");
  process.stdout.write(
    JSON.stringify({
      success: true,
      mode: fixture.mode,
      statusUnchanged: true,
      writes,
    }),
  );
} catch (error) {
  process.stdout.write(
    JSON.stringify({
      success: false,
      stage,
      diagnostic: String(error.message).split("\n")[0],
    }),
  );
  process.exitCode = 1;
} finally {
  await browser?.close();
}
