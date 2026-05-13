import { useState, useCallback } from 'react';

const STORAGE_KEY = 'currentUserEmail';

function getStoredEmail(): string {
  try {
    return localStorage.getItem(STORAGE_KEY) || '';
  } catch {
    return '';
  }
}

function setStoredEmail(email: string): void {
  try {
    localStorage.setItem(STORAGE_KEY, email);
  } catch {
    // ignore storage errors
  }
}

function clearStoredEmail(): void {
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    // ignore storage errors
  }
}

export function useCurrentUser() {
  const [email, setEmailState] = useState<string>(() => getStoredEmail());

  const login = useCallback((newEmail: string) => {
    const trimmed = newEmail.trim();
    setStoredEmail(trimmed);
    setEmailState(trimmed);
  }, []);

  const logout = useCallback(() => {
    clearStoredEmail();
    setEmailState('');
  }, []);

  return { email, login, logout, isLoggedIn: email.length > 0 };
}
