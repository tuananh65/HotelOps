// Decimal arithmetic avoids rounding an invalid conversion into a valid order.
export function replenishmentBaseQuantity(quantity: unknown, factor: unknown): number | null {
  function scaled(value: unknown, digits: number): bigint | null {
    const match = /^(\d+)(?:\.(\d+))?$/.exec(String(value));
    if (!match || (match[2]?.length ?? 0) > digits) return null;
    return BigInt(match[1] + (match[2] ?? "").padEnd(digits, "0"));
  }
  const qty = scaled(quantity, 3), rate = scaled(factor, 6);
  if (qty === null || rate === null || qty <= 0n || rate <= 0n) return null;
  if (scaled(Number(quantity), 3) !== qty) return null;
  const product = qty * rate;
  if (product % 1000000n !== 0n) return null;
  const thousandths = product / 1000000n;
  if (thousandths > BigInt(Number.MAX_SAFE_INTEGER)) return null;
  const result = Number(thousandths) / 1000;
  // Ensure the JSON number preserves the exact decimal value sent to SQL.
  return scaled(result, 3) === thousandths ? result : null;
}
