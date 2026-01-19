declare global {
  interface Window {
    __WB_CONFIG__?: {
      apiBaseUrl?: string;
    };
  }
}

const DEFAULT_API_BASE_URL = 'http://localhost:5671';

export function getApiBaseUrl() {
  if (typeof window !== 'undefined') {
    const runtime = window.__WB_CONFIG__?.apiBaseUrl;
    if (runtime && runtime.trim().length > 0) {
      return runtime.trim();
    }
  }

  return import.meta.env.VITE_API_BASE_URL || DEFAULT_API_BASE_URL;
}
