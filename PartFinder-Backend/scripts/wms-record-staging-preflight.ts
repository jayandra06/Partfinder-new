/**
 * Records WMS staging-gate preflight on a live API (staging lab or named staging tenant).
 *
 * Usage (no secrets in git):
 *   set WMS_API_BASE=https://staging-api.example.com
 *   set WMS_JWT=<org-scoped bearer token>
 *   npm run wms:staging-preflight
 *
 * Optional: WMS_PREFLIGHT_NOTE="staging acceptance Nov 2026"
 */
const base = (process.env.WMS_API_BASE ?? 'http://127.0.0.1:3000').replace(/\/$/, '');
const jwt = process.env.WMS_JWT?.trim();
const note = process.env.WMS_PREFLIGHT_NOTE?.trim();

function redact(obj: unknown): unknown {
  if (obj === null || typeof obj !== 'object') return obj;
  const o = { ...(obj as Record<string, unknown>) };
  delete o.tenantUri;
  delete o.connectionString;
  return o;
}

async function main() {
  if (!jwt) {
    console.error('WMS_JWT is required (Bearer token for staging org with WMS admin).');
    process.exit(1);
  }

  const res = await fetch(`${base}/api/wms/v1/staging-gate/preflight`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${jwt}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(note ? { tenantLabel: note } : {}),
  });

  const text = await res.text();
  let body: unknown;
  try {
    body = JSON.parse(text);
  } catch {
    body = { raw: text };
  }

  if (!res.ok) {
    console.error('Preflight failed:', res.status, text);
    process.exit(1);
  }

  const envelope = body as { data?: unknown };
  const sanitized = redact(envelope.data ?? body);
  console.log('--- Paste into 03A-2-staging-acceptance-report.md §2 (sanitized) ---');
  console.log(JSON.stringify(sanitized, null, 2));
  console.log('--- end ---');
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});