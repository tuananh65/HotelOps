export const assetConditions: Record<string, string> = {
  GOOD: "Tốt",
  FAIR: "Trung bình",
  POOR: "Kém",
  FAULTY: "Hỏng",
  UNDER_REPAIR: "Đang sửa chữa",
  LOST: "Thất lạc",
};

export function assetConditionLabel(code: string) {
  return assetConditions[code] || "Chưa xác định";
}

export function assetConditionCounts(
  rows: { condition: string; count: number }[],
) {
  const counts = { good: 0, faulty: 0, other: 0 };
  for (const row of rows) {
    const group =
      row.condition === "GOOD"
        ? "good"
        : row.condition === "FAULTY"
          ? "faulty"
          : "other";
    counts[group] += row.count;
  }
  return counts;
}
