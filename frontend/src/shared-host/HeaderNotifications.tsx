import { userMessage } from "../i18n/userMessage";
import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Bell, ExternalLink } from "lucide-react";
import { useAuth } from "../auth/AuthProvider";
import {
  AppButton,
  EmptyState,
  ErrorState,
  LoadingSkeleton,
} from "../components/design-system";
import {
  InboxFailure,
  openInboxSource,
  readInbox,
  type Inbox,
} from "../notifications/inboxApi";
import { AppPopover } from "../components/AppPopover";
import { NotificationUnreadBadge } from "../notifications/NotificationUnreadBadge";

export function HeaderNotifications() {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" && state.pending === null
      ? state.authorityGeneration
      : null;
  const [openedFor, setOpenedFor] = useState<typeof owner>(null);
  const open = !!owner && openedFor === owner;
  const [retry, setRetry] = useState(0);
  const [result, setResult] = useState<{
    owner: typeof owner;
    inbox?: Inbox;
    error?: string;
  } | null>(null);
  const [busy, setBusy] = useState(false);
  const button = useRef<HTMLButtonElement>(null);
  const currentOwner = useRef(owner);
  currentOwner.current = owner;
  const sourceAbort = useRef<AbortController | null>(null);
  const navigate = useNavigate();
  useEffect(() => {
    const controller = new AbortController();
    if (!open || !owner) return;
    setResult(null);
    void readInbox("page=1&pageSize=5", controller.signal)
      .then((inbox) => {
        if (!controller.signal.aborted) setResult({ owner, inbox });
      })
      .catch((error) => {
        if (controller.signal.aborted) return;
        if (error instanceof InboxFailure && error.status === 401)
          actions.invalidateSession(owner);
        setResult({
          owner,
          error: "Chưa tải được thông báo. Vui lòng thử lại.",
        });
      });
    return () => controller.abort();
  }, [open, owner, retry, actions]);
  useEffect(() => () => sourceAbort.current?.abort(), [owner]);
  const visible = result?.owner === owner ? result : null;
  async function openSource(id: number) {
    if (!owner || sourceAbort.current) return;
    const controller = new AbortController();
    sourceAbort.current = controller;
    setBusy(true);
    try {
      const path = await openInboxSource(
        id,
        controller.signal,
        () => currentOwner.current === owner,
      );
      if (currentOwner.current === owner && !controller.signal.aborted) {
        setOpenedFor(null);
        navigate(path);
      }
    } catch (error) {
      if (!controller.signal.aborted && currentOwner.current === owner) {
        if (error instanceof InboxFailure && error.status === 401)
          actions.invalidateSession(owner);
        setResult({
          owner,
          inbox: visible?.inbox,
          error:
            "Chưa mở được bản ghi. Quyền truy cập hoặc dữ liệu nguồn có thể đã thay đổi.",
        });
      }
    } finally {
      sourceAbort.current = null;
      setBusy(false);
    }
  }
  return (
    <>
      <button
        type="button"
        ref={button}
        className="ds-notification-trigger"
        aria-expanded={open}
        aria-label="Mở thông báo"
        title="Thông báo"
        onClick={() => setOpenedFor(owner)}
      >
        <Bell size={20} aria-hidden="true" />
        <NotificationUnreadBadge />
      </button>
      <AppPopover
        anchor={button}
        open={open}
        title="Thông báo mới nhất"
        busy={busy}
        onClose={() => {
          sourceAbort.current?.abort();
          setOpenedFor(null);
        }}
        footer={
          <AppButton
            onClick={() => {
              setOpenedFor(null);
              navigate("/notifications");
            }}
          >
            Xem tất cả thông báo
          </AppButton>
        }
      >
        {!visible && <LoadingSkeleton rows={4} />}
        {visible?.error && (
          <ErrorState
            message={visible.error}
            retry={() => setRetry((value) => value + 1)}
          />
        )}
        {visible?.inbox &&
          (visible.inbox.items.length ? (
            <ul className="ds-inbox-preview">
              {visible.inbox.items.map((item) => (
                <li key={item.id} className={item.readAt ? "" : "is-unread"}>
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => void openSource(item.id)}
                  >
                    <strong>{item.title}</strong>
                    <span>{userMessage(item.message)}</span>
                    <time dateTime={item.createdAt}>
                      {new Intl.DateTimeFormat("vi-VN", {
                        dateStyle: "short",
                        timeStyle: "short",
                        timeZone: "Asia/Ho_Chi_Minh",
                      }).format(new Date(item.createdAt))}
                    </time>
                    <ExternalLink size={14} aria-hidden="true" />
                  </button>
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState
              icon={Bell}
              title="Chưa có thông báo"
              description="Thông báo mới sẽ xuất hiện tại đây."
            />
          ))}
      </AppPopover>
    </>
  );
}
