import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
const root = fileURLToPath(new URL('../../', import.meta.url));
// The harness hard-restricts SQL to the isolated V2 TEST database and rolls every fixture back.
const child = spawn('dotnet', ['run', '--no-build', '--project', 'scripts/verification/v2-assessment-runtime/V2AssessmentRuntime.csproj'], { cwd: root, windowsHide: true, stdio: 'inherit' });
child.on('error', error => { console.error(error.message); process.exitCode = 1; });
child.on('exit', code => { process.exitCode = code ?? 1; });
