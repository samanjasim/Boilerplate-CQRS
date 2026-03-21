import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader, EmptyState } from '@/components/common';
import { useUsers } from '../api';
import { ROUTES } from '@/config';
import { Users } from 'lucide-react';
import { format } from 'date-fns';

export default function UsersListPage() {
  const { t } = useTranslation();
  const { data, isLoading, isError } = useUsers();

  const users = data?.data ?? [];

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('users.title')} />
        <EmptyState icon={Users} title={t('common.errorOccurred')} description={t('common.tryAgain')} />
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader title={t('users.title')} subtitle={t('users.allUsers')} />

      {users.length === 0 ? (
        <EmptyState icon={Users} title={t('common.noResults')} />
      ) : (
        <Card>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('users.userName')}</TableHead>
                  <TableHead>{t('users.userEmail')}</TableHead>
                  <TableHead>{t('users.userRoles')}</TableHead>
                  <TableHead>{t('users.userCreated')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {users.map((user) => (
                  <TableRow key={user.id}>
                    <TableCell>
                      <Link to={ROUTES.USERS.getDetail(user.id)} className="font-medium text-foreground hover:text-primary">
                        {user.firstName} {user.lastName}
                      </Link>
                      <p className="text-xs text-muted-foreground">@{user.username}</p>
                    </TableCell>
                    <TableCell className="text-muted-foreground">{user.email}</TableCell>
                    <TableCell>
                      <div className="flex flex-wrap gap-1">
                        {user.roles?.map((role) => (
                          <Badge key={role} variant="secondary">{role}</Badge>
                        )) || <span className="text-muted-foreground">-</span>}
                      </div>
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {user.createdAt ? format(new Date(user.createdAt), 'MMM d, yyyy') : '-'}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
