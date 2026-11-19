import {
  Children,
  createContext,
  useContext,
  Fragment,
  cloneElement,
  forwardRef,
  isValidElement,
  useId,
  useRef,
  useState,
  type ReactNode,
  type ReactElement,
  type SelectHTMLAttributes,
} from "react";

export const SelectSearchContext = createContext(true);

const normalize = (value: string) =>
  value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .toLocaleLowerCase("vi");
function text(node: ReactNode): string {
  return Children.toArray(node)
    .map((child) =>
      isValidElement<{ children?: ReactNode }>(child)
        ? text(child.props.children)
        : String(child ?? ""),
    )
    .join("");
}

/** Keeps native select semantics/validation; typing narrows its authorized choices. */
export const SearchableSelect = forwardRef<
  HTMLSelectElement,
  SelectHTMLAttributes<HTMLSelectElement>
>(function SearchableSelect({ children, onChange, ...props }, ref) {
  const searchEnabled = useContext(SelectSearchContext);
  const id = useId();
  const selectRef = useRef<HTMLSelectElement | null>(null);
  const [search, setSearch] = useState("");
  const [uncontrolledValue, setUncontrolledValue] = useState(
    props.defaultValue,
  );
  const query = searchEnabled ? normalize(search.trim()) : "";
  const selected = new Set(
    [props.value ?? uncontrolledValue].flat().map(String),
  );
  function filter(nodes: ReactNode): ReactNode {
    return Children.map(nodes, (child) => {
      if (
        !isValidElement<{
          children?: ReactNode;
          value?: unknown;
          hidden?: boolean;
        }>(child)
      )
        return child;
      if (child.type === Fragment || child.type === "optgroup")
        return cloneElement(child, {}, filter(child.props.children));
      if (child.type !== "option") return child;
      const matches =
        !query || normalize(text(child.props.children)).includes(query);
      // Never silently change the selected value while searching.
      if (
        !matches &&
        child.props.value !== "" &&
        !selected.has(String(child.props.value))
      )
        return null;
      return cloneElement(child as ReactElement<{ hidden?: boolean }>, {
        hidden: child.props.hidden || !matches,
      });
    });
  }
  return (
    <span className="ds-search-select">
      <select
        {...props}
        ref={(node) => {
          selectRef.current = node;
          if (typeof ref === "function") ref(node);
          else if (ref) ref.current = node;
        }}
        onChange={(event) => {
          setUncontrolledValue(
            props.multiple
              ? Array.from(
                  event.target.selectedOptions,
                  (option) => option.value,
                )
              : event.target.value,
          );
          setSearch("");
          onChange?.(event);
        }}
      >
        {filter(children)}
      </select>
      {searchEnabled && <input
        id={id}
        type="search"
        className="ds-select-search"
        aria-label={`Tìm trong ${props["aria-label"] ?? props.name ?? "lựa chọn"}`}
        placeholder="Gõ để tìm lựa chọn…"
        disabled={props.disabled}
        value={search}
        onChange={(event) => {
          if (props.value === undefined && selectRef.current)
            setUncontrolledValue(
              Array.from(
                selectRef.current.selectedOptions,
                (option) => option.value,
              ),
            );
          setSearch(event.target.value);
        }}
        onKeyDown={(event) => {
          if (event.key === "Enter") event.preventDefault();
        }}
      />}
    </span>
  );
});
