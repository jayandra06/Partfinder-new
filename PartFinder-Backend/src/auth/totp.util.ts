import * as crypto from 'crypto';

const BASE32 = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';

export function fromBase32(encoded: string): Buffer {
  const s = encoded.trim().toUpperCase().replace(/\s+/g, '');
  if (!s.length) {
    throw new Error('empty');
  }
  const outputLength = Math.floor((s.length * 5) / 8);
  const data = Buffer.alloc(outputLength);
  let buffer = 0;
  let bitsLeft = 0;
  let count = 0;
  for (const ch of s) {
    const val = BASE32.indexOf(ch);
    if (val < 0) {
      throw new Error('invalid');
    }
    buffer = (buffer << 5) | val;
    bitsLeft += 5;
    if (bitsLeft >= 8) {
      data[count++] = (buffer >> (bitsLeft - 8)) & 0xff;
      bitsLeft -= 8;
    }
  }
  return data;
}

function packCounterBigEndian(counter: number): Buffer {
  const buf = Buffer.allocUnsafe(8);
  let v = counter;
  for (let i = 7; i >= 0; i--) {
    buf[i] = v & 0xff;
    v = Math.floor(v / 256);
  }
  return buf;
}

function generateTotp(key: Buffer, counter: number): string {
  const buf = packCounterBigEndian(counter);
  const hash = crypto.createHmac('sha1', key).update(buf).digest();
  const offset = hash[hash.length - 1] & 0x0f;
  const binary =
    ((hash[offset] & 0x7f) << 24) |
    (hash[offset + 1] << 16) |
    (hash[offset + 2] << 8) |
    hash[offset + 3];
  const otp = binary % 1000000;
  return otp.toString().padStart(6, '0');
}

export function verifyTotp(
  secretBase32: string,
  sixDigitCode: string,
  drift = 1,
): boolean {
  if (!/^\d{6}$/.test(sixDigitCode)) {
    return false;
  }
  let key: Buffer;
  try {
    key = fromBase32(secretBase32);
  } catch {
    return false;
  }
  const counter = Math.floor(Date.now() / 1000 / 30);
  for (let d = -drift; d <= drift; d++) {
    if (generateTotp(key, counter + d) === sixDigitCode) {
      return true;
    }
  }
  return false;
}

export function isPlausibleTotpSecretBase32(s: string): boolean {
  try {
    const b = fromBase32(s);
    return b.length >= 10;
  } catch {
    return false;
  }
}
