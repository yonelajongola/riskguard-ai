import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { api, demoSession, getSession, setSession } from "../lib/api";
import { demoModeEnabled } from "../lib/config";
import type { AuthResponse, User } from "../types";

interface AuthContextValue {
  user: User | null;
  isAuthenticated: boolean;
  isDemo: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (input: { firstName: string; lastName: string; organizationName: string; email: string; password: string }) => Promise<void>;
  useDemo: () => void;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [session, updateSession] = useState<AuthResponse | null>(() => getSession());

  useEffect(() => {
    const syncSession = (event: Event) => {
      const nextSession = (event as CustomEvent<AuthResponse | null>).detail;
      if (!nextSession) queryClient.clear();
      updateSession(nextSession);
    };
    window.addEventListener("riskguard:session", syncSession);
    return () => window.removeEventListener("riskguard:session", syncSession);
  }, [queryClient]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user: session?.user ?? null,
      isAuthenticated: Boolean(session),
      isDemo: session?.accessToken === "demo-token",
      login: async (email, password) => {
        const result = await api<AuthResponse>("/auth/login", {
          method: "POST",
          body: JSON.stringify({ email, password }),
        });
        queryClient.clear();
        setSession(result);
      },
      register: async (input) => {
        const result = await api<AuthResponse>("/auth/register", {
          method: "POST",
          body: JSON.stringify(input),
        });
        queryClient.clear();
        setSession(result);
      },
      useDemo: () => {
        if (!demoModeEnabled) return;
        queryClient.clear();
        setSession(demoSession);
      },
      logout: async () => {
        if (session && session.accessToken !== "demo-token") {
          try {
            await api("/auth/logout", {
              method: "POST",
              body: JSON.stringify({ refreshToken: session.refreshToken }),
            });
          } catch {
            // Local session removal still protects the browser if the API is unavailable.
          }
        }
        queryClient.clear();
        setSession(null);
      },
    }),
    [queryClient, session],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used inside AuthProvider");
  return context;
}
