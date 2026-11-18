import { RoleCatalogRoute } from "../roles/RoleCatalogRoute";
import { CredentialResetRoute } from "../users/CredentialResetRoute";
import { useRef } from "react";
import { Link, Navigate, Route, Routes } from "react-router-dom";
import ForgotPasswordScreen from "../screens/auth/ForgotPasswordScreen";
import SignInScreen from "../screens/auth/SignInScreen";
import ProfileScreen from "../screens/auth/ProfileScreen";
import ChangePasswordScreen from "../screens/auth/ChangePasswordScreen";
import { useAuth } from "./AuthProvider";
import { UsersRouteBoundary } from "../users/UsersRouteBoundary";
import { UserDirectoryPage } from "../users/UserDirectoryPage";
import { StandaloneUserAccessReviewPage as UserAccessReviewPage } from "../users/StandaloneUserAccessReviewPage";
import { CreateUserFlowBoundary } from "../users/CreateUserFlowBoundary";
import { UpdateUserFlowBoundary } from "../users/UpdateUserFlowBoundary";
import { ActivateUserFlowBoundary } from "../users/ActivateUserFlowBoundary";
import { DeactivateUserFlowBoundary } from "../users/DeactivateUserFlowBoundary";
import { AddUserBranchFlowBoundary } from "../users/AddUserBranchFlowBoundary";
import { AssignUserRoleFlowBoundary } from "../users/AssignUserRoleFlowBoundary";
import { RevokeUserRoleFlowBoundary } from "../users/RevokeUserRoleFlowBoundary";

function Recovery() {
  const { state, actions } = useAuth();
  if (state.status === "INITIALIZING")
    return <p role="status">Đang kiểm tra phiên đăng nhập của bạn...</p>;
  return (
    <section aria-label="Khôi phục phiên đăng nhập">
      <p role="alert">
        Chưa xác minh được phiên và kết quả thao tác trước. Không tự gửi lại yêu cầu.
      </p>
      <button type="button" onClick={() => void actions.revalidate()}>
        Kiểm tra phiên đăng nhập
      </button>
      <Link to="/forgot-password">Liên hệ quản trị viên</Link>
    </section>
  );
}

type FormKind = "login" | "profile" | "changePassword";

function AuthFormRoute({ kind }: { kind: FormKind }) {
  const { state } = useAuth();
  // This admission belongs only to this mounted route, never to another URL.
  const admitted = useRef(false);
  const confirmed =
    state.session !== null &&
    (state.status === "AUTHENTICATED" || state.status === "RESTRICTED");
  const anonymous =
    state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID";
  const allowed = kind === "login" ? anonymous : confirmed;
  if (allowed) admitted.current = true;

  const operation = kind === "profile" ? "logout" : kind;
  const retain =
    admitted.current &&
    (state.pending === operation || state.status === "ERROR_UNAVAILABLE");
  const unknown =
    state.status === "INITIALIZING" || state.status === "ERROR_UNAVAILABLE";

  if (kind === "login" && confirmed)
    return (
      <Navigate
        replace
        to={state.status === "RESTRICTED" ? "/change-password" : "/dashboard"}
      />
    );
  if (kind !== "login" && anonymous) return <Navigate replace to="/login" />;

  const Component =
    kind === "login"
      ? SignInScreen
      : kind === "profile"
        ? ProfileScreen
        : ChangePasswordScreen;
  return (
    <>
      <div>{(allowed || retain) && <Component />}</div>
      {unknown && <Recovery />}
    </>
  );
}

function BusinessBoundary() {
  const { state } = useAuth();
  if (state.status === "INITIALIZING" || state.status === "ERROR_UNAVAILABLE")
    return <Recovery />;
  if (state.status === "RESTRICTED")
    return <Navigate replace to="/change-password" />;
  if (state.status !== "AUTHENTICATED" || state.session === null)
    return <Navigate replace to="/login" />;
  // No prototype imports, mock identity, business data or permission inference.
  return (
    <main>
      <h1>Không gian làm việc HotelOps</h1>
      <p>Màn nghiệp vụ này chưa được tích hợp.</p>
      <Link to="/roles">Danh mục vai trò</Link>
      <Link to="/profile">Hồ sơ cá nhân</Link>
      <Link to="/change-password">Đổi mật khẩu</Link>
    </main>
  );
}

function PrototypeUnavailable() {
  return (
    <main>
      <h1>Bản thử nghiệm chưa khả dụng</h1>
      <p>Màn hình thử nghiệm không khả dụng khi dùng xác thực thực tế.</p>
      <Link to="/login">Quay lại đăng nhập</Link>
    </main>
  );
}

/** Safe in both provider branches; authenticated is not a business permission grant. */
export function StandaloneAuthRouteHost() {
  return (
    <Routes>
      <Route
        path="/login"
        element={<AuthFormRoute key="login" kind="login" />}
      />
      <Route
        path="/profile"
        element={<AuthFormRoute key="profile" kind="profile" />}
      />
      <Route
        path="/change-password"
        element={<AuthFormRoute key="changePassword" kind="changePassword" />}
      />
      <Route path="/forgot-password" element={<ForgotPasswordScreen />} />
      <Route path="/roles" element={<RoleCatalogRoute mode="list" />} />
      <Route
        path="/roles/:roleId"
        element={<RoleCatalogRoute mode="detail" />}
      />
      <Route
        path="/roles/:roleId/permissions"
        element={<RoleCatalogRoute mode="matrix" />}
      />
      <Route
        path="/permissions"
        element={<RoleCatalogRoute mode="catalog" />}
      />
      <Route path="/dev/*" element={<PrototypeUnavailable />} />
      <Route path="/users/new" element={<CreateUserFlowBoundary />} />
      <Route path="/users/:id/edit" element={<UpdateUserFlowBoundary />} />
      <Route
        path="/users/:id/reset-credential"
        element={<CredentialResetRoute />}
      />
      <Route
        path="/users/:id/activate"
        element={<ActivateUserFlowBoundary />}
      />
      <Route path="/users/:id/deactivate" element={<DeactivateUserFlowBoundary />} />
      <Route
        path="/users/:id/branches/add"
        element={<AddUserBranchFlowBoundary />}
      />
      <Route
        path="/users/:id/roles/assign"
        element={<AssignUserRoleFlowBoundary />}
      />
      <Route path="/users/:id/roles/:assignmentId/revoke" element={<RevokeUserRoleFlowBoundary />} />
      <Route element={<UsersRouteBoundary />}>
        <Route path="/users" element={<UserDirectoryPage />} />
        <Route path="/users/:id" element={<UserAccessReviewPage />} />
      </Route>
      <Route path="*" element={<BusinessBoundary />} />
    </Routes>
  );
}
