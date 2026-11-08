import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import type { UserProfileDto, UserProfilePatch } from "./userProfileContracts";
import {
  profileFailureMessage,
  unknownProfileMessage,
  type useUpdateUserProfile,
} from "./useUpdateUserProfile";

function ProfileForm({
  profile,
  disabled,
  save,
}: {
  profile: UserProfileDto;
  disabled: boolean;
  save(patch: UserProfilePatch): Promise<void>;
}) {
  const [draft, setDraft] = useState({
    fullName: profile.fullName,
    email: profile.email,
    phone: profile.phone ?? "",
  });
  const [validation, setValidation] = useState("");
  function submit(event: FormEvent) {
    event.preventDefault();
    if (disabled) return;
    const fullName = draft.fullName.trim(),
      email = draft.email.trim(),
      phone = draft.phone.trim() || null;
    if (
      !fullName ||
      fullName.length > 200 ||
      !email ||
      email.length > 150 ||
      (phone?.length ?? 0) > 20
    ) {
      setValidation(
        "Full name and email are required. Maximum lengths are 200, 150 and 20 for full name, email and phone.",
      );
      return;
    }
    setValidation("");
    const patch: UserProfilePatch = {};
    if (fullName !== profile.fullName) patch.fullName = fullName;
    if (email !== profile.email) patch.email = email;
    if (phone !== profile.phone) patch.phone = phone;
    // An explicit Save with no edits is a valid conditional no-op, not an effect-driven update.
    if (!Object.keys(patch).length) patch.fullName = fullName;
    void save(patch);
  }
  return (
    <form aria-label="Update user profile" onSubmit={submit} noValidate>
      <fieldset disabled={disabled}>
        <legend>Profile fields</legend>
        <label>
          Full name
          <input
            name="fullName"
            value={draft.fullName}
            maxLength={200}
            required
            autoComplete="off"
            onChange={(event) =>
              setDraft({ ...draft, fullName: event.target.value })
            }
          />
        </label>
        <label>
          Email
          <input
            name="email"
            value={draft.email}
            maxLength={150}
            required
            autoComplete="off"
            onChange={(event) =>
              setDraft({ ...draft, email: event.target.value })
            }
          />
        </label>
        <label>
          Phone
          <input
            name="phone"
            value={draft.phone}
            maxLength={20}
            autoComplete="off"
            onChange={(event) =>
              setDraft({ ...draft, phone: event.target.value })
            }
          />
        </label>
        <button type="submit">Save profile</button>
      </fieldset>
      {validation && <p role="alert">{validation}</p>}
    </form>
  );
}

export function UpdateUserPage({
  target,
  flow,
}: {
  target: number;
  flow: ReturnType<typeof useUpdateUserProfile>;
}) {
  const { snapshot, progress } = flow;
  const locked =
    progress.phase === "saving" ||
    progress.phase === "stale" ||
    progress.phase === "unknown";
  return (
    <main>
      <h1>Update user profile {target}</h1>
      <Link to="/profile">Profile</Link>
      <p>
        The server checks authorization for each request. Loading a profile does
        not grant permission to save changes.
      </p>
      {progress.phase === "unknown" && (
        <>
          <p role="alert">{unknownProfileMessage}</p>
          <p>
            A subsequent read only confirms the current profile. It does not
            prove that the earlier update committed. Saving remains disabled in
            this flow.
          </p>
        </>
      )}
      {progress.phase === "updated" && (
        <p role="status">Profile update confirmed by the server.</p>
      )}
      {progress.phase === "saving" && <p role="status">Saving profile...</p>}
      {progress.error && (
        <p role="alert">
          {progress.phase === "not-dispatched"
            ? "No profile update request was sent. "
            : ""}
          {profileFailureMessage(progress.error)}
        </p>
      )}
      {flow.error && <p role="alert">{profileFailureMessage(flow.error)}</p>}
      {flow.loading && <p role="status">Loading profile...</p>}
      {snapshot && (
        <section aria-label="Server profile snapshot">
          <p>Account ID: {snapshot.profile.id}</p>
          <p>
            Version: <code>{snapshot.etag}</code>
          </p>
          <ProfileForm
            key={snapshot.revision}
            profile={snapshot.profile}
            disabled={locked}
            save={flow.save}
          />
        </section>
      )}
      {!snapshot &&
        flow.paused &&
        !progress.error &&
        progress.phase !== "unknown" && (
          <p>
            Reload the profile to confirm access and load a current version.
          </p>
        )}
      <button
        type="button"
        disabled={flow.loading || progress.phase === "saving"}
        onClick={flow.reload}
      >
        Reload profile
      </button>
    </main>
  );
}
