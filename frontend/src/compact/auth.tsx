import { AlertPopup } from "./alert-popup";
import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import { api, session, hasSession, message, type Row } from "./api";
import { useVietnameseValidation } from "./form-validation";
import { PasswordChecklist, accountMessage } from "./account-policy";
import { Eye, EyeOff, LockKeyhole, UserRound } from "lucide-react";
import ForgotPassword from "./forgot-password";
import "./login-refresh.css";
export type Profile = {
  user: { fields: Row };
  grants: { code: string; dataScope: string; branchId: number | null }[];
  branches: Row[];
  roles: Row[];
  memberships: Row[];
  requiresPasswordChange: boolean;
};
const Context = createContext<{
  profile: Profile;
  branch: number;
  setBranch: (id: number) => void;
  can: (code: string, own?: boolean) => boolean;
  canGlobal: (code: string) => boolean;
  refresh: () => Promise<void>;
  logout: () => Promise<void>;
}>(null!);
export const useAuth = () => useContext(Context);
export function Auth({ children }: { children: ReactNode }) {
  useVietnameseValidation();
  const [newPassword, setNewPassword] = useState("");
  const [currentPassword, setCurrentPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [forgotPassword, setForgotPassword] = useState(false);
  const [profile, setProfile] = useState<Profile | null>(null),
    [branch, setBranch] = useState(0),
    [loading, setLoading] = useState(true),
    [error, setError] = useState(""),
    [pending, setPending] = useState(false);
  async function refresh() {
    const p = await api<Profile>("auth/me");
    setProfile(p);
    setBranch((current) =>
      p.branches.some((b) => b.branchId === current)
        ? current
        : p.branches[0]?.branchId || 0,
    );
  }
  useEffect(() => {
    if (hasSession())
      refresh()
        .catch((e) => {
          session("");
          if (e.status !== 401) setError(message(e));
        })
        .finally(() => setLoading(false));
    else setLoading(false);
    const expired = () => {
      setProfile(null);
      setError("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
    };
    window.addEventListener("compact:expired", expired);
    return () => window.removeEventListener("compact:expired", expired);
  }, []);
  async function logout() {
    await api("auth/logout", {});
    session("");
    location.hash = "";
    setProfile(null);
  }
  if (loading)
    return (
      <div className="login-page">
        <p role="status">Đang kiểm tra phiên đăng nhập…</p>
      </div>
    );
  if (!profile || profile.requiresPasswordChange)
    return (
      <div className="login-page">
        <section className="login-card login-refreshed">
          <img
            className="brand-logo"
            src="/duc-vuong-logo-transparent.png"
            alt="Đức Vượng Group"
          />
            <p className="eyebrow">VẬN HÀNH KHÁCH SẠN</p>
          <h1>{profile ? "Đổi mật khẩu" : "Đăng nhập"}</h1>
          <p>
            {profile
              ? "Đặt mật khẩu mới để tiếp tục."
              : "Đăng nhập bằng tên đăng nhập và mật khẩu của bạn."}
          </p>
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              if (pending) return;
              setPending(true);
              setError("");
              const f = new FormData(e.currentTarget);
              try {
                if (profile) {
                  await api("auth/password", {
                    currentPassword: f.get("password"),
                    newPassword: f.get("newPassword"),
                  });
                  session("");
                  setProfile(null);
                  setError("Đã đổi mật khẩu. Vui lòng đăng nhập lại.");
                } else {
                  const r = await api("auth/login", {
                    username: f.get("username"),
                    password: f.get("password"),
                  });
                  session(r.accessToken);
                  await refresh();
                }
              } catch (e) {
                setError(accountMessage(e));
              } finally {
                setPending(false);
              }
            }}
          >
            {!profile && (
              <label>
                {/* Owner 25/11 14:11: username only; employee code must not be used. */}
                Tên đăng nhập
                <span className="login-input-wrap">
                <UserRound aria-hidden="true" size={20} />
                <input
                  name="username"
                  autoComplete="username"
                  required
                  maxLength={100}
                  placeholder="Nhập tên đăng nhập"
                />
                </span>
              </label>
            )}
            <label>
              {profile ? "Mật khẩu hiện tại" : "Mật khẩu"}
              <span className="login-input-wrap">
              <LockKeyhole aria-hidden="true" size={20} />
              <input
                id="login-password"
                name="password"
                onChange={(e) => setCurrentPassword(e.target.value)}
                type={showPassword ? "text" : "password"}
                placeholder="Nhập mật khẩu"
                autoComplete="current-password"
                required
              />
              <button type="button" className="login-password-toggle" aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"} aria-pressed={showPassword} aria-controls="login-password" onClick={() => setShowPassword(value => !value)}>
                {showPassword ? <EyeOff size={20} aria-hidden="true" /> : <Eye size={20} aria-hidden="true" />}
              </button>
              </span>
            </label>
            {!profile && <button type="button" className="login-forgot" onClick={() => setForgotPassword(true)}>Quên mật khẩu?</button>}
            {profile && (
              <label>
                Mật khẩu mới
                <input
                  name="newPassword"
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  type="password"
                  autoComplete="new-password"
                  minLength={8}
                  maxLength={128}
                  required
                />
              </label>
            )}
            {profile && (
              <PasswordChecklist
                password={newPassword}
                currentPassword={currentPassword}
              />
            )}
            {error && (
              <AlertPopup>
                {error}
              </AlertPopup>
            )}
            <button className="primary" disabled={pending}>
              {pending ? "Đang xử lý…" : profile ? "Lưu mật khẩu" : "Đăng nhập"}
            </button>
          </form>
          {forgotPassword && <ForgotPassword close={() => setForgotPassword(false)} />}
        </section>
      </div>
    );
  const can = (code: string, own = true) =>
    profile.grants.some(
      (g) =>
        g.code === code &&
        ((g.branchId === null && g.dataScope === "ALL") ||
          (g.branchId === branch &&
            (g.dataScope === "ALL" ||
              g.dataScope === "BRANCH" ||
              (own && g.dataScope === "OWN")))),
    );
  return (
    <Context.Provider
      value={{
        profile,
        branch,
        setBranch,
        can,
        logout,
        refresh,
        canGlobal: (code) =>
          profile.grants.some(
            (g) =>
              g.code === code && g.branchId === null && g.dataScope === "ALL",
          ),
      }}
    >
      {children}
    </Context.Provider>
  );
}
