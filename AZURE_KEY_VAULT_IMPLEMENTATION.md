# Azure Key Vault Integration - Implementation Guide

## Overview

This document provides a comprehensive guide for completing the Azure Key Vault integration feature in Fuse-Inventory. The backend implementation is complete, and this guide focuses on the remaining frontend work and testing.

## Completed Work

### Backend (✅ Complete)

1. **Models and Data Layer**
   - `SecretProvider` model with authentication modes (Managed Identity, Client Secret)
   - `SecretBinding` model to replace string-based `SecretRef`
   - `SecretProviderCapabilities` flags enum (Check, Create, Rotate, Read)
   - Updated `Account` model with backward-compatible `SecretRef` property
   - Migration logic in `JsonFuseStore` for existing accounts

2. **Services**
   - `ISecretProviderService` and implementation
   - `IAzureKeyVaultClient` and implementation with Azure SDK
   - `ISecretOperationService` with auditing
   - Integrated with existing `IAuditService`
   - Service registration in `FuseCodeModule`

3. **API Controllers**
   - `SecretProviderController` with full CRUD operations
   - Test connection endpoint
   - Secret operations (create, rotate, reveal)
   - Admin-only authorization for reveal operations
   - Response DTOs excluding sensitive data

4. **NuGet Packages**
   - Azure.Security.KeyVault.Secrets 4.8.0
   - Azure.Identity 1.17.0

5. **Testing**
   - All 180 existing tests updated and passing
   - Migration logic tested

## Remaining Work

### Phase 4: Frontend Models and Services

#### API Client Generation

1. **Generate TypeScript Client**
   ```bash
   # Start the API
   cd API/Fuse.API
   dotnet run

   # In another terminal, generate the client
   cd UI/Fuse.Web
   npx nswag run ../../nswag.json
   ```

   This will update `src/api/client.ts` with the new types:
   - `SecretProvider`
   - `SecretProviderAuthMode`
   - `SecretProviderCapabilities`
   - `SecretBinding`
   - `SecretBindingKind`
   - `AzureKeyVaultBinding`
   - `CreateSecretProvider`
   - `UpdateSecretProvider`
   - `CreateSecret`
   - `RotateSecret`
   - `RevealSecret`

2. **Create Composables**

Create `src/composables/useSecretProviders.ts`:

```typescript
import { useQuery, useMutation, useQueryClient } from '@tanstack/vue-query'
import { useFuseClient } from './useFuseClient'
import type { SecretProviderResponse } from '../api/client'

export function useSecretProviders() {
  const client = useFuseClient()
  const queryClient = useQueryClient()

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['secretProviders'],
    queryFn: () => client.secretProviderGET(),
    staleTime: 30000
  })

  return {
    data,
    isLoading,
    error,
    refetch
  }
}
```

3. **Update Account Composable**

Update `src/composables/useAccounts.ts` to handle the new `SecretBinding` type when creating/updating accounts.

### Phase 5: Frontend UI Components

#### Secret Providers Page

File: `src/pages/SecretProvidersPage.vue` (placeholder created)

Features to implement:
1. List all secret providers with columns:
   - Name
   - Vault URI
   - Auth Mode (Managed Identity / Client Secret)
   - Capabilities (badges for Check, Create, Rotate, Read)
   - Actions (Edit, Delete, Test Connection)

2. Add/Edit Dialog Component:
   - Name input
   - Vault URI input
   - Auth Mode selector
   - Credentials fields (conditionally shown for Client Secret mode)
     - Tenant ID
     - Client ID
     - Client Secret (password field)
   - Capability toggles with descriptions
     - Check (required, always enabled)
     - Create
     - Rotate
     - Read (show warning banner)
   - Test Connection button (validates before save)

3. Delete Confirmation:
   - Warn if accounts are using the provider
   - Show count of dependent accounts

#### Secret Provider Form Component

Create `src/components/secretproviders/SecretProviderForm.vue`:

