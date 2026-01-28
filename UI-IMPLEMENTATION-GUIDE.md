# Frontend Implementation Guide for Organizational Ownership Model

## Overview
This guide provides step-by-step instructions for implementing the UI components for the organizational ownership model. The backend API is complete and ready.

## Prerequisites
- Backend is fully implemented with working API endpoints:
  - GET/POST/PUT/DELETE `/api/position`
  - GET/POST/PUT/DELETE `/api/responsibilitytype`
  - GET/POST/PUT/DELETE `/api/application/{appId}/responsibilityassignment`

## API Client Regeneration
First, regenerate the TypeScript API client to include the new endpoints:
```bash
cd /home/runner/work/fuse-inventory/fuse-inventory
# Run nswag to regenerate client from OpenAPI spec
# The API client will be updated at UI/Fuse.Web/src/api/client.ts
```

## Implementation Steps

### 1. Create PositionsPage.vue
**Location:** `UI/Fuse.Web/src/pages/PositionsPage.vue`

**Features:**
- List all positions in a Quasar table
- Columns: Name, Description, Tags, Actions
- Search/filter functionality
- Create button (opens dialog)
- Edit/Delete actions per row
- Tag chip display
- Permission checks using `fuseStore.canModify`

**API Calls:**
```typescript
const { data: positions } = useQuery({
  queryKey: ['positions'],
  queryFn: () => useFuseClient().positionAll()
})

const createMutation = useMutation({
  mutationFn: (cmd: CreatePosition) => useFuseClient().positionPOST(cmd),
  onSuccess: () => queryClient.invalidateQueries({ queryKey: ['positions'] })
})
```

### 2. Create PositionDialog.vue
**Location:** `UI/Fuse.Web/src/components/position/PositionDialog.vue`

**Features:**
- Form with fields: Name (required), Description, Tags (multi-select)
- Validation
- Save/Cancel buttons
- Reusable for both create and edit modes

**Props:**
- `modelValue: boolean` (dialog open state)
- `position?: Position` (for edit mode)

### 3. Create ResponsibilityTypesPage.vue
**Location:** `UI/Fuse.Web/src/pages/ResponsibilityTypesPage.vue`

