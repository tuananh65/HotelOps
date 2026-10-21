# CurrentUserAccessor verification

Run from the repository root:

```powershell
dotnet run --project scripts/verification/CurrentUserAccessor/CurrentUserAccessor.Verification.csproj
```

This standalone console verification uses the existing API project and ASP.NET Core
without introducing a test framework or additional package references. A failed
assertion exits with a nonzero code. It is not discovered by `dotnet test HotelOps.slnx`.

Checks cover absent context, unauthenticated identity, missing/duplicate claims,
missing/incorrect snapshot, mismatched user/session/restriction/creation time,
valid normal and restricted identities, no caching across requests, and DI resolution.
Tickets use the existing public cookie ticket factory. Snapshots are supplied by
the harness; SQL, real cookie authentication, and the HTTP pipeline are not exercised.
