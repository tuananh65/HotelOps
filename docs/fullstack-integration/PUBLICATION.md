# Publication source pin

Repository: https://github.com/tuananh65/HotelOps
Branch: integration/fullstack-base
Status: WIP/PARTIAL, not FULLSTACK READY.

Exact tested integration source commit: a02cb2f27e9f7944b4e7f8b03da9f6f22bf3808f
Source tree: d89db530b7c1bf313d984ddbb9b7eaa7b705c13e
Frontend tree: 577b17911c4ebf96483efb174d86ff0c22c1209f

The following documentation-only commit pins this source and supplies onboarding checks. Its own final SHA is verified with git ls-remote after publication and reported externally in the FS-001 completion message; a Git commit cannot contain its own final SHA. Create DEV1 and DEV3 branches from that exact verified final published commit, not a moving develop ref.

Source builds Passed; explicit xUnit93 Passed; frontend build Passed/Node13 Passed. Frontend lint117 findings and inherited staged whitespace remain. API/HTTPS/SQL not accepted; five recovery controllers inactive under user DI/WorkTime-only approval.

To verify an onboarding checkout against the exact code baseline:

```powershell
$codeBaseline = 'a02cb2f27e9f7944b4e7f8b03da9f6f22bf3808f'
$publishedBaseline = git rev-parse origin/integration/fullstack-base
git merge-base --is-ancestor $codeBaseline $publishedBaseline
if ($LASTEXITCODE -ne 0) { throw 'Unexpected baseline history' }
if ((git rev-parse "$($publishedBaseline):src") -ne 'd89db530b7c1bf313d984ddbb9b7eaa7b705c13e') { throw 'Backend source tree mismatch' }
if ((git rev-parse "$($publishedBaseline):frontend") -ne '577b17911c4ebf96483efb174d86ff0c22c1209f') { throw 'Frontend source tree mismatch' }
# Compare publishedBaseline with the exact final SHA in the FS-001 receipt.
```
