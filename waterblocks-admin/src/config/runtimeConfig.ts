declare global {
  interface Window {
    __WB_CONFIG__?: {
      apiBaseUrl?: string;
      archiveAllWorkspacesEnabled?: boolean | string;
    };
  }
}

const DEFAULT_API_BASE_URL = 'http://localhost:5671';

function parseBoolean(value: boolean | string | undefined) {
  if (typeof value === 'boolean') {
    return value;
  }

  if (typeof value === 'string') {
    return value.trim().toLowerCase() === 'true';
  }

  return false;
}

export function getApiBaseUrl() {
  if (typeof window !== 'undefined') {
    const runtime = window.__WB_CONFIG__?.apiBaseUrl;
    if (runtime && runtime.trim().length > 0) {
      return runtime.trim();
    }
  }

  return import.meta.env.VITE_API_BASE_URL || DEFAULT_API_BASE_URL;
}

export function getArchiveAllWorkspacesEnabled() {
  if (typeof window !== 'undefined' && window.__WB_CONFIG__?.archiveAllWorkspacesEnabled !== undefined) {
    return parseBoolean(window.__WB_CONFIG__.archiveAllWorkspacesEnabled);
  }

  return parseBoolean(import.meta.env.VITE_ARCHIVE_ALL_WORKSPACES_ENABLED);
}
