import { Link } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import { AuthLayout } from "../../layouts/AuthLayout";
import { Form, notify } from "../../ui";
export default function ForgotPasswordScreen() {
  useLanguage();
  return (
    <AuthLayout title={t("Forgot Password")}>
      <p>
        {t(
          "Contact your Administrator to reset your password. For your privacy, this instruction is the same for every account.",
        )}
      </p>
      <Form
        fields={[
          { key: "username", label: "Username or email", required: true },
        ]}
        label="Show instructions"
        onSubmit={() => {
          notify("Please contact the Administrator to reset your password.");
        }}
      />
      <Link to="/login">{t("Back to sign in")}</Link>
    </AuthLayout>
  );
}
