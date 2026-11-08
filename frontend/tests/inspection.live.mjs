// LIVE runner: real SignInScreen + ASP.NET + owned SQL. No HTTP interception.
import https from "node:https";
import fs from "node:fs";
import path from "node:path";
import { spawn } from "node:child_process";
import assert from "node:assert/strict";
import { chromium, expect as baseExpect } from "@playwright/test";
const expect = baseExpect.configure({ timeout: 120000 });
const out = path.resolve(process.argv[2]);
const environment = path.join(out, "environment.json");
const env = JSON.parse(fs.readFileSync(environment, "utf8"));
if (!env.success || !env.createdByThisRun || !env.syntheticOnly)
  throw Error("Isolation receipt required");
const runtime = JSON.parse(
  fs.readFileSync(path.join(out, "runtime.private.json"), "utf8"),
);
const harness = path.resolve(
  "../scripts/verification/InspectionLive/bin/Debug/net10.0/InspectionLive.dll",
);
const report = {
  task: "FE05-FAST-001",
  database: env.database,
  owner: env.owner,
  live: true,
  httpInterception: false,
  cases: [],
  http: [],
  success: false,
};
async function sql(mode) {
  return new Promise((resolve, reject) => {
    const p = spawn("dotnet", [harness, mode, environment, out], {
      windowsHide: true,
      stdio: ["ignore", "pipe", "pipe"],
    });
    let err = "";
    p.stderr.on("data", (x) => (err += x));
    p.on("error", reject);
    p.on("exit", (code) =>
      code === 0
        ? resolve()
        : reject(Error(`Owned SQL ${mode} failed: ${err.slice(0, 500)}`)),
    );
  });
}
await sql("probe");
const cert = fs.readFileSync(path.join(out, "runtime.private.cert.pem")),
  key = fs.readFileSync(path.join(out, "runtime.private.key.pem")),
  dist = path.resolve("dist");
const proxy = https.createServer({ cert, key }, async (req, res) => {
  if (req.url.startsWith("/api/")) {
    if (!["GET", "HEAD", "OPTIONS"].includes(req.method)) {
      try {
        await sql("probe");
      } catch {
        res.writeHead(503);
        res.end();
        return;
      }
    }
    const upstream = https.request(
      new URL(req.url, runtime.api),
      { method: req.method, ca: cert, headers: req.headers },
      (r) => {
        report.http.push({
          method: req.method,
          path: req.url,
          status: r.statusCode,
        });
        res.writeHead(r.statusCode, r.headers);
        r.pipe(res);
      },
    );
    upstream.on("error", () => {
      if (!res.headersSent) res.writeHead(502);
      res.end();
    });
    req.pipe(upstream);
    return;
  }
  const pathname = decodeURIComponent(
    new URL(req.url, runtime.origin).pathname,
  );
  let file = path.resolve(dist, "." + pathname);
  if (!file.startsWith(dist + path.sep) && file !== dist) {
    res.writeHead(403);
    res.end();
    return;
  }
  if (!fs.existsSync(file) || fs.statSync(file).isDirectory())
    file = path.join(dist, "index.html");
  res.writeHead(200, {
    "Content-Type":
      {
        ".html": "text/html",
        ".js": "text/javascript",
        ".css": "text/css",
        ".svg": "image/svg+xml",
      }[path.extname(file)] || "application/octet-stream",
    "Cache-Control": "no-store",
  });
  fs.createReadStream(file).pipe(res);
});
await new Promise((resolve) => proxy.listen(7448, "127.0.0.1", resolve));
const browser = await chromium.launch({
  executablePath:
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
});
const context = await browser.newContext({ ignoreHTTPSErrors: true });
const page = await context.newPage();
page.setDefaultTimeout(120000);
const branch = runtime.branch,
  id = runtime.inspectionIds[0],
  second = runtime.inspectionIds[1];
