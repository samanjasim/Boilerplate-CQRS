import { useMemo, useCallback } from 'react';
import { Check, Minus } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { Permission as PermissionEntity } from '@/types';

interface PermissionMatrixProps {
  allPermissions: PermissionEntity[];
  selectedIds: Set<string>;
  onChange: (selectedIds: Set<string>) => void;
  disabled?: boolean;
}

interface ModuleGroup {
  module: string;
  permissions: PermissionEntity[];
}

export function PermissionMatrix({
  allPermissions,
  selectedIds,
  onChange,
  disabled = false,
}: PermissionMatrixProps) {
  const groupedPermissions = useMemo<ModuleGroup[]>(() => {
    const map = new Map<string, PermissionEntity[]>();
    for (const perm of allPermissions) {
      const module = perm.module || 'Other';
      if (!map.has(module)) map.set(module, []);
      map.get(module)!.push(perm);
    }
    return Array.from(map.entries())
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([module, permissions]) => ({ module, permissions }));
  }, [allPermissions]);

  const togglePermission = useCallback(
    (id: string) => {
      if (disabled) return;
      const next = new Set(selectedIds);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      onChange(next);
    },
    [selectedIds, onChange, disabled]
  );

  const toggleModule = useCallback(
    (group: ModuleGroup) => {
      if (disabled) return;
      const moduleIds = group.permissions.map((p) => p.id);
      const allSelected = moduleIds.every((id) => selectedIds.has(id));
      const next = new Set(selectedIds);
      if (allSelected) {
        moduleIds.forEach((id) => next.delete(id));
      } else {
        moduleIds.forEach((id) => next.add(id));
      }
      onChange(next);
    },
    [selectedIds, onChange, disabled]
  );

  const toggleAll = useCallback(() => {
    if (disabled) return;
    const allIds = allPermissions.map((p) => p.id);
    const allSelected = allIds.every((id) => selectedIds.has(id));
    onChange(allSelected ? new Set() : new Set(allIds));
  }, [allPermissions, selectedIds, onChange, disabled]);

  const allSelected = allPermissions.length > 0 && allPermissions.every((p) => selectedIds.has(p.id));
  const someSelected = allPermissions.some((p) => selectedIds.has(p.id)) && !allSelected;

  return (
    <div className="space-y-1">
      {/* Select All Header */}
      <div className="flex items-center justify-between rounded-lg bg-muted px-4 py-3 border">
        <div className="flex items-center gap-3">
          <Checkbox
            checked={allSelected}
            indeterminate={someSelected}
            onChange={toggleAll}
            disabled={disabled}
          />
          <span className="text-sm font-semibold text-foreground">
            Select All Permissions
          </span>
        </div>
        <span className="text-xs text-muted-foreground">
          {selectedIds.size} / {allPermissions.length} selected
        </span>
      </div>

      {/* Module Groups */}
      <div className="space-y-1">
        {groupedPermissions.map((group) => {
          const moduleIds = group.permissions.map((p) => p.id);
          const moduleAllSelected = moduleIds.every((id) => selectedIds.has(id));
          const moduleSomeSelected = moduleIds.some((id) => selectedIds.has(id)) && !moduleAllSelected;

          return (
            <div key={group.module} className="rounded-lg border overflow-hidden">
              {/* Module Header */}
              <div
                className={cn(
                  'flex items-center gap-3 px-4 py-2.5 bg-muted/50 cursor-pointer select-none',
                  disabled && 'cursor-not-allowed opacity-60'
                )}
                onClick={() => toggleModule(group)}
              >
                <Checkbox
                  checked={moduleAllSelected}
                  indeterminate={moduleSomeSelected}
                  onChange={() => toggleModule(group)}
                  disabled={disabled}
                />
                <span className="text-sm font-medium text-foreground">{group.module}</span>
                <span className="text-xs text-muted-foreground">
                  ({moduleIds.filter((id) => selectedIds.has(id)).length}/{moduleIds.length})
                </span>
              </div>

              {/* Permission Items */}
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-0 divide-y sm:divide-y-0">
                {group.permissions.map((perm) => (
                  <label
                    key={perm.id}
                    className={cn(
                      'flex items-center gap-3 px-4 py-2.5 text-sm transition-colors',
                      !disabled && 'cursor-pointer hover:bg-muted/30',
                      disabled && 'cursor-not-allowed opacity-60',
                      selectedIds.has(perm.id) && 'bg-primary/5'
                    )}
                  >
                    <Checkbox
                      checked={selectedIds.has(perm.id)}
                      onChange={() => togglePermission(perm.id)}
                      disabled={disabled}
                    />
                    <div className="min-w-0">
                      <span className="text-foreground font-medium">
                        {formatPermissionLabel(perm.name)}
                      </span>
                      {perm.description && (
                        <p className="text-xs text-muted-foreground truncate">{perm.description}</p>
                      )}
                    </div>
                  </label>
                ))}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

// --- Checkbox sub-component ---

interface CheckboxProps {
  checked: boolean;
  indeterminate?: boolean;
  onChange: () => void;
  disabled?: boolean;
}

function Checkbox({ checked, indeterminate, onChange, disabled }: CheckboxProps) {
  return (
    <button
      type="button"
      role="checkbox"
      aria-checked={indeterminate ? 'mixed' : checked}
      onClick={(e) => {
        e.stopPropagation();
        if (!disabled) onChange();
      }}
      disabled={disabled}
      className={cn(
        'flex h-4 w-4 shrink-0 items-center justify-center rounded border transition-colors',
        checked || indeterminate
          ? 'border-primary bg-primary text-primary-foreground'
          : 'border-input bg-background hover:border-primary',
        disabled && 'opacity-50 cursor-not-allowed'
      )}
    >
      {checked && <Check className="h-3 w-3" strokeWidth={3} />}
      {indeterminate && !checked && <Minus className="h-3 w-3" strokeWidth={3} />}
    </button>
  );
}

// --- Helpers ---

function formatPermissionLabel(name: string): string {
  // "Users.ManageRoles" -> "Manage Roles"
  const action = name.split('.').pop() || name;
  return action.replace(/([A-Z])/g, ' $1').trim();
}
