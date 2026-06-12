import type { AuthResponse } from "../types";
import { demoModeEnabled } from "./config";

const API_URL = normalizeApiUrl(import.meta.env.VITE_API_BASE_URL);
const SESSION_KEY = "riskguard.session";
const SESSION_EVENT = "riskguard:session";
let refreshPromise: Promise<AuthResponse | null> | null = null;

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly details?: unknown,
  ) {
    super(message);
  }
}

export function getSession(): AuthResponse | null {
  const value = sessionStorage.getItem(SESSION_KEY);
  if (!value) return null;
  try {
    const session = JSON.parse(value) as AuthResponse;
    if (session.accessToken === "demo-token" && !demoModeEnabled) {
      sessionStorage.removeItem(SESSION_KEY);
      return null;
    }
    return session;
  } catch {
    sessionStorage.removeItem(SESSION_KEY);
    return null;
  }
}

export function setSession(session: AuthResponse | null) {
  if (session) sessionStorage.setItem(SESSION_KEY, JSON.stringify(session));
  else sessionStorage.removeItem(SESSION_KEY);
  window.dispatchEvent(new CustomEvent<AuthResponse | null>(SESSION_EVENT, { detail: session }));
}

export async function api<T>(
  path: string,
  options: RequestInit = {},
  fallback?: T,
): Promise<T> {
  const session = getSession();
  if (session?.accessToken === "demo-token") {
    if (fallback !== undefined) return fallback;
    throw new Error("The demo workspace is read-only. Sign out and use a live account to make changes.");
  }
  if (!session && fallback !== undefined) {
    return fallback;
  }

  const response = await authenticatedFetch(path, options);
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

async function authenticatedFetch(path: string, options: RequestInit = {}, retry = true) {
  const session = getSession();
  const headers = new Headers(options.headers);
  if (!(options.body instanceof FormData)) headers.set("Content-Type", "application/json");
  if (session?.accessToken) {
    headers.set("Authorization", `Bearer ${session.accessToken}`);
  }

  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, { ...options, headers });
  } catch {
    throw new ApiError(
      "Unable to reach the RiskGuard API. Confirm the configured backend is running.",
      0,
    );
  }
  if (response.status === 401 && retry && session?.refreshToken && !path.startsWith("/auth/")) {
    const refreshed = await refreshSession(session.refreshToken);
    if (refreshed) {
      return authenticatedFetch(path, options, false);
    }
  }
  if (!response.ok) {
    throw await toError(response);
  }
  return response;
}

async function refreshSession(refreshToken: string) {
  if (refreshPromise) return refreshPromise;
  refreshPromise = fetch(`${API_URL}/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
  })
    .then(async (response) => {
      if (!response.ok) {
        setSession(null);
        return null;
      }
      const refreshed = (await response.json()) as AuthResponse;
      setSession(refreshed);
      return refreshed;
    })
    .catch(() => {
      setSession(null);
      return null;
    })
    .finally(() => {
      refreshPromise = null;
    });
  return refreshPromise;
}

export async function downloadReport(path: string, fileName: string) {
  const session = getSession();
  if (session?.accessToken === "demo-token") {
    throw new Error("The demo workspace cannot generate files. Sign out and use a live account.");
  }
  const response = await authenticatedFetch(path);
  const blob = await response.blob();
  const disposition = response.headers.get("content-disposition") ?? "";
  const match = /filename\*?=(?:UTF-8''|")?([^";]+)/i.exec(disposition);
  const downloadName = match
    ? decodeURIComponent(match[1].replace(/"/g, ""))
    : fileName;
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = downloadName;
  anchor.click();
  window.setTimeout(() => URL.revokeObjectURL(url), 1_000);
}

async function toError(response: Response) {
  const body = await response.json().catch(() => null);
  const validation = body?.errors && typeof body.errors === "object"
    ? Object.values(body.errors).flat().join(" ")
    : "";
  return new ApiError(
    body?.message || validation || body?.detail || body?.title || `Request failed (${response.status})`,
    response.status,
    body,
  );
}

function normalizeApiUrl(value?: string) {
  const normalized = value?.trim().replace(/\/+$/, "");
  if (!normalized) return "/api";
  return /^https?:\/\/[^/]+$/i.test(normalized) ? `${normalized}/api` : normalized;
}

export const demoSession: AuthResponse = {
  accessToken: "demo-token",
  refreshToken: "demo-refresh",
  expiresAtUtc: new Date(Date.now() + 86_400_000).toISOString(),
  user: {
    id: "demo-admin",
    email: "admin@riskguard.local",
    fullName: "System Administrator",
    roles: ["Admin"],
    organizationId: "demo-organization",
  },
};