```vue
<template>
  <q-card class="form-card">
    <q-card-section>
      <div class="text-h6">{{ mode === 'edit' ? 'Edit' : 'Add' }} Secret Provider</div>
    </q-card-section>
    
    <q-card-section>
      <q-form @submit.prevent="submitForm">
        <q-input 
          v-model="form.name" 
          label="Name" 
          :disable="loading" 
          required 
        />
        
        <q-input 
          v-model="form.vaultUri" 
          label="Vault URI" 
          :disable="loading" 
          type="url" 
          placeholder="https://your-vault.vault.azure.net/"
          required 
          hint="e.g., https://mykeyvault.vault.azure.net/"
        />
        
        <q-select 
          v-model="form.authMode" 
          :options="authModeOptions" 
          label="Authentication Mode" 
          :disable="loading" 
          required
        />
        
        <div v-if="form.authMode === 'ClientSecret'" class="q-gutter-sm q-mt-md">
          <q-input 
            v-model="form.tenantId" 
            label="Tenant ID" 
            :disable="loading" 
            required 
          />
          <q-input 
            v-model="form.clientId" 
            label="Client ID" 
            :disable="loading" 
            required 
          />
          <q-input 
            v-model="form.clientSecret" 
            label="Client Secret" 
            :disable="loading" 
            type="password" 
            required 
          />
        </div>
        
        <q-separator class="q-my-md" />
        
        <div class="text-subtitle2 q-mb-sm">Capabilities</div>
        
        <q-checkbox 
          v-model="form.capabilities.check" 
          label="Check (Required)" 
          :disable="true" 
        />
        
        <q-checkbox 
          v-model="form.capabilities.create" 
          label="Create - Allow creating new secrets" 
          :disable="loading" 
        />
        
        <q-checkbox 
          v-model="form.capabilities.rotate" 
          label="Rotate - Allow rotating existing secrets" 
          :disable="loading" 
        />
        
        <q-checkbox 
          v-model="form.capabilities.read" 
          label="Read - Allow revealing secret values (Admin only)" 
          :disable="loading" 
        />
        
        <q-banner v-if="form.capabilities.read" dense class="bg-orange-1 text-orange-9 q-mt-sm">
          <template v-slot:avatar>
            <q-icon name="warning" color="orange" />
          </template>
          Read capability allows admin users to reveal secret values. All reveal operations are audited.
        </q-banner>
        
        <div class="q-mt-md flex justify-end">
          <q-btn label="Test Connection" flat @click="testConnection" :disable="loading" class="q-mr-sm" />
          <q-btn label="Cancel" flat @click="$emit('cancel')" :disable="loading" />
          <q-btn label="Save" color="primary" type="submit" :loading="loading" class="q-ml-sm" />
        </div>
      </q-form>
    </q-card-section>
  </q-card>
</template>
```

#### Update Account Form

Update `src/components/accounts/AccountForm.vue` to replace the simple "Secret Reference" input with:

1. **Secret Storage Type Selector**
   - None
   - Plain Reference
   - Azure Key Vault

2. **Conditional Fields**

For "Plain Reference":
```vue
<q-input 
  v-model="form.secretRef" 
  label="Secret Reference" 
  dense 
  outlined 
  hint="Reference to where the secret is stored"
/>
```

For "Azure Key Vault":
```vue
<q-select 
  v-model="form.secretProviderId" 
  :options="secretProviderOptions" 
  label="Secret Provider" 
  dense 
  outlined 
  required
/>

<q-input 
  v-model="form.secretName" 
  label="Secret Name" 
  dense 
  outlined 
  required
  hint="Name of the secret in Key Vault"
/>

<q-input 
  v-model="form.secretVersion" 
  label="Version (Optional)" 
  dense 
  outlined
  hint="Leave empty to use latest version"
/>
```

3. **Secret Operation Buttons** (only visible when editing existing account with AKV binding)

```vue
<div v-if="mode === 'edit' && form.secretBindingKind === 'AzureKeyVault'" class="q-mt-md">
  <q-btn 
    v-if="hasCreateCapability" 
    label="Generate & Create Secret" 
    icon="add_circle" 
    flat 
    color="primary" 
    @click="generateAndCreateSecret" 
  />
  
  <q-btn 
    v-if="hasRotateCapability" 
    label="Rotate Secret" 
    icon="autorenew" 
    flat 
    color="primary" 
    @click="rotateSecret" 
  />
  
  <q-btn 
    v-if="hasReadCapability && isAdmin" 
    label="Reveal Secret" 
    icon="visibility" 
    flat 
    color="orange" 
    @click="showRevealDialog" 
  />
</div>
```

#### Secret Reveal Dialog

Create `src/components/accounts/SecretRevealDialog.vue`:

