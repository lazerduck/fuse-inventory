import { defineStore } from "pinia";
import { useFuseClient } from "../composables/useFuseClient";
import { useAuthToken } from "../composables/useAuthToken";
import { LogoutSecurityUser, SecurityPosture } from "api/client";
import { type SecurityUserInfo, type LoginSecurityUser } from "api/client";

// Default Admin Role ID (matches PermissionService.DefaultAdminRoleId)
const DEFAULT_ADMIN_ROLE_ID = "00000000-0000-0000-0000-000000000001";

type SecurityUserWithAdminFlag = SecurityUserInfo & {
  isAdmin?: boolean;
};

type Permission = string;

function hasHighestAccess(user: SecurityUserInfo | null | undefined): boolean {
  if (!user) return false;

  const userWithAdminFlag = user as SecurityUserWithAdminFlag;
  if (userWithAdminFlag.isAdmin) return true;

  // Legacy compatibility while older role-based payloads are still present.
  return user.roleIds?.includes(DEFAULT_ADMIN_ROLE_ID) ?? false;
}

function isOpenReadPermission(posture: SecurityPosture | null, permission: Permission): boolean {
  // Backend allows read descriptors during RestrictedEditing even when unauthenticated.
  return posture === SecurityPosture.RestrictedEditing && permission.endsWith("Read");
}

const fuseClient = useFuseClient
const { getToken, setToken, clearToken } = useAuthToken()

export const useFuseStore = defineStore("fuse", {
  state: () => ({
    requireSetup: false as boolean,
    securityPosture: null as SecurityPosture | null,
    currentUser: null as SecurityUserInfo | null,
    sessionToken: null as string | null,
    userPermissions: null as Permission[] | null
  }),
  getters: {
    isLoggedIn: (state) => !!state.currentUser && !!state.sessionToken,
    isAdmin: (state) => hasHighestAccess(state.currentUser),
    userRole: (state) => (state.currentUser as { role?: string } | null)?.role ?? null,
    userName: (state) => state.currentUser?.userName ?? null,
    hasPermission: (state) => (permission: Permission): boolean => {
      // No security restrictions
      if (state.securityPosture === SecurityPosture.Unrestricted) return true;

      // Restricted editing allows read operations without authentication.
      if (isOpenReadPermission(state.securityPosture, permission)) return true;

      // Must be logged in
      if (!state.currentUser) return false;

      // Admin users have full access.
      if (hasHighestAccess(state.currentUser)) return true;

      // Check granular permissions if loaded
      if (state.userPermissions !== null) {
        return state.userPermissions.includes(permission);
      }
      // Permissions not loaded yet, deny
      return false;
    },
    canModify: (state) => {
      switch (state.securityPosture) {
        case SecurityPosture.Unrestricted:
          return true;
        case SecurityPosture.RestrictedEditing:
        case SecurityPosture.FullyRestricted:
          if (!state.currentUser) return false;

          if (hasHighestAccess(state.currentUser)) return true;

          // Check if user has any write permissions
          if (state.userPermissions !== null) {
            return state.userPermissions.length > 0;
          }
          return false;
        default:
          return false;
      }
    },
    canRead: (state) => {
      switch (state.securityPosture) {
        case SecurityPosture.Unrestricted:
        case SecurityPosture.RestrictedEditing:
          return true;
        case SecurityPosture.FullyRestricted:
          return state.currentUser !== null;
        default:
          return false;
      }
    }
  },
  actions: {
    invalidateAuth() {
      clearToken();
      this.sessionToken = null;
      this.currentUser = null;
      this.userPermissions = null;
    },
    async resolveUserPermissions() {
      if (!this.currentUser?.roleIds?.length) {
        this.userPermissions = [];
        return;
      }
      try {
        const roleIds = [...new Set(this.currentUser.roleIds ?? [])];
        const roleResults = await Promise.allSettled(
          roleIds.map((id) => fuseClient().roleGET(id))
        );

        const permissions: Permission[] = [];
        for (const result of roleResults) {
          if (result.status !== "fulfilled") {
            continue;
          }

          for (const perm of (result.value.permissions ?? [])) {
            if (!permissions.includes(perm as Permission)) {
              permissions.push(perm as Permission);
            }
          }
        }

        this.userPermissions = permissions;
      } catch {
        this.userPermissions = [];
      }
    },
    async fetchStatus() {
      const status = await fuseClient().state();
      this.requireSetup = status.requiresSetup || false;
      this.securityPosture = status.posture || null;
      this.currentUser = status.currentUser || null;

      if (!this.currentUser) {
        this.sessionToken = null;
        this.userPermissions = null;
      } else {
        await this.resolveUserPermissions();
      }
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
      this.userPermissions = null;
      
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
          this.invalidateAuth();
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