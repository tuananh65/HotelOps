import {
  forwardRef,
  useEffect,
  useImperativeHandle,
  useRef,
  useState,
} from "react";
import type { CreateUserResponse } from "./createUserContracts";

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
          setNotice(
            "Copied to clipboard. This does not confirm delivery to the employee.",
          );
      } catch {
        if (active.current && current === revision.current)
          setNotice(
            "Clipboard copy is unavailable. Reveal the credential and hand it off securely by another method.",
          );
      }
    }
    if (acknowledged)
      return (
        <section aria-label="Credential handoff complete">
          <h2>Credential handoff complete</h2>
          <p>
            The credential has been removed from this page. If it is lost, use
            an authorized credential reset process; do not create the account
            again.
          </p>
        </section>
      );
    if (!credential) return null;
    return (
      <section aria-label="Initial credential handoff">
        <h2>Account created — initial credential</h2>
        <p>Employee: {credential.user.fullName}</p>
        <p>Username: {credential.user.username}</p>
        <p>Employee code: {credential.user.employeeCode}</p>
        <p>Account ID: {credential.user.id}</p>
        <p>
          Expires at:{" "}
          <time dateTime={credential.temporaryCredential.expiresAtUtc}>
            {credential.temporaryCredential.expiresAtUtc}
          </time>
        </p>
        {revealed ? (
          <p aria-label="Initial password">
            {credential.temporaryCredential.password}
          </p>
        ) : (
          <p>Password hidden</p>
        )}
        <button type="button" onClick={() => setRevealed((value) => !value)}>
          {revealed ? "Hide password" : "Reveal password"}
        </button>
        <button type="button" onClick={() => void copy()}>
          Copy password
        </button>
        {notice && <p role="status">{notice}</p>}
        <p>
          The clipboard may retain the password after you leave this page.
          Leaving or closing this page removes access to this credential. It
          cannot be retrieved again here.
        </p>
        <p>
          If the credential is lost, ask an authorized administrator to arrange
          a credential reset. Do not create the account again.
        </p>
        <button
          type="button"
          onClick={() => {
            ++revision.current;
            setCredential(null);
            setRevealed(false);
            setNotice("");
            setAcknowledged(true);
          }}
        >
          I have securely handed off the credential
        </button>
      </section>
    );
  },
);
