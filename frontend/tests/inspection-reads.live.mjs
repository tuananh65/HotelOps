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
  "../scripts/verification/InspectionReadLive/bin/Debug/net10.0/InspectionReadLive.dll",
);
const report = {
  task: "FE05-FAST-002",
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
const priorFile = process.argv[3];
const priorPass = new Set(
  priorFile
    ? JSON.parse(fs.readFileSync(path.resolve(priorFile), "utf8"))
        .cases.filter((x) => x.result === "PASS")
        .map((x) => x.name)
    : [],
);
async function check(name, action) {
  if (priorPass.has(name) && name !== "Real Sign-In and cookie session") {
    report.cases.push({
      name,
      result: "NOT_RERUN",
      priorReceipt: path.basename(priorFile),
    });
    console.log("NOT_RERUN " + name);
    return;
  }
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
  await check("Real Sign-In and cookie session", async () => {
    await login(page, "int.hk");
    assert.equal((await request(page, "GET", "/api/auth/session")).status, 200);
    const cookie = (await context.cookies()).find(
      (c) => c.name === "__Host-HotelOps.Auth",
    );
    assert.ok(cookie?.secure && cookie?.httpOnly);
  });
  await check(
    "Severity lookup: active MINOR/MAJOR domain, inactive MAJOR excluded, branch and input scope",
    async () => {
      const values = await request(
        page,
        "GET",
        `/api/branches/${branch}/inspection-severities`,
      );
      assert.equal(values.status, 200);
      assert.deepEqual(
        values.body.map((x) => x.id),
        [runtime.severityId],
      );
      assert.ok(!values.body.some((x) => x.id === runtime.majorId));
      assert.equal(
        (
          await request(
            page,
            "GET",
            `/api/branches/${runtime.foreign}/inspection-severities`,
          )
        ).status,
        403,
      );
      assert.equal(
        (await request(page, "GET", "/api/branches/0/inspection-severities"))
          .status,
        400,
      );
    },
  );
  await check(
    "Assigned list belongs to session actor; search and server pagination",
    async () => {
      const session = await request(page, "GET", "/api/auth/session");
      const list = await request(
        page,
        "GET",
        `/api/branches/${branch}/inspections/assigned?page=1&pageSize=1`,
      );
      assert.equal(list.status, 200);
      assert.equal(list.body.total, 2);
      assert.equal(list.body.items.length, 1);
      assert.equal(
        list.body.items[0].inspectorId,
        Number(session.body.user.id),
      );
      const next = await request(
        page,
        "GET",
        `/api/branches/${branch}/inspections/assigned?page=2&pageSize=1`,
      );
      assert.equal(next.status, 200);
      assert.notEqual(next.body.items[0].id, list.body.items[0].id);
      assert.equal(
        (
          await request(
            page,
            "GET",
            `/api/branches/${branch}/inspections/assigned?page=0`,
          )
        ).status,
        400,
      );
      assert.equal(
        (
          await request(
            page,
            "GET",
            `/api/branches/${branch}/inspections/assigned?pageSize=101`,
          )
        ).status,
        400,
      );
      const search = await request(
        page,
        "GET",
        `/api/branches/${branch}/inspections/assigned?search=FAST_INSP_1`,
      );
      assert.equal(search.status, 200);
      assert.equal(search.body.total, 1);
    },
  );
  await check("Assigned list UI opens actual assigned execution", async () => {
    await page.goto(runtime.origin + "/inspections");
    await page.getByLabel("List branch ID").fill(String(branch));
    await page
      .getByRole("button", { name: "Load inspections", exact: true })
      .click();
    await page
      .getByRole("link", { name: "Open FAST_INSP_1", exact: true })
      .click();
    await expect(page).toHaveURL(
      new RegExp(`/inspections/${branch}/${id}/execution$`),
    );
    await expect(
      page.getByText("Assigned checklist version ID:", { exact: false }),
    ).toBeVisible();
  });
  await check(
    "Assignment queue excludes cycles with open inspection; planning preview and writer dependency enforced",
    async () => {
      const queue = await request(
        page,
        "GET",
        `/api/branches/${branch}/inspection-assignment-tasks`,
      );
      assert.equal(queue.status, 200);
      assert.equal(queue.body.total, 1);
      assert.equal(queue.body.items[0].id, runtime.taskId);
      await page.goto(runtime.origin + "/inspections");
      await page.getByLabel("List branch ID").fill(String(branch));
      await page
        .getByLabel("Inspection list", { exact: true })
        .selectOption("tasks");
      await page
        .getByRole("button", { name: "Load inspections", exact: true })
        .click();
      await page
        .getByRole("button", { name: "Plan FAST_TASK_3", exact: true })
        .click();
      await expect(
        page.getByText("Assignment submission unavailable:", { exact: false }),
      ).toBeVisible();
      await expect(page.getByLabel("Inspector candidate")).toContainText(
        "Synthetic int.hk",
      );
      const options = await page
        .getByLabel("Inspector candidate")
        .locator("option")
        .evaluateAll((xs) =>
          xs.map((x) => ({ id: x.value, text: x.textContent })),
        );
      const choice = options.find((x) => x.text?.includes("int.hk"));
      assert.ok(choice);
      await page.getByLabel("Inspector candidate").selectOption(choice.id);
      assert.equal(
        report.http.filter(
          (x) =>
            x.method === "POST" &&
            /\/cleaning-tasks\/\d+\/inspections$/.test(x.path),
        ).length,
        0,
      );
    },
  );
  await check(
    "Concurrent read transactions return scoped snapshots",
    async () => {
      const results = await Promise.all(
        Array.from({ length: 4 }, (_, n) =>
          request(
            page,
            "GET",
            n % 2
              ? `/api/branches/${branch}/inspections/assigned`
              : `/api/branches/${branch}/inspection-severities`,
          ),
        ),
      );
      assert.ok(results.every((r) => r.status === 200));
    },
  );
  await check(
    "Severity selector submits server ID; inactive severity rejected and defect persists after reload",
    async () => {
      await page.goto(
        runtime.origin + `/inspections/${branch}/${id}/execution`,
      );
      await page
        .getByLabel("Operation reason")
        .fill("FAST002 severity integration");
      await clickMutation("Start Inspection", "/start");
      await page
        .getByLabel("Outcome: Synthetic bootstrap")
        .selectOption("FAIL");
      await clickMutation("Save Draft", "/results", "PUT");
      const read = await request(page, "GET", route + "/execution");
      assert.equal(
        (
          await request(
            page,
            "POST",
            route + "/defects",
            {
              itemId: read.body.criteria[0].itemId,
              severityId: runtime.majorId,
              description: "Inactive must reject",
              location: "Desk",
            },
            read.etag,
          )
        ).status,
        400,
      );
      const selector = page.getByLabel("Severity", { exact: true });
      await expect(
        selector.locator(`option[value="${runtime.severityId}"]`),
      ).toHaveCount(1);
      await expect(
        selector.locator(`option[value="${runtime.majorId}"]`),
      ).toHaveCount(0);
      await selector.selectOption(String(runtime.severityId));
      await page
        .getByLabel("Failed criterion")
        .selectOption(String(read.body.criteria[0].itemId));
      await page
        .getByLabel("Description", { exact: true })
        .fill("FAST002 selected severity defect");
      await page.getByLabel("Location in room").fill("Desk");
      await clickMutation("Record defect", "/defects");
      await page.reload();
      await expect(
        page.getByText(/#\d+ FAST002 selected severity defect/),
      ).toBeVisible();
      const persisted = await request(page, "GET", route + "/execution");
      assert.equal(persisted.body.defects[0].severityId, runtime.severityId);
    },
  );
  await check(
    "Read collections empty, wrong branch forbidden, denied account and anonymous handling",
    async () => {
      const empty = await request(
        page,
        "GET",
        `/api/branches/${branch}/inspections/assigned?search=NO_MATCH`,
      );
      assert.equal(empty.status, 200);
      assert.equal(empty.body.total, 0);
      for (const suffix of [
        "inspection-severities",
        "inspections/assigned",
        "inspection-assignment-tasks",
      ])
        assert.equal(
          (
            await request(
              page,
              "GET",
              `/api/branches/${runtime.foreign}/${suffix}`,
            )
          ).status,
          403,
        );
      const other = await browser.newContext({ ignoreHTTPSErrors: true });
      const p = await other.newPage();
      await p.goto(runtime.origin + "/login");
      for (const suffix of [
        "inspection-severities",
        "inspections/assigned",
        "inspection-assignment-tasks",
      ])
        assert.equal(
          (await request(p, "GET", `/api/branches/${branch}/${suffix}`)).status,
          401,
        );
      await login(p, "int.denied");
      for (const suffix of [
        "inspection-severities",
        "inspections/assigned",
        "inspection-assignment-tasks",
      ])
        assert.equal(
          (await request(p, "GET", `/api/branches/${branch}/${suffix}`)).status,
          403,
        );
      await other.close();
    },
  );
  await check("SQL persistence and unchanged assignment count", () =>
    sql("verify"),
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
