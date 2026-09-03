const authForm = document.getElementById('auth-form');
const authError = document.getElementById('auth-error');
const submitButton = document.getElementById('auth-submit');
const isRegister = document.body.dataset.authMode === 'register';

function getReturnUrl() {
    const value = new URLSearchParams(window.location.search).get('returnUrl');
    return value && value.startsWith('/') && !value.startsWith('//') ? value : '/index.html';
}

function showAuthError(message) {
    authError.textContent = message;
    authError.hidden = false;
}

authForm?.addEventListener('submit', async event => {
    event.preventDefault();
    authError.hidden = true;
    submitButton.disabled = true;
    submitButton.textContent = isRegister ? 'Creating account…' : 'Signing in…';

    const formData = new FormData(authForm);
    const payload = isRegister
        ? {
            email: String(formData.get('email') || ''),
            displayName: String(formData.get('displayName') || ''),
            password: String(formData.get('password') || '')
        }
        : {
            email: String(formData.get('email') || ''),
            password: String(formData.get('password') || '')
        };

    try {
        const csrfResponse = await fetch('/api/security/csrf', {
            credentials: 'same-origin',
            cache: 'no-store',
            headers: { 'Accept': 'application/json' }
        });
        const csrf = await csrfResponse.json().catch(() => null);
        if (!csrfResponse.ok || !csrf?.token) {
            throw new Error(`Could not initialize a secure session (${csrfResponse.status}). Refresh the page and try again.`);
        }
        const response = await fetch(isRegister ? '/api/auth/register' : '/api/auth/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf.token },
            credentials: 'same-origin',
            body: JSON.stringify(payload)
        });
        const body = await response.json().catch(() => null);
        if (!response.ok) throw new Error(body?.detail || body?.title || 'Something went wrong. Please try again.');
        window.location.replace(getReturnUrl());
    } catch (error) {
        showAuthError(error.message);
        submitButton.disabled = false;
        submitButton.textContent = isRegister ? 'Create account' : 'Sign in';
    }
});
