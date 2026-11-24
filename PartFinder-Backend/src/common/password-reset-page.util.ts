function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

/** Minimal hosted form for completing a password reset from email link. */
export function buildPasswordResetHtmlPage(token: string, apiPostUrl: string): string {
  const safeToken = escapeHtml(token);
  const safeApi = escapeHtml(apiPostUrl);
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>PartFinder — Reset password</title>
  <style>
    body { font-family:Segoe UI,system-ui,sans-serif;background:#0b121c;color:#eaf2ff;margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;padding:24px; }
    .card { background:#111a26;border:1px solid #2a3d58;border-radius:12px;padding:32px;max-width:420px;width:100%;box-shadow:0 12px 40px rgba(0,0,0,.35); }
    h1 { font-size:22px;margin:0 0 8px; }
    p { color:#7b8da8;font-size:14px;line-height:1.5;margin:0 0 20px; }
    label { display:block;font-size:11px;letter-spacing:.08em;color:#aab8ca;margin:0 0 6px;font-weight:600; }
    input { width:100%;box-sizing:border-box;padding:12px 14px;border-radius:10px;border:1px solid #2a3d58;background:#0d1520;color:#eaf2ff;font-size:15px;margin:0 0 14px; }
    input:focus { outline:2px solid #1f7ae0;border-color:#1f7ae0; }
    button { width:100%;padding:12px;border:none;border-radius:10px;background:#1f7ae0;color:#fff;font-size:15px;font-weight:600;cursor:pointer;margin-top:6px; }
    button:disabled { opacity:.6;cursor:not-allowed; }
    .msg { margin-top:16px;font-size:14px; }
    .err { color:#ffb4ab; }
    .ok { color:#2abd8f; }
  </style>
</head>
<body>
  <div class="card">
    <h1>Set a new password</h1>
    <p>Choose a strong password. This reset link expires <strong>10 minutes</strong> after it was sent.</p>
    <form id="f">
      <input type="hidden" id="token" value="${safeToken}" />
      <label for="pw">NEW PASSWORD</label>
      <input id="pw" type="password" autocomplete="new-password" required minlength="8" />
      <label for="pw2">CONFIRM PASSWORD</label>
      <input id="pw2" type="password" autocomplete="new-password" required minlength="8" />
      <button type="submit" id="btn">Update password</button>
    </form>
    <div id="msg" class="msg" role="status"></div>
  </div>
  <script>
    const apiUrl = "${safeApi}";
  const tokenInput = document.getElementById('token');
    const msg = document.getElementById('msg');
    const btn = document.getElementById('btn');
    function strong(pw) {
      return /[a-z]/.test(pw) && /[A-Z]/.test(pw) && /\\d/.test(pw) && pw.length >= 8;
    }
    document.getElementById('f').addEventListener('submit', async (e) => {
      e.preventDefault();
      msg.className = 'msg';
      const pw = document.getElementById('pw').value;
      const pw2 = document.getElementById('pw2').value;
      if (pw !== pw2) {
        msg.textContent = 'Passwords do not match.';
        msg.classList.add('err');
        return;
      }
      if (!strong(pw)) {
        msg.textContent = 'Use at least 8 characters with upper, lower, and a number.';
        msg.classList.add('err');
        return;
      }
      if (!tokenInput.value) {
        msg.textContent = 'Reset link is missing or invalid. Request a new link from the PartFinder app.';
        msg.classList.add('err');
        return;
      }
      btn.disabled = true;
      try {
        const res = await fetch(apiUrl, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ token: tokenInput.value, newPassword: pw }),
        });
        const data = await res.json().catch(() => ({}));
        if (res.ok && data.ok) {
          msg.textContent = 'Password updated. You can close this page and sign in to PartFinder with your new password.';
          msg.classList.add('ok');
          btn.style.display = 'none';
          document.getElementById('f').querySelectorAll('input[type=password]').forEach(i => i.disabled = true);
        } else {
          msg.textContent = data.message || 'Reset failed. The link may have expired — request a new one from the app (valid for 10 minutes).';
          msg.classList.add('err');
          btn.disabled = false;
        }
      } catch {
        msg.textContent = 'Network error. Check your connection and try again.';
        msg.classList.add('err');
        btn.disabled = false;
      }
    });
    if (!tokenInput.value) {
      msg.textContent = 'Reset link is missing or invalid. Open the link from your email or request a new one from PartFinder.';
      msg.classList.add('err');
      btn.disabled = true;
    }
  </script>
</body>
</html>`;
}