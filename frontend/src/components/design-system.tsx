import { userMessage } from "../i18n/userMessage";
import {
  forwardRef,
  useEffect,
  useId,
  useRef,
  useState,
  type ButtonHTMLAttributes,
  type InputHTMLAttributes,
  type SelectHTMLAttributes,
  type ReactNode,
} from "react";
import { createPortal } from "react-dom";
import { statusColor } from "./statusColors";
import { SearchableSelect } from "./SearchableSelect";
import { Link } from "react-router-dom";
import {
  AlertCircle,
  ArrowDown,
  ArrowUp,
  ArrowUpDown,
  ChevronLeft,
  ChevronRight,
  CircleCheck,
  EllipsisVertical,
  Inbox,
  Info,
  LoaderCircle,
  X,
  type LucideIcon,
} from "lucide-react";

export const AppInput = forwardRef<
  HTMLInputElement,
  InputHTMLAttributes<HTMLInputElement>
>(function AppInput({ className = "", ...props }, ref) {
  return <input {...props} ref={ref} className={`ds-control ${className}`} />;
});
export const AppSelect = forwardRef<
  HTMLSelectElement,
  SelectHTMLAttributes<HTMLSelectElement>
>(function AppSelect({ className = "", ...props }, ref) {
  return <SearchableSelect {...props} ref={ref} className={`ds-control ${className}`} />;
});

export const AppButton = forwardRef<
  HTMLButtonElement,
  ButtonHTMLAttributes<HTMLButtonElement> & {
    variant?: "primary" | "secondary" | "ghost" | "danger";
    loading?: boolean;
    icon?: LucideIcon;
  }
