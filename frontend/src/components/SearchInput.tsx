import { forwardRef, useId, type InputHTMLAttributes } from "react";

/** Suggestions come only from records already returned to this page. Free text is retained. */
export const SearchInput = forwardRef<
  HTMLInputElement,
  InputHTMLAttributes<HTMLInputElement> & { suggestions?: readonly string[] }
>(function SearchInput({ suggestions = [], ...props }, ref) {
  const id = useId();
  return (
    <>
      <input {...props} ref={ref} list={id} autoComplete="off" />
      <datalist id={id}>
        {[...new Set(suggestions.filter(Boolean))].map((value) => (
          <option key={value} value={value} />
        ))}
      </datalist>
    </>
  );
});
export function searchSuggestions(data: unknown): string[] {
  const result = new Set<string>();
  function collect(value: unknown, depth: number) {
    if (depth > 5 || !value || typeof value !== "object") return;
    if (Array.isArray(value)) {
      value.forEach((item) => collect(item, depth + 1));
      return;
    }
    for (const [key, item] of Object.entries(value)) {
      if (
        typeof item === "string" &&
        /(?:name|title|subject|purpose|number|No)$/i.test(key) &&
        item.length < 250
      )
        result.add(item);
      else if (typeof item === "object") collect(item, depth + 1);
    }
  }
  collect(data, 0);
  return [...result];
}
