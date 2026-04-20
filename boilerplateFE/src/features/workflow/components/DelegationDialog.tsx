import { useState, useDeferredValue } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Spinner } from '@/components/ui/spinner';
import { useSearchUsers } from '@/features/users/api';
import { useCreateDelegation } from '../api';
import { formatDelegationDates } from './delegation-serializer';

interface DelegationDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function DelegationDialog({ open, onOpenChange }: DelegationDialogProps) {
  const { t } = useTranslation();
  const [toUserId, setToUserId] = useState('');
  const [searchTerm, setSearchTerm] = useState('');
  const [showDropdown, setShowDropdown] = useState(false);
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const { mutate: createDelegation, isPending } = useCreateDelegation();

  const deferredSearch = useDeferredValue(searchTerm);
  const { data: usersData, isLoading: usersLoading } = useSearchUsers(
    { searchTerm: deferredSearch, pageSize: 10, status: 'Active' },
    { enabled: open && searchTerm.length > 0 },
  );

  const users = usersData?.data ?? [];

  const handleSelectUser = (userId: string, displayName: string) => {
    setToUserId(userId);
    setSearchTerm(displayName);
    setShowDropdown(false);
  };

  const handleSearchChange = (value: string) => {
    setSearchTerm(value);
    setToUserId('');
    setShowDropdown(value.length > 0);
  };

  const handleConfirm = () => {
    if (!toUserId || !startDate || !endDate) return;
    const { startDate: startIso, endDate: endIso } = formatDelegationDates(startDate, endDate);
    createDelegation(
      { toUserId, startDate: startIso, endDate: endIso },
      {
        onSuccess: () => {
          setToUserId('');
          setSearchTerm('');
          setStartDate('');
          setEndDate('');
          onOpenChange(false);
        },
      },
    );
  };

  const handleOpenChange = (isOpen: boolean) => {
    if (!isOpen) {
      setToUserId('');
      setSearchTerm('');
      setStartDate('');
      setEndDate('');
      setShowDropdown(false);
    }
    onOpenChange(isOpen);
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('workflow.delegation.title')}</DialogTitle>
          <DialogDescription>
            {t('workflow.delegation.description', 'Delegate your pending tasks to another user for a specific period.')}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {/* Single autocomplete field */}
          <div className="space-y-1.5">
            <Label>{t('workflow.delegation.delegateTo')}</Label>
            <div className="relative">
              <Input
                value={searchTerm}
                onChange={(e) => handleSearchChange(e.target.value)}
                onFocus={() => searchTerm.length > 0 && setShowDropdown(true)}
                placeholder={t('workflow.delegation.searchPlaceholder', 'Type a name or email to search...')}
                autoComplete="off"
              />
              {toUserId && (
                <span className="absolute end-3 top-1/2 -translate-y-1/2 text-xs text-primary">✓</span>
              )}

              {/* Dropdown results */}
              {showDropdown && (
                <div className="absolute z-50 mt-1 w-full rounded-lg border bg-popover shadow-lg max-h-48 overflow-y-auto">
                  {usersLoading ? (
                    <div className="flex justify-center py-3">
                      <Spinner size="sm" />
                    </div>
                  ) : users.length === 0 ? (
                    <div className="px-3 py-3 text-sm text-muted-foreground text-center">
                      {searchTerm.length < 2
                        ? t('workflow.delegation.typeToSearch', 'Type at least 2 characters to search')
                        : t('workflow.delegation.noUsersFound', 'No users found')}
                    </div>
                  ) : (
                    users.map((user) => {
                      const displayName = `${user.firstName} ${user.lastName}`;
                      return (
                        <button
                          key={user.id}
                          type="button"
                          className="w-full text-start px-3 py-2.5 text-sm hover:bg-muted/50 transition-colors flex items-center justify-between"
                          onClick={() => handleSelectUser(user.id, displayName)}
                        >
                          <div>
                            <span className="font-medium text-foreground">{displayName}</span>
                            <span className="text-muted-foreground ms-2">{user.email}</span>
                          </div>
                          {user.id === toUserId && (
                            <span className="text-primary text-xs">✓</span>
                          )}
                        </button>
                      );
                    })
                  )}
                </div>
              )}
            </div>
          </div>

          {/* Date range */}
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <Label htmlFor="delegation-start">{t('workflow.delegation.startDate')}</Label>
              <Input
                id="delegation-start"
                type="date"
                value={startDate}
                onChange={(e) => setStartDate(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="delegation-end">{t('workflow.delegation.endDate')}</Label>
              <Input
                id="delegation-end"
                type="date"
                value={endDate}
                onChange={(e) => setEndDate(e.target.value)}
              />
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button
            onClick={handleConfirm}
            disabled={isPending || !toUserId || !startDate || !endDate}
          >
            {isPending ? t('common.saving') : t('workflow.delegation.confirm')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
