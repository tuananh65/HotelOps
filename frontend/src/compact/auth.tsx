import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import { api, session, hasSession, message, type Row } from "./api";
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
        <section className="login-card">
          <img className="brand-logo" src="/duc-vuong-logo.jpg" alt="Đức Vượng Group" />
          <p className="eyebrow">HOTEL OPERATIONS</p>
          <h1>{profile ? "Đổi mật khẩu" : "Chào mừng trở lại"}</h1>
          <p>
            {profile
              ? "Đặt mật khẩu mới để tiếp tục."
              : "Đăng nhập để bắt đầu ngày làm việc."}
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
                setError(message(e));
              } finally {
                setPending(false);
              }
            }}
          >
            {!profile && (
              <label>
                Tên đăng nhập
                <input
                  name="username"
                  autoComplete="username"
                  required
                  maxLength={100}
                />
              </label>
            )}
            <label>
              {profile ? "Mật khẩu hiện tại" : "Mật khẩu"}
              <input
                name="password"
                type="password"
                autoComplete="current-password"
                required
              />
            </label>
            {profile && (
              <label>
                Mật khẩu mới
                <input
                  name="newPassword"
                  type="password"
                  autoComplete="new-password"
                  minLength={12}
                  maxLength={128}
                  required
                />
              </label>
            )}
            {error && (
              <p role="alert" className="alert">
                {error}
              </p>
            )}
            <button className="primary" disabled={pending}>
              {pending ? "Đang xử lý…" : profile ? "Lưu mật khẩu" : "Đăng nhập"}
            </button>
          </form>
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
