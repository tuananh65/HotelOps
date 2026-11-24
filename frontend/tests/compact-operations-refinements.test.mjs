import test from "node:test";
import assert from "node:assert/strict";
import {
  assetConditionCounts,
  assetConditionLabel,
} from "../src/compact/asset-presentation.ts";

test("asset segments partition every condition without treating repairs as good or faulty", () => {
  assert.deepEqual(
    assetConditionCounts([
      { condition: "GOOD", count: 7 },
      { condition: "FAULTY", count: 3 },
      { condition: "FAIR", count: 2 },
      { condition: "POOR", count: 1 },
      { condition: "UNDER_REPAIR", count: 4 },
      { condition: "LOST", count: 1 },
    ]),
    { good: 7, faulty: 3, other: 8 },
  );
  assert.deepEqual(assetConditionCounts([]), { good: 0, faulty: 0, other: 0 });
});
test("condition labels are Vietnamese and unknown values do not imply good condition", () => {
  assert.equal(assetConditionLabel("GOOD"), "Tốt");
  assert.equal(assetConditionLabel("FAULTY"), "Hỏng");
  assert.equal(assetConditionLabel("UNDER_REPAIR"), "Đang sửa chữa");
  assert.equal(assetConditionLabel("UNRECOGNIZED"), "Chưa xác định");
});
