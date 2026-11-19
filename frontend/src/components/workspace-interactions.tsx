import { userMessage } from "../i18n/userMessage";
import {
  Children,
  Fragment,
  cloneElement,
  isValidElement,
  useEffect,
  useId,
  useRef,
  useState,
  type ReactNode,
  type ReactElement,
} from "react";
import { useLocation } from "react-router-dom";
import { ChevronDown, ChevronUp, Filter } from "lucide-react";
import { AppButton, AppModal, AppDrawer, AppToast } from "./design-system";

const successEvent = "hotelops:confirmed-workflow-success";
export function announceWorkflowSuccess(message: string, scope: string) {
  window.dispatchEvent(
    new CustomEvent(successEvent, { detail: { message, scope } }),
  );
}
export function WorkflowFeedbackHost() {
  const [message, setMessage] = useState("");
  useEffect(() => {
    const receive = (event: Event) =>
      setMessage((event as CustomEvent<{ message: string }>).detail.message);
    window.addEventListener(successEvent, receive);
    return () => window.removeEventListener(successEvent, receive);
  }, []);
  return message ? (
    <AppToast tone="success" message={message} onClose={() => setMessage("")} />
  ) : null;
}

export function restoredFilterDraft<T extends Record<string, string>>(
  shape: T,
  applied: object | string,
  defaults: Partial<T> = {},
): T {
  const values =
    typeof applied === "string"
      ? new Map(new URLSearchParams(applied))
      : new Map(Object.entries(applied));
  return Object.fromEntries(
    Object.keys(shape).map((key) => [
      key,
      String(values.get(key) ?? defaults[key] ?? ""),
    ]),
  ) as T;
}

export function FilterToggleButton({
  open,
  count = 0,
  controls,
  onClick,
}: {
  open: boolean;
  count?: number;
  controls: string;
  onClick: () => void;
}) {
  return (
    <AppButton
      className="ds-filter-toggle"
      icon={Filter}
      aria-expanded={open}
      aria-controls={controls}
      onClick={onClick}
    >
      {open ? "Ẩn bộ lọc" : "Bộ lọc"}
      {count > 0 ? ` (${count})` : ""}
      {open ? (
        <ChevronUp size={16} aria-hidden="true" />
      ) : (
        <ChevronDown size={16} aria-hidden="true" />
      )}
    </AppButton>
  );
}

export function DeferredFilterPanel({
  children,
  activeCount = 0,
  onOpen,
  onReset,
  onApply,
  disabled = false,
}: {
  children: ReactNode;
  activeCount?: number;
  onOpen?: () => void;
  onReset: () => void;
  onApply: () => void;
  disabled?: boolean;
}) {
  return (
    <CollapsibleFilterPanel
      activeCount={activeCount}
      onOpen={onOpen}
      onClose={onOpen}
    >
      <form
        className="ds-filter-grid"
        onSubmit={(e) => {
          e.preventDefault();
          if (!disabled) onApply();
        }}
      >
        {children}
        <div className="ds-filter-actions">
          <AppButton disabled={disabled} onClick={onReset}>
            Xóa bộ lọc
          </AppButton>
          <AppButton disabled={disabled} type="submit" variant="primary">
            Áp dụng bộ lọc
          </AppButton>
        </div>
      </form>
    </CollapsibleFilterPanel>
  );
}

/** Visibility is deliberately independent of the caller's query and pagination.
 * Keep controls mounted: hiding cannot reset native inputs or restart read effects.
 * Auto-apply screens retain their existing semantics; deferred forms provide onClose.
 */