```vue
<template>
  <q-dialog v-model="isOpen" persistent>
    <q-card style="min-width: 400px">
      <q-card-section>
        <div class="text-h6">Reveal Secret</div>
      </q-card-section>
      
      <q-card-section>
        <q-banner dense class="bg-red-1 text-negative q-mb-md">
          <template v-slot:avatar>
            <q-icon name="warning" color="red" />
          </template>
          <div class="text-weight-bold">Security Warning</div>
          <div class="text-body2">
            You are about to reveal a secret value. This operation:
            <ul class="q-pl-md q-my-sm">
              <li>Will be audited with your username and timestamp</li>
              <li>Should only be done when absolutely necessary</li>
              <li>Exposes sensitive credential information</li>
            </ul>
            Only proceed if you understand the security implications.
          </div>
        </q-banner>
        
        <q-input 
          v-if="revealed" 
          v-model="secretValue" 
          label="Secret Value" 
          type="password" 
          readonly
          :append-icon="showValue ? 'visibility_off' : 'visibility'"
          @click:append="showValue = !showValue"
        >
          <template v-slot:append>
            <q-icon 
              :name="showValue ? 'visibility_off' : 'visibility'" 
              class="cursor-pointer" 
              @click="showValue = !showValue" 
            />
          </template>
        </q-input>
        
        <div v-if="!revealed" class="text-body2 text-grey-7">
          Click "Reveal" to display the secret value.
        </div>
      </q-card-section>
      
      <q-card-actions align="right">
        <q-btn label="Close" flat color="primary" @click="close" />
        <q-btn 
          v-if="!revealed" 
          label="Reveal" 
          color="negative" 
          @click="revealSecret" 
          :loading="loading" 
        />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>
```

### Phase 6: Testing

1. **Unit Tests**
   - Create `SecretProviderService.Tests.cs`
   - Create `AzureKeyVaultClient.Tests.cs`
   - Test all CRUD operations
   - Test capability validation
   - Test connection testing

2. **Integration Tests**
   - Test complete flow: create provider → create secret → rotate secret
   - Test migration of existing accounts
   - Test admin authorization for reveal

3. **Manual Testing Checklist**
   - [ ] Create secret provider with Managed Identity auth
   - [ ] Create secret provider with Client Secret auth
   - [ ] Test connection with valid credentials
   - [ ] Test connection with invalid credentials
   - [ ] Update provider capabilities
   - [ ] Delete provider (should fail if accounts use it)
   - [ ] Create account with AKV binding
   - [ ] Generate and create secret for account
   - [ ] Rotate secret
   - [ ] Reveal secret (admin only)
   - [ ] Verify audit logs show all secret operations
   - [ ] Test migration of existing accounts with plain SecretRef
   - [ ] Verify backward compatibility

### Phase 7: Documentation and Security

1. **Update README.md**
   - Add Azure Key Vault integration to features list
   - Add configuration guide
   - Add security best practices

2. **Security Documentation**
   ```markdown
   ## Azure Key Vault Integration

   ### Setup

   1. Create an Azure Key Vault
   2. Configure access policies or RBAC
   3. For Managed Identity:
      - Enable system-assigned managed identity on your host
      - Grant Key Vault Secrets Officer role
   4. For Client Secret:
      - Create service principal in Azure AD
      - Grant Key Vault Secrets Officer role
      - Store credentials securely

   ### Security Considerations

   - **Managed Identity** is recommended for production use
   - **Client Secret** credentials are stored encrypted
   - **Read capability** should be disabled unless absolutely necessary
   - All secret reveal operations are audited
   - Only admin users can reveal secrets
   - Regular rotation of credentials is recommended
   ```

3. **Run Security Scans**
   ```bash
   # CodeQL is already configured in the workflow
   # Run locally:
   dotnet test
   
   # Check for vulnerabilities in dependencies
   dotnet list package --vulnerable
   ```

## API Endpoints Reference

### Secret Providers

- `GET /api/secretprovider` - List all providers
- `GET /api/secretprovider/{id}` - Get provider by ID
- `POST /api/secretprovider` - Create provider
- `PUT /api/secretprovider/{id}` - Update provider
- `DELETE /api/secretprovider/{id}` - Delete provider
- `POST /api/secretprovider/test-connection` - Test connection

### Secret Operations

- `POST /api/secretprovider/{providerId}/secrets` - Create secret
- `POST /api/secretprovider/{providerId}/secrets/{secretName}/rotate` - Rotate secret
- `POST /api/secretprovider/{providerId}/secrets/{secretName}/reveal` - Reveal secret (admin only)

## Data Migration

Existing accounts with plain string `SecretRef` are automatically migrated to use `SecretBinding` with kind `PlainReference`. The `SecretRef` property remains available for backward compatibility but now returns a computed value based on the binding.

## Future Enhancements

- Support for other secret providers (HashiCorp Vault, AWS Secrets Manager)
- Automatic secret rotation scheduling
- Secret expiration notifications
- Multi-region Key Vault support
- Secret versioning management UI
