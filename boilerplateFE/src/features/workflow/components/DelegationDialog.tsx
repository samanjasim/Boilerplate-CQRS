import { useState, useDeferredValue } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Spinner } from '@/components/ui/spinner';
import { useSearchUsers } from '@/features/users/api';
import { useCreateDelegation } from '../api';

interface DelegationDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function DelegationDialog({ open, onOpenChange }: DelegationDialogProps) {
  const { t } = useTranslation();
  const [toUserId, setToUserId] = useState('');
  const [searchTerm, setSearchTerm] = useState('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const { mutate: createDelegation, isPending } = useCreateDelegation();

  const deferredSearch = useDeferredValue(searchTerm);
  const { data: usersData, isLoading: usersLoading } = useSearchUsers(
    { searchTerm: deferredSearch, pageSize: 20, status: 'Active' },
    { enabled: open },
  );

  const users = usersData?.data ?? [];

  const handleConfirm = () => {
    if (!toUserId || !startDate || !endDate) return;
    createDelegation(
      { toUserId, startDate, endDate },
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

  const selectedUser = users.find((u) => u.id === toUserId);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('workflow.delegation.title')}</DialogTitle>
          <DialogDescription>
            {t('workflow.delegation.description', 'Delegate your pending tasks to another user for a specific period.')}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="delegation-user">{t('workflow.delegation.delegateTo')}</Label>
            <Input
              id="delegation-user-search"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder={t('workflow.delegation.searchPlaceholder', 'Search users by name or email...')}
              className="mb-2"
            />
            {usersLoading ? (
              <div className="flex justify-center py-3">
                <Spinner size="sm" />
              </div>
            ) : (
              <Select value={toUserId} onValueChange={setToUserId}>
                <SelectTrigger id="delegation-user">
                  <SelectValue placeholder={t('workflow.delegation.selectUser', 'Select a user')}>
                    {selectedUser
                      ? `${selectedUser.firstName} ${selectedUser.lastName} (${selectedUser.email})`
                      : undefined}
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  {users.length === 0 ? (
                    <div className="px-3 py-2 text-sm text-muted-foreground">
                      {t('workflow.delegation.noUsersFound', 'No users found')}
                    </div>
                  ) : (
                    users.map((user) => (
                      <SelectItem key={user.id} value={user.id}>
                        {user.firstName} {user.lastName} ({user.email})
                      </SelectItem>
                    ))
                  )}
                </SelectContent>
              </Select>
            )}
          </div>

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
            {t('workflow.delegation.confirm')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
