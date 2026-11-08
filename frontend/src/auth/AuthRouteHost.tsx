import { useRef } from "react";
import { Link, Navigate, Route, Routes } from "react-router-dom";
import ForgotPasswordScreen from "../screens/auth/ForgotPasswordScreen";
import SignInScreen from "../screens/auth/SignInScreen";
import ProfileScreen from "../screens/auth/ProfileScreen";
import ChangePasswordScreen from "../screens/auth/ChangePasswordScreen";
import { useAuth } from "./AuthProvider";
import { UsersRouteBoundary } from "../users/UsersRouteBoundary";
import { UserDirectoryPage } from "../users/UserDirectoryPage";
import { UserAccessReviewPage } from "../users/UserAccessReviewPage";
import { CreateUserFlowBoundary } from "../users/CreateUserFlowBoundary";
import { UpdateUserFlowBoundary } from "../users/UpdateUserFlowBoundary";
import { ActivateUserFlowBoundary } from "../users/ActivateUserFlowBoundary";
import { AddUserBranchFlowBoundary } from "../users/AddUserBranchFlowBoundary";
import { AssignUserRoleFlowBoundary } from "../users/AssignUserRoleFlowBoundary";

function Recovery() {
  const { state, actions } = useAuth();
  if (state.status === "INITIALIZING")
    return <p role="status">Checking your session...</p>;
  return (
    <section aria-label="Session recovery">
      <p role="alert">
        Your session could not be confirmed. The previous operation's outcome is
        not confirmed. No request has been retried automatically.
      </p>
      <button type="button" onClick={() => void actions.revalidate()}>
        Check session
      </button>
      <Link to="/forgot-password">Contact Administrator</Link>
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
      <h1>HotelOps workspace</h1>
      <p>This business screen is not integrated yet.</p>
      <Link to="/profile">Profile</Link>
      <Link to="/change-password">Change password</Link>
    </main>
  );
}

function PrototypeUnavailable() {
  return (
    <main>
      <h1>Prototype unavailable</h1>
      <p>Prototype screens are unavailable in real-auth mode.</p>
      <Link to="/login">Return to sign in</Link>
    </main>
  );
}

/** Safe in both provider branches; authenticated is not a business permission grant. */
export function AuthRouteHost() {
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
      <Route path="/dev/*" element={<PrototypeUnavailable />} />
      <Route path="/users/new" element={<CreateUserFlowBoundary />} />
      <Route path="/users/:id/edit" element={<UpdateUserFlowBoundary />} />
      <Route
        path="/users/:id/activate"
        element={<ActivateUserFlowBoundary />}
      />
      <Route
        path="/users/:id/branches/add"
        element={<AddUserBranchFlowBoundary />}
      />
      <Route
        path="/users/:id/roles/assign"
        element={<AssignUserRoleFlowBoundary />}
      />
      <Route element={<UsersRouteBoundary />}>
        <Route path="/users" element={<UserDirectoryPage />} />
        <Route path="/users/:id" element={<UserAccessReviewPage />} />
      </Route>
      <Route path="*" element={<BusinessBoundary />} />
    </Routes>
  );
}
