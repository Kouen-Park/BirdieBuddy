renderNav('account');
const alertEl = document.getElementById('account-alert');
const show = (message, type = 'success') => { alertEl.innerHTML = `<div class="alert ${type}">${escapeHtml(message)}</div>`; };
Api.get('/auth/me').then(user => { document.getElementById('account-email').value = user.email; document.getElementById('display-name').value = user.displayName; });
document.getElementById('profile-form').addEventListener('submit', async event => { event.preventDefault(); try { await Api.put('/auth/profile', { displayName: document.getElementById('display-name').value }); show('Profile saved.'); } catch (error) { show(error.message, 'error'); } });
document.getElementById('password-form').addEventListener('submit', async event => { event.preventDefault(); try { await Api.post('/auth/change-password', { currentPassword: document.getElementById('current-password').value, newPassword: document.getElementById('new-password').value }); event.target.reset(); show('Password changed.'); } catch (error) { show(error.message, 'error'); } });
