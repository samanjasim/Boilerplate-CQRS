import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader, EmptyState, ExportButton } from '@/components/common';
import { useUsers } from '../api';
import { useInvitations, useRevokeInvitation } from '@/features/auth/api';
import { InviteUserModal } from '../components';
import { ROUTES } from '@/config';
import { Users, UserPlus, X, Mail, Clock } from 'lucide-react';
import { formatDate } from '@/utils/format';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants';

export default function UsersListPage() {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();
  const canExport = hasPermission(PERMISSIONS.System.ExportData);
  const { data, isLoading, isError } = useUsers();
  const { data: invitationsData } = useInvitations();
  const { mutate: revokeInvitation } = useRevokeInvitation();
  const [inviteModalOpen, setInviteModalOpen] = useState(false);

  const users = data?.data ?? [];
  const invitations = invitationsData?.data ?? [];
  const pendingInvitations = invitations.filter(
    (inv) => !inv.isAccepted && new Date(inv.expiresAt) > new Date()
  );

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('users.title')} />
        <EmptyState icon={Users} title={t('common.errorOccurred')} description={t('common.tryAgain')} />
      </div>
    );
  }

  if (isLoading && !data) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('users.title')}
        subtitle={t('users.allUsers')}
        actions={
          <div className="flex items-center gap-2">
            {canExport && <ExportButton reportType="Users" />}
            <Button onClick={() => setInviteModalOpen(true)}>
              <UserPlus className="mr-2 h-4 w-4" />
              {t('invitations.inviteUser')}
            </Button>
          </div>
        }
      />

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
                      {user.createdAt ? formatDate(user.createdAt) : '-'}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      {/* Pending Invitations Section */}
      {pendingInvitations.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-lg">
              <Mail className="h-5 w-5" />
              {t('invitations.pendingInvitations')}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('common.email')}</TableHead>
                  <TableHead>{t('invitations.role')}</TableHead>
                  <TableHead>{t('invitations.invitedBy')}</TableHead>
                  <TableHead>{t('invitations.expiresAt')}</TableHead>
                  <TableHead>{t('common.actions')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {pendingInvitations.map((invitation) => (
                  <TableRow key={invitation.id}>
                    <TableCell className="font-medium">{invitation.email}</TableCell>
                    <TableCell>
                      <Badge variant="outline">{invitation.roleName}</Badge>
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {invitation.invitedByName}
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      <div className="flex items-center gap-1">
                        <Clock className="h-3 w-3" />
                        {formatDate(invitation.expiresAt)}
                      </div>
                    </TableCell>
                    <TableCell>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => revokeInvitation(invitation.id)}
                      >
                        <X className="mr-1 h-4 w-4" />
                        {t('invitations.revoke')}
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      <InviteUserModal open={inviteModalOpen} onOpenChange={setInviteModalOpen} />
    </div>
  );
}