export function CollapsibleFilterPanel({
  children,
  activeCount = 0,
  summary,
  onOpen,
  onClose,
  resetNative = false,
}: {
  children: ReactNode;
  activeCount?: number;
  summary?: ReactNode;
  onOpen?: () => void;
  onClose?: () => void;
  resetNative?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const panel = useRef<HTMLDivElement>(null);
  const id = useId();
  const { pathname } = useLocation();
  useEffect(() => {
    setOpen(false);
  }, [pathname]);
  return (
    <section className="ds-collapsible-filters">
      <div className="ds-filter-toolbar">
        <FilterToggleButton
          open={open}
          count={activeCount}
          controls={id}
          onClick={() => {
            if (open) {
              // Native defaultValue controls must discard unapplied edits too.
              if (resetNative)
                panel.current
                  ?.querySelectorAll<
                    HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement
                  >("input, select, textarea")
                  .forEach((control) => {
                    if (control.disabled) return;
                    if (control instanceof HTMLSelectElement) {
                      const defaults = [...control.options].filter(
                        (option) => option.defaultSelected,
                      );
                      control.value =
                        defaults[0]?.value ?? control.options[0]?.value ?? "";
                    } else if (
                      control instanceof HTMLInputElement &&
                      ["checkbox", "radio"].includes(control.type)
                    )
                      control.checked = control.defaultChecked;
                    else control.value = control.defaultValue;
                  });
              onClose?.();
            } else onOpen?.();
            setOpen(!open);
          }}
        />
        {summary && <span className="ds-filter-summary">{summary}</span>}
      </div>
      <div ref={panel} id={id} hidden={!open} className="ds-filter-panel">
        {children}
      </div>
    </section>
  );
}

/** Presentation only: mutation handlers, validation, uncertain outcomes and
 * refreshes continue to belong to the existing workflow. Never infer success
 * from HTTP interception or a generic submit event.
 */
export function ActionDialog({
  title,
  children,
  open: controlled,
  onClose,
  busy = false,
  scope,
  locked = false,
  drawer = false,
  trigger,
  error,
  successKey,
}: {
  title: string;
  children: ReactNode;
  open?: boolean;
  onClose?: () => void;
  busy?: boolean;
  locked?: boolean;
  drawer?: boolean;
  trigger?: string;
  error?: ReactNode;
  successKey?: unknown;
  scope?: string;
}) {
  const [localOpen, setLocalOpen] = useState(false);
  const formId = useId();
  const [dirty, setDirty] = useState(false);
  const open = controlled ?? localOpen;
  const previousSuccess = useRef(successKey);
  const closed = useRef(onClose);
  closed.current = onClose;
  useEffect(() => {
    if (!open || !scope) return;
    const receive = (event: Event) => {
      if ((event as CustomEvent<{ scope: string }>).detail.scope !== scope)
        return;
      setDirty(false);
      setLocalOpen(false);
      closed.current?.();
    };
    window.addEventListener(successEvent, receive);
    return () => window.removeEventListener(successEvent, receive);
  }, [open, scope]);
  useEffect(() => {
    if (previousSuccess.current !== successKey) {
      previousSuccess.current = successKey;
      setLocalOpen(false);
      setDirty(false);
    }
  }, [successKey]);
  const close = () => {
    if (busy) return;
    setLocalOpen(false);
    setDirty(false);
    onClose?.();
  };
  const Dialog = drawer ? AppDrawer : AppModal;
  const footerActions: ReactNode[] = [];
  let formIndex = 0;
  function content(
    nodes: ReactNode,
    ownerForm?: string,
    parentDisabled = false,
  ): ReactNode {
    return Children.map(nodes, (node) => {
      if (!isValidElement(node)) return node;
      const element = node as ReactElement<Record<string, unknown>>;
      // Nested workflows own their form IDs, actions and validation. Never
      // extract a child's Save button into the parent drawer's footer.
      if (typeof element.type !== "string" && element.type !== Fragment)
        return node;
      const props = element.props;
      if (
        ["h2", "h3"].includes(String(element.type)) &&
        props.children === title
      )
        return null;
      const currentForm =
        element.type === "form"
          ? String(props.id ?? `${formId}-${formIndex++}`)
          : ownerForm;
      const disabled =
        parentDisabled ||
        (element.type === "fieldset" && props.disabled === true);
      if (element.type === "button" && props["data-dialog-action"] === true) {
        footerActions.push(
          cloneElement(element, {
            key: `action-${footerActions.length}`,
            ...(currentForm ? { form: currentForm } : {}),
            disabled: busy || locked || disabled || props.disabled === true,
            className: `ds-button ds-button--primary ${props.className ?? ""}`,
          }),
        );
        return null;
      }
      if (props.children === undefined) return node;
      return cloneElement(element, {
        ...(element.type === "form" ? { id: currentForm } : {}),
        children: content(props.children as ReactNode, currentForm, disabled),
      });
    });
  }
  const body = content(children);
  return (
    <>
      {controlled === undefined && (
        <AppButton
          onClick={() => {
            setDirty(false);
            setLocalOpen(true);
          }}
        >
          {trigger ?? title}
        </AppButton>
      )}
      <Dialog
        open={open}
        title={title}
        onClose={close}
        busy={busy}
        dirty={dirty}
        size={drawer ? "extra-large" : "large"}
        footer={
          <>
            <AppButton
              disabled={busy}
              onClick={() => {
                if (
                  !dirty ||
                  window.confirm(
                    "Bạn có thay đổi chưa lưu. Đóng và bỏ các thay đổi này?",
                  )
                )
                  close();
              }}
            >
              Hủy
            </AppButton>
            {footerActions}
          </>
        }
      >
        <div
          className="ds-action-workflow"
          onChangeCapture={() => setDirty(true)}
        >
          {error && <div role="status">{userMessage(error)}</div>}
          {body}
        </div>
        {locked && (
          <p role="alert">
            Kết quả thao tác chưa được xác nhận. Kiểm tra lại trạng thái trước
            khi tiếp tục.
          </p>
        )}
      </Dialog>
    </>
  );
}

export function OperationSurface({
  read,
  quick,
  title,
  open,
  busy,
  error,
  onClose,
  onReopen,
  children,
  long = false,
  activeCount = 0,
}: {
  read: boolean;
  quick: boolean;
  title: string;
  open: boolean;
  busy: boolean;
  error?: ReactNode;
  onClose: () => void;
  onReopen: () => void;
  children: ReactNode;
  long?: boolean;
  activeCount?: number;
}) {
  if (read)
    return quick ? (
      <>{children}</>
    ) : (
      <CollapsibleFilterPanel activeCount={activeCount}>
        {children}
      </CollapsibleFilterPanel>
    );
  return (
    <>
      <AppButton onClick={onReopen}>{title}</AppButton>
      <ActionDialog
        open={open}
        title={title}
        onClose={onClose}
        busy={busy}
        error={error}
        drawer={long}
      >
        {children}
      </ActionDialog>
    </>
  );
}
