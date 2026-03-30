import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import { useRoles, useAssignUserRole } from '@/features/roles/api';

interface RoleAssignModalProps {
  isOpen: boolean;
  onClose: () => void;
  userId: string;
  currentRoles: string[];
  onSuccess?: () => void;
}

export function RoleAssignModal({
  isOpen,
  onClose,
  userId,
  currentRoles,
  onSuccess,
}: RoleAssignModalProps) {
  const { t } = useTranslation();
  const { data: rolesData, isLoading } = useRoles({ enabled: isOpen });
  const { mutate: assignRole, isPending } = useAssignUserRole();
  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null);

  const roles = rolesData?.data ?? [];
  // Filter out roles the user already has
  const availableRoles = roles.filter(
    (role) => role.isActive && !currentRoles.includes(role.id)
  );

  const handleAssign = () => {
    if (!selectedRoleId) return;
    assignRole(
      { roleId: selectedRoleId, userId },
      {
        onSuccess: () => {
          setSelectedRoleId(null);
          onSuccess?.();
          onClose();
        },
      }
    );
  };

  const handleClose = () => {
    setSelectedRoleId(null);
    onClose();
  };

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && handleClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('users.assignRole')}</DialogTitle>
        </DialogHeader>

        {isLoading ? (
          <div className="flex justify-center py-6">
            <Spinner />
          </div>
        ) : availableRoles.length === 0 ? (
          <p className="py-4 text-sm text-muted-foreground text-center">
            {t('users.noRolesAvailable')}
          </p>
        ) : (
          <div className="space-y-1">
            {availableRoles.map((role) => (
              <label
                key={role.id}
                className={`flex items-center gap-3 rounded-lg px-3 py-2.5 cursor-pointer transition-colors ${
                  selectedRoleId === role.id
                    ? 'bg-primary/10 border border-primary/30'
                    : 'hover:bg-muted border border-transparent'
                }`}
              >
                <input
                  type="radio"
                  name="role"
                  value={role.id}
                  checked={selectedRoleId === role.id}
                  onChange={() => setSelectedRoleId(role.id)}
                  className="h-4 w-4 text-primary accent-primary"
                />
                <div>
                  <p className="text-sm font-medium text-foreground">{role.name}</p>
                  {role.description && (
                    <p className="text-xs text-muted-foreground">{role.description}</p>
                  )}
                </div>
              </label>
            ))}
          </div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={handleClose} disabled={isPending}>
            Cancel
          </Button>
          <Button
            onClick={handleAssign}
            disabled={isPending || !selectedRoleId}
          >
            {isPending ? t('common.loading') : t('users.assignRole')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
