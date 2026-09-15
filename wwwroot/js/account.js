renderNav('account');
const alertEl = document.getElementById('account-alert');
let accountUserId = null;
const show = (message, type = 'success') => { alertEl.innerHTML = `<div class="alert ${type}">${escapeHtml(message)}</div>`; };
Api.get('/auth/me').then(user => {
  accountUserId = user.id; document.getElementById('account-email').value = user.email; document.getElementById('display-name').value = user.displayName;
  if (!user.emailVerified) {
    alertEl.innerHTML = '<div class="alert verification-prompt"><span>Your email is not verified.</span><button class="btn btn-secondary" id="send-verification" type="button">Send verification email</button></div>';
    document.getElementById('send-verification').addEventListener('click', async event => {
      const button = event.currentTarget; button.disabled = true;
      try { await Api.post('/auth/send-verification', {}); show('Verification email requested. Check your inbox.'); }
      catch (error) { show(error.message, 'error'); button.disabled = false; }
    });
  }
});
document.getElementById('profile-form').addEventListener('submit', async event => { event.preventDefault(); try { await Api.put('/auth/profile', { displayName: document.getElementById('display-name').value }); show('Profile saved.'); } catch (error) { show(error.message, 'error'); } });
document.getElementById('password-form').addEventListener('submit', async event => {
  event.preventDefault();
  const button = event.currentTarget.querySelector('button[type="submit"]');
  button.disabled = true;
  try {
    await Api.post('/auth/change-password', { currentPassword: document.getElementById('current-password').value, newPassword: document.getElementById('new-password').value });
    sessionStorage.removeItem('birdiebuddy.liveUser');
    Api.csrfToken = null;
    location.replace('/login.html?passwordChanged=1');
  } catch (error) {
    show(error.message, 'error');
    button.disabled = false;
  }
});
document.getElementById('export-account').addEventListener('click', async event => {
  const button = event.currentTarget; button.disabled = true;
  try {
    const response = await fetch('/api/auth/export', { credentials: 'same-origin', cache: 'no-store', redirect: 'manual' });
    if (!response.ok) throw new Error(`Export failed (${response.status}).`);
    if (!response.headers.get('content-type')?.includes('application/json')) throw new Error('The export response was not valid JSON. Please sign in again.');
    const blob = await response.blob(); const url = URL.createObjectURL(blob); const link = document.createElement('a');
    link.href = url; link.download = `birdie-buddy-export-${new Date().toISOString().slice(0, 10)}.json`;
    document.body.append(link); link.click(); link.remove(); URL.revokeObjectURL(url); show('Your data export was downloaded.');
  } catch (error) { show(error.message, 'error'); } finally { button.disabled = false; }
});
document.getElementById('delete-account-form').addEventListener('submit', async event => {
  event.preventDefault(); const form = event.currentTarget; const button = form.querySelector('button[type="submit"]');
  if (document.getElementById('delete-confirmation').value !== 'DELETE MY ACCOUNT') return show('Type DELETE MY ACCOUNT exactly to confirm.', 'error');
  button.disabled = true;
  try {
    if (!accountUserId) accountUserId = (await Api.get('/auth/me')).id;
    await Api.post('/auth/delete-account', { password: document.getElementById('delete-password').value, confirmation: document.getElementById('delete-confirmation').value });
    try {
      const prefix = `birdiebuddy.live.v2.${accountUserId}.`;
      for (let index = localStorage.length - 1; index >= 0; index -= 1) {
        const key = localStorage.key(index); if (key?.startsWith(prefix)) localStorage.removeItem(key);
      }
    } catch { /* Server deletion succeeded; storage restrictions must not prevent redirect. */ }
    sessionStorage.removeItem('birdiebuddy.liveUser'); location.replace('/signup.html?deleted=1');
  } catch (error) { show(error.message, 'error'); button.disabled = false; }
});
