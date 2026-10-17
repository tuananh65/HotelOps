import { useNavigate } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import { AuthLayout } from "../../layouts/AuthLayout";
import { changePassword } from "../../services";
import { Form, notify } from "../../ui";
export default function ChangePasswordScreen() {
  useLanguage();
  const navigate = useNavigate();
  return (
    <AuthLayout title={t("Change Password")}>
      <p>
        {t(
          "Use 8+ characters with uppercase, lowercase, number and special character.",
        )}
      </p>
      <Form
        fields={[
          {
            key: "current",
            label: "Current password",
            type: "password",
            required: true,
          },
          {
            key: "next",
            label: "New password",
            type: "password",
            required: true,
          },
          {
            key: "confirm",
            label: "Confirm new password",
            type: "password",
            required: true,
          },
        ]}
        label="Change password"
        onSubmit={async (values) => {
          await changePassword(values.current, values.next, values.confirm);
          notify("Password changed.");
          navigate("/profile");
        }}
      />
    </AuthLayout>
  );
}
