import { AlertPopup } from "./alert-popup";
import { useState } from "react";
import { api, type Row } from "./api";
import { useAuth } from "./auth";
import { Dialog, State, useQuery } from "./ui";
import { accountMessage } from "./account-policy";

// The shell owns navigation. Render this dialog for every authenticated role.
export default function PersonalProfile({ close }: { close: () => void }) {
  const { refresh } = useAuth();
  const [revision, bump] = useState(0);
  const query = useQuery<Row>("auth/profile", revision);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");
  const [saved, setSaved] = useState(false);
  const [stale, setStale] = useState(false);
  async function submit(form: HTMLFormElement) {
    if (pending || stale || !query.data) return;
    const data = new FormData(form);
    setPending(true);
    setError("");
    setSaved(false);
    try {
      await api(
        "auth/profile",
        {
          rowVersion: query.data.rowVersion,
          fullName: String(data.get("fullName") || "").trim(),
          email: String(data.get("email") || "").trim() || null,
          phone: String(data.get("phone") || "").trim() || null,
        },
        "PUT",
      );
      setSaved(true);
      bump((value) => value + 1);
      try {
        await refresh();
      } catch {
        setError(
          "Đã lưu hồ sơ, nhưng chưa tải lại được thông tin phiên đăng nhập.",
        );
      }
    } catch (e) {
      setError(accountMessage(e));
      if ((e as { code?: string }).code === "STALE_ROW_VERSION") setStale(true);
    } finally {
      setPending(false);
    }
  }
  return (
    <Dialog
      title="Thông tin cá nhân"
      close={() => {
        if (!pending) close();
      }}
    >
      <State {...query} retry={() => bump((value) => value + 1)}>
        {query.data && (
          <form
            key={query.data.rowVersion}
            onSubmit={(event) => {
              event.preventDefault();
              void submit(event.currentTarget);
            }}
          >
            <p>
              <strong>{query.data.username}</strong> · Mã nhân viên:{" "}
              {query.data.employeeCode}
            </p>
            <label>
              Họ tên
              <input
                name="fullName"
                required
                maxLength={200}
                defaultValue={query.data.fullName}
                autoComplete="name"
              />
            </label>
            <label>
              Email
              <input
                name="email"
                type="email"
                maxLength={254}
                defaultValue={query.data.email || ""}
                autoComplete="email"
              />
            </label>
            <label>
              Số điện thoại
              <input
                name="phone"
                type="tel"
                maxLength={30}
                defaultValue={query.data.phone || ""}
                autoComplete="tel"
              />
            </label>
            <button className="primary" disabled={pending || stale}>
              {pending ? "Đang lưu…" : "Lưu thông tin"}
            </button>
          </form>
        )}
      </State>
      {saved && <AlertPopup tone="success">Đã lưu thông tin cá nhân.</AlertPopup>}
      {error && (
        <AlertPopup>
          {error}
        </AlertPopup>
      )}
      {stale && (
        <button
          disabled={pending}
          onClick={() => {
            setStale(false);
            setError("");
            bump((value) => value + 1);
          }}
        >
          Tải lại hồ sơ
        </button>
      )}
    </Dialog>
  );
}
