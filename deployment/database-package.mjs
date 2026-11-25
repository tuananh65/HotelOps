import { readFile, writeFile } from 'node:fs/promises';
import { createCipheriv, createDecipheriv, randomBytes, createHash } from 'node:crypto';
import { gzipSync, gunzipSync } from 'node:zlib';

// Authenticated encryption; the random key must NEVER be committed with the package.
const [mode, input, output, keyFile] = process.argv.slice(2);
if (!['encrypt', 'decrypt'].includes(mode) || !input || !output || !keyFile) {
  throw new Error('Usage: node deployment/database-package.mjs encrypt|decrypt input output private-key-file');
}
const magic = Buffer.from('HOTELOPSDB1');
const source = await readFile(input);
if (mode === 'encrypt') {
  const key = randomBytes(32);
  const iv = randomBytes(12);
  const cipher = createCipheriv('aes-256-gcm', key, iv);
  cipher.setAAD(magic);
  const ciphertext = Buffer.concat([cipher.update(gzipSync(source)), cipher.final()]);
  await writeFile(keyFile, key.toString('base64'), { flag: 'wx', mode: 0o600 });
  await writeFile(output, Buffer.concat([magic, iv, cipher.getAuthTag(), ciphertext]), { flag: 'wx' });
} else {
  if (!source.subarray(0, magic.length).equals(magic)) throw new Error('Invalid database package');
  const key = Buffer.from((await readFile(keyFile, 'utf8')).trim(), 'base64');
  if (key.length !== 32) throw new Error('Invalid key');
  const decipher = createDecipheriv('aes-256-gcm', key, source.subarray(magic.length, magic.length + 12));
  decipher.setAAD(magic);
  decipher.setAuthTag(source.subarray(magic.length + 12, magic.length + 28));
  const compressed = Buffer.concat([decipher.update(source.subarray(magic.length + 28)), decipher.final()]);
  await writeFile(output, gunzipSync(compressed), { flag: 'wx', mode: 0o600 });
}
console.log(JSON.stringify({ mode, output, sha256: createHash('sha256').update(await readFile(output)).digest('hex') }));