const route = `/api/branches/${branch}/inspections/${id}`;
async function check(name, action) {
  try {
    await action();
    report.cases.push({ name, result: "PASS" });
    console.log("PASS " + name);
  } catch (e) {
    report.cases.push({ name, result: "FAIL", error: e.message.slice(0, 800) });
    throw e;
  }
}
async function request(p, method, url, body, etag, csrf = true) {
  return p.evaluate(
    async ({ method, url, body, etag, csrf }) => {
      const headers = { Accept: "application/json" };
      if (body !== undefined) headers["Content-Type"] = "application/json";
      if (etag) headers["If-Match"] = etag;
      if (csrf && method !== "GET") {
        const token = await (
          await fetch("/api/auth/csrf", { credentials: "include" })
        ).json();
        headers[token.headerName] = token.csrfToken;
      }
      const r = await fetch(url, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
        credentials: "include",
      });
      return {
        status: r.status,
        etag: r.headers.get("etag"),
        body: await r.json().catch(() => null),
      };
    },
    { method, url, body, etag, csrf },
  );
}
async function login(p, user) {
  await p.goto(runtime.origin + "/login");
  await p.getByLabel("Username or email").fill(user);
  await p.getByLabel("Password", { exact: true }).fill(runtime.password);
  await p.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(p).toHaveURL(/\/dashboard$/);
}
async function clickMutation(label, suffix, method = "POST") {
  const wait = page.waitForResponse(
    (r) =>
      new URL(r.url()).pathname.endsWith(suffix) &&
      r.request().method() === method,
    { timeout: 120000 },
  );
  await page.getByRole("button", { name: label, exact: true }).click();
  const response = await wait;
  assert.equal(response.status(), 200);
  await expect(
    page.getByText(`${suffix.slice(1)} saved.`, { exact: false }),
  ).toBeVisible();
}
const pdf = {
  name: "evidence.pdf",
  mimeType: "application/pdf",
  buffer: Buffer.from("%PDF-1.4\nSynthetic FE05 evidence\n%%EOF"),
};
try {
  await check("Real SignInScreen, cookie session and HTTPS", async () => {
    await login(page, "int.hk");
    assert.equal((await request(page, "GET", "/api/auth/session")).status, 200);
    const cookie = (await context.cookies()).find(
      (c) => c.name === "__Host-HotelOps.Auth",
    );
    assert.ok(cookie?.secure && cookie?.httpOnly && cookie?.sameSite === "Lax");
  });
  await check(
    "Assigned source, Start, mandatory validation, FAIL and criterion evidence Save",
    async () => {
      await page.goto(
        runtime.origin + `/inspections/${branch}/${id}/execution`,
      );
      await page
        .getByLabel("Operation reason")
        .fill("Live independent inspection");
      await clickMutation("Start Inspection", "/start");
      await expect(
        page.getByRole("button", { name: "Finish Inspection", exact: true }),
      ).toBeDisabled();
      const before = await request(page, "GET", route + "/execution");
      assert.equal(
        (
          await request(
            page,
            "POST",
            route + "/finish",
            { reason: "Missing mandatory" },
            before.etag,
          )
        ).status,
        400,
      );
      await page
        .getByLabel("Outcome: Synthetic bootstrap")
        .selectOption("FAIL");
      await page
        .getByLabel("Criterion evidence: Synthetic bootstrap")
        .setInputFiles(pdf);
      await clickMutation("Save Draft", "/results", "PUT");
      await expect(
        page.getByText("Defect required: Synthetic bootstrap"),
      ).toBeVisible();
    },
  );
  await check(
    "Record defect with actual severity reference and private evidence",
    async () => {
      const read = await request(page, "GET", route + "/execution");
      await page
        .getByLabel("Failed criterion")
        .selectOption(String(read.body.criteria[0].itemId));
      await page
        .getByLabel("Severity", { exact: true })
        .selectOption(String(runtime.severityId));
      await page
        .getByLabel("Description", { exact: true })
        .fill("Synthetic dust defect");
      await page.getByLabel("Location in room").fill("Desk");
      await page
        .getByLabel("Defect evidence", { exact: true })
        .setInputFiles({ ...pdf, name: "defect.pdf" });
      await clickMutation("Record defect", "/defects");
      await expect(page.getByText(/#\d+ Synthetic dust defect/)).toBeVisible();
    },
  );
  await check(
    "Stale ETag rejected, real CSRF enforced, Finish and persistence after reload",
    async () => {
      const old = await request(page, "GET", route + "/execution");
      await clickMutation("Finish Inspection", "/finish");
      assert.equal(
        (
          await request(
            page,
            "POST",
            route + "/finish",
            { reason: "Stale duplicate" },
            old.etag,
          )
        ).status,
        412,
      );
      const now = await request(page, "GET", route + "/execution");
      assert.equal(
        (
          await request(
            page,
            "POST",
            route + "/defects",
            {
              itemId: now.body.criteria[0].itemId,
              severityId: runtime.severityId,
              description: "CSRF must reject",
              location: "Desk",
            },
            now.etag,
            false,
          )
        ).status,
        400,
      );
      await page.reload();
      await expect(
        page.getByText(
          "Pending Conclusion. No approval/rejection action is provided here.",
        ),
      ).toBeVisible();
      await expect(page.getByText(/#\d+ Synthetic dust defect/)).toBeVisible();
    },
  );
  await check(
    "Second inspection PASS Save/Finish through real UI",
    async () => {
      await page.goto(
        runtime.origin + `/inspections/${branch}/${second}/execution`,
      );
      await page
        .getByLabel("Operation reason")
        .fill("Live PASS criteria inspection");
      const secondRoute = `/api/branches/${branch}/inspections/${second}`;
      const prior = await request(page, "GET", secondRoute + "/execution");
      assert.equal(
        (
          await request(
            page,
            "POST",
            secondRoute + "/start",
            { reason: "Concurrent authorized operation" },
            prior.etag,
          )
        ).status,
        200,
      );
      const stale = page.waitForResponse(
        (r) =>
          new URL(r.url()).pathname === secondRoute + "/start" &&
          r.request().method() === "POST",
      );
      await page
        .getByRole("button", { name: "Start Inspection", exact: true })
        .click();
      assert.equal((await stale).status(), 412);
      await expect(
        page.getByText("Further writes are blocked.", { exact: false }),
      ).toBeVisible();
      await page
        .getByRole("button", { name: "Reload inspection", exact: true })
        .click();
      await page
        .getByLabel("Outcome: Synthetic bootstrap")
        .selectOption("PASS");
      await clickMutation("Save Draft", "/results", "PUT");
      await clickMutation("Finish Inspection", "/finish");
    },
  );
  await check(
    "History collection search, branch filter, pagination and detail",
    async () => {
      await page.goto(runtime.origin + "/inspections/history");
      await page.getByLabel("branchId", { exact: true }).fill(String(branch));
      await page.getByLabel("Search", { exact: true }).fill("FAST_INSP_1");
      await page
        .getByRole("button", { name: "Search history", exact: true })
        .click();
      await expect(page.getByText("1 inspections · Page 1")).toBeVisible();
      const list = await request(
        page,
        "GET",
        `/api/inspection-history?branchId=${branch}&page=2&pageSize=1`,
      );
      assert.equal(list.status, 200);
      assert.equal(list.body.filters.page, 2);
      assert.equal(list.body.items.length, 1);
      await page
        .getByRole("link", { name: "FAST_INSP_1", exact: true })
        .click();
      await expect(
        page.getByRole("heading", { name: "FAST_INSP_1", exact: true }),
      ).toBeVisible();
      await expect(page.getByText(/#\d+ Synthetic dust defect/)).toBeVisible();
    },
  );
  await check(
    "Evidence download authorized by backend and actual bytes",
    async () => {
      const download = page.waitForEvent("download");
      await page
        .getByRole("button", { name: "Download defect.pdf", exact: true })
        .click();
      const file = await download;
      const stream = await file.createReadStream();
      let data = "";
      for await (const chunk of stream) data += chunk.toString();
      assert.equal(data, pdf.buffer.toString());
    },
  );
  await check(
    "Real branch isolation, denied account 403 and anonymous 401",
    async () => {
      assert.equal(
        (
          await request(
            page,
            "GET",
            `/api/branches/${runtime.foreign}/inspections/${id}/execution`,
          )
        ).status,
        403,
      );
      assert.equal(
        (
          await request(
            page,
            "GET",
            `/api/inspection-history?branchId=${runtime.foreign}`,
          )
        ).status,
        403,
      );
      const other = await browser.newContext({ ignoreHTTPSErrors: true });
      const p = await other.newPage();
      await p.goto(runtime.origin + "/login");
      assert.equal((await request(p, "GET", route + "/execution")).status, 401);
      await login(p, "int.denied");
      assert.equal((await request(p, "GET", route + "/execution")).status, 403);
      await p.goto(runtime.origin + `/inspections/${branch}/${id}/execution`);
      await expect(p.getByRole("alert").first()).toContainText("Access denied");
      await other.close();
    },
  );
  await check(
    "Independent SQL persistence results/defect/evidence/session/audit",
    () => sql("verify"),
  );
  report.success = true;
} catch (e) {
  report.error = e.message.slice(0, 1200);
  console.error("FAIL " + report.error);
  process.exitCode = 1;
} finally {
  await fs.promises.writeFile(
    path.join(out, "live-results.json"),
    JSON.stringify(report, null, 2),
  );
  await browser.close();
  proxy.closeAllConnections();
  await new Promise((resolve) => proxy.close(resolve));
  fs.writeFileSync(path.join(out, "stop"), "stop owned process");
}
