import {
  forwardRef,
  useEffect,
  useImperativeHandle,
  useRef,
  useState,
} from "react";
import type { CreateUserResponse } from "./createUserContracts";
import { Check, Copy, Eye, EyeOff } from "lucide-react";
import { AppButton } from "../components/design-system";

export interface CredentialHandoffHandle {
  receive(data: CreateUserResponse): void;
}

/** The only React state containing the plaintext; no parent/store retains this response. */
export const InitialCredentialHandoff = forwardRef<CredentialHandoffHandle>(
  function InitialCredentialHandoff(_, ref) {
    const [credential, setCredential] = useState<CreateUserResponse | null>(
      null,
    );
    const [revealed, setRevealed] = useState(false);
    const [notice, setNotice] = useState("");
    const [acknowledged, setAcknowledged] = useState(false);
    const active = useRef(false);
    const revision = useRef(0);
    useEffect(() => {
      active.current = true;
      return () => {
        active.current = false;
        ++revision.current;
      };
    }, []);
    useImperativeHandle(
      ref,
      () => ({
        receive(data) {
          ++revision.current;
          setCredential(data);
          setRevealed(false);
          setNotice("");
          setAcknowledged(false);
        },
      }),
      [],
    );

    async function copy() {
      if (!credential) return;
      const current = revision.current;
      try {
        await navigator.clipboard.writeText(
          credential.temporaryCredential.password,
        );
        if (active.current && current === revision.current)
          setNotice("Đã sao chép. Chưa xác nhận đã bàn giao cho nhân viên.");
      } catch {
        if (active.current && current === revision.current)
          setNotice(
            "Không sao chép được. Hiện mật khẩu và bàn giao an toàn bằng cách khác.",
          );
      }
    }
    if (acknowledged)
      return (
        <section className="user-credential" aria-label="Đã bàn giao mật khẩu">
          <h2>Đã bàn giao mật khẩu</h2>
          <p>
            Mật khẩu đã xóa khỏi trang. Nếu bị mất, dùng quy trình đặt lại được
            cấp quyền; không tạo lại tài khoản.
          </p>
        </section>
      );
    if (!credential) return null;
    return (
      <section
        className="user-credential"
        aria-label="Bàn giao mật khẩu ban đầu"
      >
        <h2>Đã tạo tài khoản — mật khẩu ban đầu</h2>
        <p>Nhân viên: {credential.user.fullName}</p>
        <p>Tên đăng nhập: {credential.user.username}</p>
        <p>Mã nhân viên: {credential.user.employeeCode}</p>
        <p>
          Hết hạn lúc:{" "}
          <time dateTime={credential.temporaryCredential.expiresAtUtc}>
            {new Intl.DateTimeFormat("vi-VN", {
              dateStyle: "short",
              timeStyle: "short",
              timeZone: "Asia/Ho_Chi_Minh",
            }).format(
              new Date(credential.temporaryCredential.expiresAtUtc),
            )}{" "}
            (giờ Việt Nam)
          </time>
        </p>
        {revealed ? (
          <p aria-label="Mật khẩu ban đầu">
            {credential.temporaryCredential.password}
          </p>
        ) : (
          <p>Đã ẩn mật khẩu</p>
        )}
        <AppButton
          icon={revealed ? EyeOff : Eye}
          onClick={() => setRevealed((value) => !value)}
        >
          {revealed ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
        </AppButton>
        <AppButton icon={Copy} onClick={() => void copy()}>
          Sao chép mật khẩu
        </AppButton>
        {notice && <p role="status">{notice}</p>}
        <p>
          Bộ nhớ sao chép có thể giữ mật khẩu sau khi rời trang. Rời hoặc đóng
          trang sẽ mất quyền xem mật khẩu này; không lấy lại được tại đây.
        </p>
        <p>
          Nếu mất mật khẩu, nhờ quản trị viên có quyền đặt lại. Không tạo lại
          tài khoản.
        </p>
        <AppButton
          icon={Check}
          onClick={() => {
            ++revision.current;
            setCredential(null);
            setRevealed(false);
            setNotice("");
            setAcknowledged(true);
          }}
        >
          Tôi đã bàn giao mật khẩu an toàn
        </AppButton>
      </section>
    );
  },
);
