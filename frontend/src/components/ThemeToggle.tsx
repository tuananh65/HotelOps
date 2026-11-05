import { Moon, Sun } from "lucide-react";
import { useSyncExternalStore } from "react";
import { getLanguage, useLanguage } from "../i18n";
const key = "hotelops-theme";
export function initializeTheme() {
  const saved = localStorage.getItem(key);
  document.documentElement.dataset.theme = saved === "dark" ? "dark" : "light";
}
function subscribe(listener: () => void) {
  window.addEventListener("hotelops-theme-change", listener);
  const sync = () => {
    initializeTheme();
    listener();
  };
  window.addEventListener("storage", sync);
  return () => {
    window.removeEventListener("hotelops-theme-change", listener);
    window.removeEventListener("storage", sync);
  };
}
export function ThemeToggle() {
  useLanguage();
  const theme = useSyncExternalStore(subscribe, () =>
    localStorage.getItem(key) === "dark" ? "dark" : "light",
  );
  const dark = theme === "dark";
  const label =
    getLanguage() === "vi"
      ? dark
        ? "Chuyển sang sáng"
        : "Chuyển sang tối"
      : dark
        ? "Switch to light mode"
        : "Switch to dark mode";
  return (
    <button
      className="theme-toggle"
      title={label}
      aria-label={label}
      aria-pressed={dark}
      onClick={() => {
        const next = dark ? "light" : "dark";
        localStorage.setItem(key, next);
        document.documentElement.dataset.theme = next;
        window.dispatchEvent(new Event("hotelops-theme-change"));
      }}
    >
      {dark ? <Sun size={18} /> : <Moon size={18} />}
    </button>
  );
}
