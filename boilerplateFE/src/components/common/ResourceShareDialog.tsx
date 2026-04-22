import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Copy, Check } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
  DialogDescription,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ConfirmDialog } from './ConfirmDialog';
import { SubjectPicker } from './SubjectPicker';
import { SubjectStack } from './SubjectStack';
import { VisibilityBadge } from './VisibilityBadge';
import {
  useResourceGrants,
  useGrantResourceAccess,
  useRevokeResourceGrant,
  useSetResourceVisibility,
} from '@/features/access/api/access.queries';
import { usePermissions } from '@/hooks';
import { PERMISSIONS } from '@/constants/permissions';
import type { ResourceType, ResourceVisibility, AccessLevel, GrantSubjectType } from '@/features/access/types';

const MAX_VISIBILITY: Record<ResourceType, ResourceVisibility> = {
  File: 'Public',
  AiAssistant: 'TenantWide',
};

const VISIBILITY_OPTIONS: ResourceVisibility[] = ['Private', 'TenantWide', 'Public'];

type Props = {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  resourceType: ResourceType;
  resourceId: string;
  resourceName: string;
  currentVisibility: ResourceVisibility;
  fileId?: string;
};

export function ResourceShareDialog({
  open,
  onOpenChange,
  resourceType,
  resourceId,
  resourceName,
  currentVisibility,
  fileId,
}: Props) {
  const { t } = useTranslation();
  const { hasPermission } = usePermissions();

  const [pendingVisibility, setPendingVisibility] = useState<ResourceVisibility | null>(null);
  const [confirmPublicOpen, setConfirmPublicOpen] = useState(false);
  const [confirmPublicCheck, setConfirmPublicCheck] = useState(false);
  const [selectedLevel, setSelectedLevel] = useState<AccessLevel>('Viewer');
  const [revokeId, setRevokeId] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  const { data: grants = [] } = useResourceGrants(resourceType, resourceId);
  const grantMutation = useGrantResourceAccess(resourceType, resourceId);
  const revokeMutation = useRevokeResourceGrant(resourceType, resourceId);
  const visibilityMutation = useSetResourceVisibility(resourceType, resourceId);

  const maxVis = MAX_VISIBILITY[resourceType];
  const canSetPublic = maxVis === 'Public' && hasPermission(PERMISSIONS.Files.Manage);

  const handleVisibilityChange = (v: ResourceVisibility) => {
    if (v === 'Public') {
      setPendingVisibility(v);
      setConfirmPublicOpen(true);
    } else {
      visibilityMutation.mutate(v);
    }
  };

  const handlePublicConfirm = () => {
    if (!confirmPublicCheck || !pendingVisibility) return;
    visibilityMutation.mutate(pendingVisibility);
    setConfirmPublicOpen(false);
    setConfirmPublicCheck(false);
    setPendingVisibility(null);
  };

  const handleCopyLink = async () => {
    if (!fileId) return;
    const url = `${window.location.origin}/files/${fileId}`;
    await navigator.clipboard.writeText(url);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleGrant = (s: { type: 'User' | 'Role'; id: string }) => {
    grantMutation.mutate({
      subjectType: s.type as GrantSubjectType,
      subjectId: s.id,
      level: selectedLevel,
    });
  };

  return (
    <>
      <Dialog open={open} onOpenChange={onOpenChange}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle className="truncate">
              {t('access.shareDialog.title', { name: resourceName })}
            </DialogTitle>
          </DialogHeader>

          <div className="space-y-5">
            {/* Visibility */}
            <div className="space-y-2">
              <p className="text-sm font-medium">{t('access.shareDialog.visibility')}</p>
              <div className="flex gap-2 flex-wrap">
                {VISIBILITY_OPTIONS.map(v => {
                  const disabled = v === 'Public' && !canSetPublic;
                  return (
                    <Button
                      key={v}
                      variant={currentVisibility === v ? 'default' : 'outline'}
                      size="sm"
                      disabled={disabled}
                      onClick={() => handleVisibilityChange(v)}
                    >
                      <VisibilityBadge visibility={v} />
                    </Button>
                  );
                })}
              </div>

              {currentVisibility === 'Public' && fileId && (
                <Button variant="outline" size="sm" className="gap-2 mt-1" onClick={handleCopyLink}>
                  {copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
                  {t('access.shareDialog.copyLink')}
                </Button>
              )}
            </div>

            {/* Add grantee */}
            <div className="space-y-2">
              <p className="text-sm font-medium">{t('access.shareDialog.addPeople')}</p>
              <div className="flex items-end gap-2">
                <div className="flex-1">
                  <SubjectPicker onSelect={handleGrant} />
                </div>
                <Select
                  value={selectedLevel}
                  onValueChange={v => setSelectedLevel(v as AccessLevel)}
                >
                  <SelectTrigger className="w-28">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Viewer">{t('access.levels.viewer')}</SelectItem>
                    <SelectItem value="Editor">{t('access.levels.editor')}</SelectItem>
                    <SelectItem value="Manager">{t('access.levels.manager')}</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>

            {/* Grant list */}
            {grants.length > 0 && (
              <div className="space-y-1">
                <p className="text-sm font-medium">{t('access.shareDialog.whoHasAccess')}</p>
                <ul className="divide-y rounded-md border text-sm">
                  {grants.map(g => (
                    <li key={g.id} className="flex items-center justify-between px-3 py-2 gap-2">
                      <SubjectStack
                        subjects={[{
                          type: g.subjectType,
                          id: g.subjectId,
                          name: g.subjectDisplayName ?? g.subjectId,
                        }]}
                      />
                      <div className="flex items-center gap-2 shrink-0">
                        <span className="text-xs text-muted-foreground capitalize">
                          {g.level.toLowerCase()}
                        </span>
                        <Button
                          variant="ghost"
                          size="sm"
                          className="h-7 text-destructive hover:text-destructive"
                          onClick={() => setRevokeId(g.id)}
                        >
                          {t('common.remove')}
                        </Button>
                      </div>
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              {t('common.close')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Confirm make-public — custom dialog to include acknowledge checkbox */}
      <Dialog open={confirmPublicOpen} onOpenChange={open => {
        setConfirmPublicOpen(open);
        if (!open) { setPendingVisibility(null); setConfirmPublicCheck(false); }
      }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('access.shareDialog.confirmPublic.title')}</DialogTitle>
            <DialogDescription>{t('access.shareDialog.confirmPublic.body')}</DialogDescription>
          </DialogHeader>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={confirmPublicCheck}
              onChange={e => setConfirmPublicCheck(e.target.checked)}
              className="h-4 w-4"
            />
            {t('access.shareDialog.confirmPublic.acknowledge')}
          </label>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmPublicOpen(false)}>
              {t('common.cancel')}
            </Button>
            <Button onClick={handlePublicConfirm} disabled={!confirmPublicCheck}>
              {t('access.shareDialog.confirmPublic.confirm')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Confirm revoke */}
      <ConfirmDialog
        isOpen={!!revokeId}
        onClose={() => setRevokeId(null)}
        title={t('access.revokeDialog.title')}
        description={t('access.revokeDialog.description')}
        variant="danger"
        onConfirm={() => {
          if (revokeId) revokeMutation.mutate(revokeId);
          setRevokeId(null);
        }}
      />
    </>
  );
}
