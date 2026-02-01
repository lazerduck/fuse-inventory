import { defineStore } from "pinia";
import { useFuseClient } from "../composables/useFuseClient";
import { useAuthToken } from "../composables/useAuthToken";
import { LogoutSecurityUser, SecurityRole } from "../api/client";
import { type SecurityUserInfo, SecurityLevel, type LoginSecurityUser } from "../api/client";

const fuseClient = useFuseClient
const { getToken, setToken, clearToken } = useAuthToken()

export const useFuseStore = defineStore("fuse", {
  state: () => ({
    requireSetup: false as boolean,
    securityLevel: null as SecurityLevel | null,
    currentUser: null as SecurityUserInfo | null,
    sessionToken: null as string | null
  }),
  getters: {
    isLoggedIn: (state) => !!state.currentUser && !!state.sessionToken,
    isAdmin: (state) => state.currentUser?.role === SecurityRole.Admin,
    userRole: (state) => state.currentUser?.role ?? null,
    userName: (state) => state.currentUser?.userName ?? null,
    canModify: (state) => {
      switch (state.securityLevel) {
        case SecurityLevel.None:
          return true;
        case SecurityLevel.RestrictedEditing:
          case SecurityLevel.FullyRestricted:
          return state.currentUser !== null && state.currentUser.role === SecurityRole.Admin;
        default:
          return false;
      }
    },
    canRead: (state) => {
      switch (state.securityLevel) {
        case SecurityLevel.None:
        case SecurityLevel.RestrictedEditing:
          return true;
        case SecurityLevel.FullyRestricted:
          return state.currentUser !== null;
        default:
          return false;
      }
    }
  },
  actions: {
    async fetchStatus() {
      const status = await fuseClient().state();
      this.requireSetup = status.requiresSetup || false;
      this.securityLevel = status.level || null;
      this.currentUser = status.currentUser || null;
    },
    async login(credentials: LoginSecurityUser) {
      const session = await fuseClient().login(credentials);
      
      // Store the token and user info
      if (session.token && session.expiresAt) {
        setToken(session.token, session.expiresAt);
        this.sessionToken = session.token;
      }
      
      this.currentUser = session.user || null;
      
      // Refresh full status after login
      await this.fetchStatus();
    },
    async logout() {
      // Create logout request with current token
      const logoutRequest = new LogoutSecurityUser();
      if (this.sessionToken) {
        logoutRequest.token = this.sessionToken;
      }
      
      await fuseClient().logout(logoutRequest);
      
      // Clear local state and token
      clearToken();
      this.sessionToken = null;
      this.currentUser = null;
      
      // Refresh status after logout
      await this.fetchStatus();
    },
    async initializeAuth() {
      // Try to restore session from stored token
      const token = getToken();
      if (token) {
        this.sessionToken = token;
        try {
          // Fetch current status which will validate the token
          await this.fetchStatus();
          
          // If we got a current user, the session is valid
          if (!this.currentUser) {
            // Token is invalid, clear it
            clearToken();
            this.sessionToken = null;
          }
        } catch (error) {
          // Token is invalid or expired
          clearToken();
          this.sessionToken = null;
          this.currentUser = null;
        }
      } else {
        // No token, just fetch status
        await this.fetchStatus();
      }
    }
  }
});


/*
{
  "Level": "None",
  "UpdatedAt": "2025-11-07T18:44:58.5003864Z",
  "RequiresSetup": true,
  "HasUsers": false,
  "CurrentUser": null
}
*/