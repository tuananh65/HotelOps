import { Link, useNavigate } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import { AuthLayout } from "../../layouts/AuthLayout";
import { login } from "../../services";
import { Form } from "../../ui";
export default function SignInScreen() {
  useLanguage();
  const navigate = useNavigate();
  return (
    <AuthLayout title={t("Sign In")}>
      <p>{t("Welcome back. Sign in to your hotel workspace.")}</p>
      <Form
        fields={[
          { key: "username", label: "Username or email", required: true },
          {
            key: "password",
            label: "Password",
            type: "password",
            required: true,
          },
        ]}
        initial={{ username: "demo.viewer", password: "DemoOnly@2026" }}
        label="Sign in"
        onSubmit={(values) => {
          const user = login(values.username, values.password);
          navigate("/dashboard");
        }}
      />
      <Link className="forgot" to="/forgot-password">
        {t("Forgot password?")}
      </Link>
      <details className="demo">
        <summary>{t("Local demo accounts")}</summary>
        <p>
          {t("Password:")}
          <code>{t("HotelOps@2026")}</code>
        </p>
        <p>
          {t(
            "admin, manager, owner, hksupervisor, housekeeper, maintenancelead, technician, storekeeper, head",
          )}
        </p>
        <small>
          {t(
            "This prototype stores demo data in this browser. It is not connected to an authentication server.",
          )}
        </small>
      </details>
    </AuthLayout>
  );
}
