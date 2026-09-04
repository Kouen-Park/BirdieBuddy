const message = document.getElementById('account-message');
const show = (text, error = false) => { message.textContent = text; message.className = `alert ${error ? 'error' : 'success'}`; message.hidden = false; };

async function csrfPost(path, body) {
  const csrfResponse = await fetch('/api/security/csrf', { credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' } });
  const csrf = await csrfResponse.json().catch(() => null);
  if (!csrfResponse.ok || !csrf?.token) throw new Error('Could not initialize a secure session. Refresh and try again.');
  const response = await fetch(`/api/auth/${path}`, { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf.token }, body: JSON.stringify(body) });
  const result = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) throw new Error(result?.detail || result?.title || 'The request could not be completed.');
  return result;
}

document.getElementById('forgot-form')?.addEventListener('submit', async event => {
  event.preventDefault(); const button = event.currentTarget.querySelector('button'); button.disabled = true;
  try { const result = await csrfPost('forgot-password', { email: document.getElementById('email').value }); event.currentTarget.hidden = true; show(result.message); }
  catch (error) { show(error.message, true); button.disabled = false; }
});

document.getElementById('reset-form')?.addEventListener('submit', async event => {
  event.preventDefault(); const button = event.currentTarget.querySelector('button'); button.disabled = true;
  try { await csrfPost('reset-password', { token: new URLSearchParams(location.search).get('token') || '', newPassword: document.getElementById('new-password').value }); event.currentTarget.hidden = true; show('Password reset. You can now sign in.'); }
  catch (error) { show(error.message, true); button.disabled = false; }
});

if (location.pathname.endsWith('/verify-email.html')) {
  csrfPost('verify-email', { token: new URLSearchParams(location.search).get('token') || '' })
    .then(() => show('Email verified. You can continue to Birdie Buddy.'))
    .catch(error => show(error.message, true));
}
