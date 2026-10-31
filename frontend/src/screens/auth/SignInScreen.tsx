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
        initial={{ username: "", password: "" }}
        label="Sign in"
        onSubmit={async (values) => {
          const user = await login(values.username, values.password);
          navigate("/profile");
        }}
      />
      <Link className="forgot" to="/forgot-password">
        {t("Forgot password?")}
      </Link>
    </AuthLayout>
  );
}
