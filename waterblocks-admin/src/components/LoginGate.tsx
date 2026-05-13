import { useState, type FormEvent } from 'react';

type LoginGateProps = {
  onLogin: (email: string) => void;
};

export function LoginGate({ onLogin }: LoginGateProps) {
  const [email, setEmail] = useState('');
  const [error, setError] = useState('');

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    const trimmed = email.trim();
    if (!trimmed) {
      setError('Email is required');
      return;
    }
    if (!trimmed.includes('@')) {
      setError('Please enter a valid email address');
      return;
    }
    onLogin(trimmed);
  };

  return (
    <div className="login-gate">
      <div className="login-gate-card">
        <h2 className="login-gate-title">Waterblocks Admin</h2>
        <p className="login-gate-subtitle">Enter your email address to continue</p>
        <form onSubmit={handleSubmit} className="login-gate-form">
          <input
            type="email"
            className="login-gate-input"
            placeholder="your@email.com"
            value={email}
            onChange={(e) => {
              setEmail(e.target.value);
              setError('');
            }}
            autoFocus
          />
          {error && <p className="login-gate-error">{error}</p>}
          <button type="submit" className="btn-primary login-gate-submit">
            Continue
          </button>
        </form>
      </div>
    </div>
  );
}
