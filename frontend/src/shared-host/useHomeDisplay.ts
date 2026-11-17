import { useState } from "react";
type Display = { personal: boolean };
const defaults: Display = { personal: true };
function read(key: string): Display {
  try {
    const value = JSON.parse(localStorage.getItem(key) || "null");
    return { personal: typeof value?.personal === "boolean" ? value.personal : true };
  } catch { return defaults; }
}
export function useHomeDisplay(userId: string) {
  const key = `hotelops.home.display.v1:${userId}`;
  const [saved, setSaved] = useState(() => ({ key, value: read(key) }));
  const value = saved.key === key ? saved.value : read(key);
  function update(field: keyof Display, checked: boolean) {
    const next = { ...value, [field]: checked };
    setSaved({ key, value: next });
    try { localStorage.setItem(key, JSON.stringify(next)); } catch { /* This session still honors the preference. */ }
  }
  return { value, update };
}
