import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useUpdateUser } from '../api';
import { updateUserSchema, type UpdateUserFormData } from '@/lib/validation';

interface EditUserModalProps {
  isOpen: boolean;
  onClose: () => void;
  user: {
    id: string;
    firstName: string;
    lastName: string;
    email: string;
    phoneNumber?: string | null;
    username: string;
  };
}

export function EditUserModal({ isOpen, onClose, user }: EditUserModalProps) {
  const { t } = useTranslation();
  const { mutate: updateUser, isPending } = useUpdateUser();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<UpdateUserFormData>({
    resolver: zodResolver(updateUserSchema(t)),
    defaultValues: {
      firstName: user.firstName,
      lastName: user.lastName,
      email: user.email,
      phoneNumber: user.phoneNumber ?? '',
    },
  });

  const onSubmit = (data: UpdateUserFormData) => {
    const payload = {
      ...data,
      phoneNumber: data.phoneNumber || null,
    };
    updateUser({ id: user.id, data: payload }, { onSuccess: onClose });
  };

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('users.editUser')}</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div className="flex items-center gap-2 rounded-lg border bg-muted px-4 py-3">
            <span className="text-sm text-muted-foreground">@{user.username}</span>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>{t('common.firstName')}</Label>
              <Input {...register('firstName')} />
              {errors.firstName && <p className="text-sm text-destructive">{errors.firstName.message}</p>}
            </div>
            <div className="space-y-2">
              <Label>{t('common.lastName')}</Label>
              <Input {...register('lastName')} />
              {errors.lastName && <p className="text-sm text-destructive">{errors.lastName.message}</p>}
            </div>
          </div>
          <div className="space-y-2">
            <Label>{t('common.email')}</Label>
            <Input type="email" {...register('email')} />
            {errors.email && <p className="text-sm text-destructive">{errors.email.message}</p>}
          </div>
          <div className="space-y-2">
            <Label>{t('common.phoneNumber')}</Label>
            <Input placeholder="+9647XXXXXXXXX" {...register('phoneNumber')} />
            {errors.phoneNumber && <p className="text-sm text-destructive">{errors.phoneNumber.message}</p>}
          </div>
          <DialogFooter>
            <Button variant="outline" type="button" onClick={onClose}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={isPending}>
              {isPending ? t('common.saving') : t('common.save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
