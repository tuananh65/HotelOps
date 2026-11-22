import { test } from "node:test";
import assert from "node:assert/strict";
import { reportCsv } from "../src/compact/report-csv.ts";
test("report CSV preserves Vietnamese, commas, quotes, newlines and sparse read DTO columns", () => {
  const csv = reportCsv([
    { name: 'Chi nhánh "Bùi Viện", tầng 1\nPhòng 101', quantity: 2 },
    { name: "Cao Thắng", unit: "cái" },
  ]);
  assert(csv.startsWith("\uFEFF"));
  assert(csv.includes('"Chi nhánh ""Bùi Viện"", tầng 1\nPhòng 101"'));
  assert(csv.includes('"name","quantity","unit"'));
  assert(csv.endsWith('"Cao Thắng","","cái"'));
});
test("exported text cannot become an Excel formula; real negative numeric quantities stay numeric", () => {
  const csv = reportCsv([
    { name: "=1+1", notes: " \t@SUM(1,2)", qty: -2 },
    { name: "+cmd", notes: "-test", qty: 0 },
  ]);
  assert(csv.includes('"\'=1+1"'));
  assert(csv.includes('"\' \t@SUM(1,2)"'));
  assert(csv.includes('"\'+cmd"'));
  assert(csv.includes('"-2"'));
});