**Features:**
- Similar to PositionsPage but for responsibility types
- Columns: Name, Description, Actions
- No tags (ResponsibilityType doesn't have tags)

**API Calls:**
```typescript
const { data: types } = useQuery({
  queryKey: ['responsibilityTypes'],
  queryFn: () => useFuseClient().responsibilityTypeAll()
})
```

### 4. Create ResponsibilityTypeDialog.vue
**Location:** `UI/Fuse.Web/src/components/responsibilitytype/ResponsibilityTypeDialog.vue`

**Features:**
- Form with fields: Name (required), Description
- Simpler than PositionDialog (no tags)

### 5. Add Ownership Tab to ApplicationEditPage.vue
**Location:** `UI/Fuse.Web/src/pages/ApplicationEditPage.vue`

**Changes:**
- Add new tab "Ownership" to existing tabs
- Display ResponsibilityAssignmentTable component in tab content
- Pass applicationId as prop

**Tab Structure:**
```vue
<q-tabs v-model="activeTab">
  <q-tab name="details" label="Details" />
  <q-tab name="instances" label="Instances" />
  <q-tab name="ownership" label="Ownership" />
  <q-tab name="pipelines" label="Pipelines" />
</q-tabs>

<q-tab-panels v-model="activeTab">
  <!-- existing tab panels -->
  <q-tab-panel name="ownership">
    <ResponsibilityAssignmentTable :applicationId="applicationId" />
  </q-tab-panel>
</q-tab-panels>
```

### 6. Create ResponsibilityAssignmentTable.vue
**Location:** `UI/Fuse.Web/src/components/responsibilityassignment/ResponsibilityAssignmentTable.vue`

**Features:**
- Display assignments for an application
- Columns: Position Name, Responsibility Type, Scope, Environment (if scoped), Notes, Primary, Actions
- Add Assignment button
- Edit/Delete per row
- Resolve position/responsibility type/environment names from IDs

**Props:**
- `applicationId: string`

**API Calls:**
```typescript
const { data: assignments } = useQuery({
  queryKey: ['responsibilityAssignments', applicationId],
  queryFn: () => useFuseClient().applicationResponsibilityAssignmentAll(applicationId)
})

// Also fetch positions, types, and environments for display
const { data: positions } = useQuery({
  queryKey: ['positions'],
  queryFn: () => useFuseClient().positionAll()
})
```

### 7. Create ResponsibilityAssignmentDialog.vue
**Location:** `UI/Fuse.Web/src/components/responsibilityassignment/ResponsibilityAssignmentDialog.vue`

**Features:**
- Form fields:
  - Position (dropdown from positions)
  - Responsibility Type (dropdown from responsibility types)
  - Scope (radio: All / Environment)
  - Environment (dropdown, enabled only if Scope=Environment)
  - Notes (text area)
  - Primary (checkbox)

**Props:**
- `modelValue: boolean`
- `applicationId: string`
- `assignment?: ResponsibilityAssignment` (for edit mode)

**Validation:**
- Position and Responsibility Type required
- Environment required if Scope=Environment

### 8. Update Router
**Location:** `UI/Fuse.Web/src/router.ts`

Add routes:
```typescript
{
  path: '/positions',
  name: 'positions',
  component: () => import('./pages/PositionsPage.vue')
},
{
  path: '/responsibility-types',
  name: 'responsibilityTypes',
  component: () => import('./pages/ResponsibilityTypesPage.vue')
}
```

### 9. Update Navigation Menu
**Location:** `UI/Fuse.Web/src/App.vue`

Add menu items in the Settings section (after existing settings items):
```vue
<q-item clickable v-ripple :to="{ name: 'positions' }" active-class="bg-primary text-white">
  <q-item-section avatar>
    <q-icon name="people" />
  </q-item-section>
  <q-item-section>Positions</q-item-section>
</q-item>

<q-item clickable v-ripple :to="{ name: 'responsibilityTypes' }" active-class="bg-primary text-white">
  <q-item-section avatar>
    <q-icon name="assignment_ind" />
  </q-item-section>
  <q-item-section>Responsibility Types</q-item-section>
</q-item>
```

## Testing Checklist

1. **Positions Management:**
   - [ ] Create position with name, description, tags
   - [ ] Edit existing position
   - [ ] Delete position (verify cannot delete if referenced)
   - [ ] Search/filter positions

2. **Responsibility Types Management:**
   - [ ] Create responsibility type
   - [ ] Edit existing responsibility type
   - [ ] Delete responsibility type (verify cannot delete if referenced)
   - [ ] Search/filter types

3. **Responsibility Assignments:**
   - [ ] Add assignment with Scope=All
   - [ ] Add assignment with Scope=Environment and specific environment
   - [ ] Edit assignment
   - [ ] Delete assignment
   - [ ] Mark assignment as primary
   - [ ] View assignments on Application page

4. **Data Persistence:**
   - [ ] Restart application and verify data persists

5. **Audit Logging:**
   - [ ] Verify audit logs are created for all operations

## UI/UX Guidelines

1. **Language:**
   - Use "Positions" not "Roles" or "People"
   - Include helper text: "Positions represent organisational functions, not individuals."

2. **Tables:**
   - Use Quasar q-table with pagination
   - Enable search/filter
   - Show loading state

3. **Forms:**
   - Use Quasar form validation
   - Show clear error messages
   - Disable save button while submitting

4. **Permissions:**
   - Check `fuseStore.canModify` before showing edit/delete/create actions
   - Check `fuseStore.canRead` before showing data

## Example Code Patterns

### Query Pattern
```typescript
import { useQuery, useMutation, useQueryClient } from '@tanstack/vue-query'
import { useFuseClient } from '@/composables/useFuseClient'

const client = useFuseClient()
const queryClient = useQueryClient()

const { data, isLoading } = useQuery({
  queryKey: ['positions'],
  queryFn: () => client.positionAll()
})
```

### Mutation Pattern
```typescript
const deleteMutation = useMutation({
  mutationFn: (id: string) => client.positionDELETE(id),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['positions'] })
    Notify.create({ type: 'positive', message: 'Position deleted' })
  },
  onError: (error) => {
    Notify.create({ type: 'negative', message: error.message })
  }
})
```

### Dialog Pattern
```typescript
const isDialogOpen = ref(false)
const selectedItem = ref<Position | undefined>()

function openCreateDialog() {
  selectedItem.value = undefined
  isDialogOpen.value = true
}

function openEditDialog(item: Position) {
  selectedItem.value = item
  isDialogOpen.value = true
}
```

## Notes

- The backend API uses camelCase for JSON properties (not PascalCase)
- All GUIDs should be displayed as uppercase
- Use Quasar's built-in components and styling
- Follow existing patterns from ApplicationsPage, AccountsPage, etc.
- Use TanStack Vue Query for all data fetching
- Keep components focused and reusable
