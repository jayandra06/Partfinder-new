import { readFileSync, writeFileSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const partFinderDir = join(dirname(fileURLToPath(import.meta.url)), '..');

function decodeToString(buf) {
  if (buf.length >= 2 && buf[0] === 0xff && buf[1] === 0xfe) {
    return buf.subarray(2).toString('utf16le');
  }
  if (buf.length >= 2 && buf[0] === 0xfe && buf[1] === 0xff) {
    const u16 = new Uint16Array(buf.buffer, buf.byteOffset + 2, (buf.length - 2) >> 1);
    let out = '';
    for (let i = 0; i < u16.length; i++) {
      const v = u16[i];
      out += String.fromCharCode(((v & 0xff) << 8) | (v >> 8));
    }
    return out;
  }
  const sample = Math.min(buf.length, 400);
  let nullAtOdd = 0;
  for (let i = 1; i < sample; i += 2) {
    if (buf[i] === 0) nullAtOdd++;
  }
  const pairs = Math.floor(sample / 2);
  if (pairs > 8 && nullAtOdd > pairs * 0.6) {
    return buf.toString('utf16le');
  }
  return buf.toString('utf8');
}

function fix(path) {
  const buf = readFileSync(path);
  let text = decodeToString(buf);
  if (text.charCodeAt(0) === 0xfeff) {
    text = text.slice(1);
  }
  writeFileSync(path, text, 'utf8');
  console.log('UTF-8:', path);
}

const files = [
  'Services/MongoConnectionTester.cs',
  'Services/MongoTenantBootstrap.cs',
  'Services/SetupApiClient.cs',
  'Services/SetupStatusResult.cs',
  'MainWindow.xaml.cs',
];

for (const f of files) {
  fix(join(partFinderDir, f));
}