>(function AppButton(
  {
    variant = "secondary",
    loading = false,
    icon: Icon,
    children,
    className = "",
    disabled,
    type = "button",
    ...props
  },
  ref,
) {
  return (
    <button
      {...props}
      ref={ref}
      type={type}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      className={`ds-button ds-button--${variant} ${className}`}
    >
      {loading ? (
        <LoaderCircle className="ds-spin" size={16} aria-hidden="true" />
      ) : Icon ? (
        <Icon size={16} aria-hidden="true" />
      ) : null}
      {children}
    </button>
  );
});
export function IconButton({
  label,
  icon: Icon,
  ...props
}: Omit<React.ComponentProps<typeof AppButton>, "children" | "icon"> & {
  label: string;
  icon: LucideIcon;
}) {
  return (
    <AppButton
      {...props}
      variant={props.variant ?? "ghost"}
      icon={Icon}
      aria-label={label}
      title={label}
      className={`ds-icon-button ${props.className ?? ""}`}
    />
  );
}
export function WorkspacePageHeader({
  title,
  description,
  actions,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
}) {
  return (
    <header className="ds-page-header">
      <div>
        <h1>{title}</h1>
        {description && <p>{description}</p>}
      </div>
      {actions && <div className="ds-actions">{actions}</div>}
    </header>
  );
}
export function ContentCard({
  title,
  children,
  className = "",
}: {
  title?: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <section className={`ds-card ${className}`}>
      {title && <h2>{title}</h2>}
      {children}
    </section>
  );
}
export type StatusTone = "success" | "warning" | "error" | "info" | "neutral";
/** Presentation only. Callers pass the label/tone; no workflow or permission is inferred. */
export function StatusBadge({
  children,
  tone,
}: {
  children: ReactNode;
  tone?: StatusTone;
}) {
  return <span className={`ds-badge ds-badge--${tone ?? "state"}`} style={!tone && typeof children === "string" ? statusColor(children) : undefined}>{children}</span>;
}
export function EmptyState({
  title,
  description,
  action,
  icon: Icon = Inbox,
}: {
  title: string;
  description?: string;
  action?: ReactNode;
  icon?: LucideIcon;
}) {
  return (
    <div className="ds-empty" role="status">
      <Icon size={42} aria-hidden="true" />
      <h3>{title}</h3>
      {description && <p>{description}</p>}
      {action}
    </div>
  );
}
export function ErrorState({
  message,
  retry,
}: {
  message: string;
  retry?: () => void;
}) {
  return (
    <div className="ds-error" role="alert">
      <AlertCircle size={20} aria-hidden="true" />
      <div>
        <p>{userMessage(message)}</p>
        {retry && <AppButton onClick={retry}>Thử lại</AppButton>}
      </div>
    </div>
  );
}
export function LoadingSkeleton({
  rows = 5,
  label = "Đang tải dữ liệu",
}: {
  rows?: number;
  label?: string;
}) {
  return (
    <div
      className="ds-skeleton"
      role="status"
      aria-label={label}
      aria-busy="true"
    >
      {Array.from({ length: rows }, (_, index) => (
        <span key={index} />
      ))}
    </div>
  );
}
export function DataTable({
  children,
  label,
  loading = false,
  error,
  retry,
  empty = false,
  minWidth = 760,
}: {
  children: ReactNode;
  label: string;
  loading?: boolean;
  error?: string;
  retry?: () => void;
  empty?: boolean;
  minWidth?: number;
}) {
  if (loading)
    return <LoadingSkeleton label={`Đang tải ${label.toLowerCase()}`} />;
  if (error) return <ErrorState message={error} retry={retry} />;
  if (empty)
    return (
      <EmptyState
        title="Không tìm thấy dữ liệu"
        description="Thử đổi điều kiện tìm kiếm hoặc xóa bộ lọc."
      />
    );
  return (
    <div
      className="ds-table-scroll"
      role="region"
      aria-label={label}
      tabIndex={0}
    >
      <table style={{ minWidth }}>{children}</table>
    </div>
  );
}
export function SortHeader({
  children,
  direction,
  onSort,
}: {
  children: ReactNode;
  direction?: "ascending" | "descending";
  onSort: () => void;
}) {
  const Icon =
    direction === "ascending"
      ? ArrowUp
      : direction === "descending"
        ? ArrowDown
        : ArrowUpDown;
  return (
    <th scope="col" aria-sort={direction ?? "none"}>
      <button type="button" className="ds-sort" onClick={onSort}>
        {children}
        <Icon size={14} aria-hidden="true" />
      </button>
    </th>
  );
}
export function Pagination({
  page,
  pageSize,
  total,
  onPage,
  onPageSize,
  sizes = [10, 20, 50, 100],
}: {
  page: number;
  pageSize: number;
  total: number;
  onPage: (page: number) => void;
  onPageSize?: (size: number) => void;
  sizes?: number[];
}) {
  const count = Math.max(1, Math.ceil(total / pageSize));
  const pages = [
    ...new Set([
      1,
      ...Array.from({ length: 3 }, (_, index) => page - 1 + index).filter(
        (value) => value > 1 && value < count,
      ),
      count,
    ]),
  ];
  return (
    <nav className="ds-pagination" aria-label="Phân trang">
      <span>
        Hiển thị {total ? (page - 1) * pageSize + 1 : 0}–
        {Math.min(page * pageSize, total)} / {total} bản ghi
      </span>
      {onPageSize && (
        <label>
          Số dòng{" "}
          <SearchableSelect
            aria-label="Số dòng mỗi trang"
            value={pageSize}
            onChange={(event) => onPageSize(Number(event.target.value))}
          >
            {sizes.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </SearchableSelect>
        </label>
      )}
      <div className="ds-page-buttons">
        <IconButton
          label="Trang trước"
          icon={ChevronLeft}
          disabled={page <= 1}
          onClick={() => onPage(page - 1)}
        />
        {pages.map((number, index) => (
          <span key={number}>
            {index > 0 && number - pages[index - 1] > 1 && (
              <span className="ds-page-gap">…</span>
            )}
            <button
              type="button"
              className="ds-page-number"
              aria-current={number === page ? "page" : undefined}
              onClick={() => number !== page && onPage(number)}
            >
              {number}
            </button>
          </span>
        ))}
        <IconButton
          label="Trang sau"
          icon={ChevronRight}
          disabled={page >= count}
          onClick={() => onPage(page + 1)}
        />
      </div>
    </nav>
  );
}

export type MenuItem = {
  label: string;
  icon?: LucideIcon;
  to?: string;
  onSelect?: () => void;
  danger?: boolean;
  disabled?: boolean;
};
export function ActionMenu({
  items,
  label = "Thao tác",
  icon: Icon = EllipsisVertical,
  trigger,
}: {
  items: MenuItem[];
  label?: string;
  icon?: LucideIcon;
  trigger?: ReactNode;
}) {
  const [position, setPosition] = useState<{
    top: number;
    left: number;
  } | null>(null);
  const button = useRef<HTMLButtonElement>(null);
  const menu = useRef<HTMLDivElement>(null);
  const id = useId();
  const close = () => {
    setPosition(null);
    button.current?.focus();
  };
  useEffect(() => {
    if (!position) return;
    const target = menu.current;
    if (target) {
      const rect = target.getBoundingClientRect();
      target.style.top = `${Math.max(8, Math.min(position.top, window.innerHeight - rect.height - 8))}px`;
      target.style.left = `${Math.max(8, Math.min(position.left, window.innerWidth - rect.width - 8))}px`;
      target
        .querySelector<HTMLElement>('[role="menuitem"]:not([disabled])')
        ?.focus();
    }
    const outside = (event: PointerEvent) => {
      if (
        !menu.current?.contains(event.target as Node) &&
        !button.current?.contains(event.target as Node)
      )
        setPosition(null);
    };
    const dismiss = () => setPosition(null);
    document.addEventListener("pointerdown", outside);
    window.addEventListener("resize", dismiss);
    window.addEventListener("scroll", dismiss, true);
    return () => {
      document.removeEventListener("pointerdown", outside);
      window.removeEventListener("resize", dismiss);
      window.removeEventListener("scroll", dismiss, true);
    };
  }, [position]);
  return (
    <>
      <button
        ref={button}
        type="button"
        className="ds-menu-trigger"
        aria-label={label}
        title={label}
        aria-haspopup="menu"
        aria-expanded={!!position}
        aria-controls={position ? id : undefined}
        onClick={() => {
          if (position) {
            close();
            return;
          }
          const rect = button.current!.getBoundingClientRect();
          setPosition({ top: rect.bottom + 8, left: rect.right - 220 });
        }}
      >
        <Icon size={18} aria-hidden="true" />
        {trigger}
      </button>
      {position &&
        createPortal(
          <div
            ref={menu}
            id={id}
            className="ds-menu"
            role="menu"
            aria-label={label}
            onKeyDown={(event) => {
              const items = [
                ...menu.current!.querySelectorAll<HTMLElement>(
                  '[role="menuitem"]:not([disabled])',
                ),
              ];
              const index = items.indexOf(
                document.activeElement as HTMLElement,
              );
              if (event.key === "Escape") {
                event.preventDefault();
                close();
              }
              if (event.key === "Tab") setPosition(null);
              if (["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)) {
                event.preventDefault();
                items[
                  event.key === "Home"
                    ? 0
                    : event.key === "End"
                      ? items.length - 1
                      : (index +
                          (event.key === "ArrowDown" ? 1 : -1) +
                          items.length) %
                        items.length
                ]?.focus();
              }
            }}
          >
            {items.map((item, index) => {
              const ItemIcon = item.icon;
              const body = (
                <>
                  {ItemIcon && <ItemIcon size={16} aria-hidden="true" />}
                  {item.label}
                </>
              );
              const select = () => {
                close();
                item.onSelect?.();
              };
              return item.to && !item.disabled ? (
                <Link
                  role="menuitem"
                  key={index}
                  className={item.danger ? "is-danger" : undefined}
                  to={item.to}
                  onClick={select}
                >
                  {body}
                </Link>
              ) : (
                <button
                  key={index}
                  type="button"
                  role="menuitem"
                  disabled={item.disabled}
                  className={item.danger ? "is-danger" : undefined}
                  onClick={select}
                >
                  {body}
                </button>
              );
            })}
          </div>,
          button.current?.closest("dialog") ?? document.body,
        )}
    </>
  );
}

let modalScrollLocks = 0;
let originalBodyOverflow = "";
export function AppModal({
  open,
  title,
  children,
  footer,
  onClose,
  busy = false,
  dirty = false,
  size = "medium",
  drawer = false,
}: {
  open: boolean;
  title: string;
  children: ReactNode;
  footer?: ReactNode;
  onClose: () => void;
  busy?: boolean;
  dirty?: boolean;
  size?: "small" | "medium" | "large" | "extra-large";
  drawer?: boolean;
}) {
  const dialog = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const close = () => {
    if (
      !busy &&
      (!dirty ||
        window.confirm(
          "Bạn có thay đổi chưa lưu. Đóng và bỏ các thay đổi này?",
        ))
    )
      onClose();
  };
  useEffect(() => {
    const target = dialog.current!;
    if (!open) {
      if (target.open) target.close();
      return;
    }
    const previous = document.activeElement as HTMLElement | null;
    if (modalScrollLocks === 0)
      originalBodyOverflow = document.body.style.overflow;
    modalScrollLocks++;
    document.body.style.overflow = "hidden";
    if (!target.open) target.showModal();
    return () => {
      if (target.open) target.close();
      modalScrollLocks = Math.max(0, modalScrollLocks - 1);
      if (modalScrollLocks === 0)
        document.body.style.overflow = originalBodyOverflow;
      if (previous?.isConnected) previous.focus();
    };
  }, [open]);
  return (
    <dialog
      ref={dialog}
      tabIndex={-1}
      aria-labelledby={titleId}
      aria-busy={busy || undefined}
      onKeyDown={(event) => {
        if (event.key !== "Tab") return;
        const items = [
          ...event.currentTarget.querySelectorAll<HTMLElement>(
            'button, a[href], input, select, textarea, [tabindex]:not([tabindex="-1"])',
          ),
        ].filter(
          (element) =>
            !element.matches(":disabled") &&
            element.getClientRects().length > 0 &&
            element.tabIndex >= 0,
        );
        if (!items.length) {
          event.preventDefault();
          event.currentTarget.focus();
          return;
        }
        const first = items[0],
          last = items[items.length - 1];
        if (
          event.shiftKey &&
          (document.activeElement === first ||
            document.activeElement === event.currentTarget)
        ) {
          event.preventDefault();
          last.focus();
        } else if (
          !event.shiftKey &&
          (document.activeElement === last ||
            !event.currentTarget.contains(document.activeElement))
        ) {
          event.preventDefault();
          first.focus();
        }
      }}
      className={`ds-dialog ds-dialog--${size}${drawer ? " ds-drawer" : ""}`}
      onCancel={(event) => {
        event.preventDefault();
        event.stopPropagation();
        close();
      }}
      onClick={(event) => {
        if (event.target === event.currentTarget) {
          const rect = event.currentTarget.getBoundingClientRect();
          if (
            event.clientX < rect.left ||
            event.clientX > rect.right ||
            event.clientY < rect.top ||
            event.clientY > rect.bottom
          )
            close();
        }
      }}
    >
      <div className="ds-dialog-layout">
        <header>
          <h2 id={titleId}>{title}</h2>
          <IconButton label="Đóng" icon={X} onClick={close} disabled={busy} />
        </header>
        <div className="ds-dialog-body">{open && children}</div>
        {footer && <footer>{footer}</footer>}
      </div>
    </dialog>
  );
}
export function AppDrawer({
  navigation = false,
  ...props
}: Omit<React.ComponentProps<typeof AppModal>, "drawer"> & {
  navigation?: boolean;
}) {
  return (
    <AppModal
      {...props}
      size={props.size ?? (navigation ? "medium" : "large")}
      drawer={navigation}
    />
  );
}
export function ConfirmDialog({
  open,
  title,
  description,
  onConfirm,
  onClose,
  busy = false,
  error,
}: {
  open: boolean;
  title: string;
  description: string;
  onConfirm: () => void;
  onClose: () => void;
  busy?: boolean;
  error?: string;
}) {
  return (
    <AppModal
      open={open}
      title={title}
      busy={busy}
      onClose={onClose}
      size="small"
      footer={
        <>
          <AppButton disabled={busy} onClick={onClose}>
            Hủy
          </AppButton>
          <AppButton variant="danger" loading={busy} onClick={onConfirm}>
            Xác nhận
          </AppButton>
        </>
      }
    >
      <p>{description}</p>
      {error && <ErrorState message={error} />}
    </AppModal>
  );
}
export function AppToast({
  message,
  tone = "success",
  onClose,
}: {
  message: string;
  tone?: StatusTone;
  onClose: () => void;
}) {
  const callback = useRef(onClose);
  callback.current = onClose;
  useEffect(() => {
    if (tone === "error") return;
    const timer = setTimeout(
      () => callback.current(),
      tone === "success" ? 5000 : 8000,
    );
    return () => clearTimeout(timer);
  }, [message, tone]);
  const Icon =
    tone === "success" ? CircleCheck : tone === "error" ? AlertCircle : Info;
  return createPortal(
    <div
      className={`ds-toast ds-badge--${tone}`}
      role={tone === "error" ? "alert" : "status"}
    >
      <Icon size={20} aria-hidden="true" />
      <p>{userMessage(message)}</p>
      <IconButton label="Đóng thông báo" icon={X} onClick={onClose} />
    </div>,
    document.body,
  );
}
