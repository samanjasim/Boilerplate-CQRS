import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Building, Users, ArrowRight, Check } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { FileUpload } from '@/components/common';
import { useAuthStore, selectUser } from '@/stores';
import { useUpdateTenantBranding } from '@/features/tenants/api';
import { useInviteUser } from '@/features/auth/api';
import { useRoles } from '@/features/roles/api';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';

interface OnboardingWizardProps {
  onComplete: () => void;
  /**
   * Optional. When provided, a "Remind me later" button shows on the first
   * step and dismisses the wizard for 24h without marking the tenant
   * onboarded.
   */
  onRemindLater?: () => void;
}

const STEPS = ['profile', 'team', 'done'] as const;
type Step = typeof STEPS[number];
const PROGRESS_KEY = 'onboarding-wizard-step';

function readPersistedStep(): Step {
  try {
    const raw = sessionStorage.getItem(PROGRESS_KEY);
    if (raw && (STEPS as readonly string[]).includes(raw)) return raw as Step;
  } catch {
    /* ignore */
  }
  return 'profile';
}

export function OnboardingWizard({ onComplete, onRemindLater }: OnboardingWizardProps) {
  const { t } = useTranslation();
  const user = useAuthStore(selectUser);
  const [currentStep, setCurrentStepState] = useState<Step>(readPersistedStep);

  const setCurrentStep = (step: Step) => {
    setCurrentStepState(step);
    try {
      sessionStorage.setItem(PROGRESS_KEY, step);
    } catch {
      /* ignore */
    }
  };

  const handleComplete = () => {
    try {
      sessionStorage.removeItem(PROGRESS_KEY);
    } catch {
      /* ignore */
    }
    onComplete();
  };

  // Step 1: Profile
  const [description, setDescription] = useState('');
  const [uploadedLogoId, setUploadedLogoId] = useState<string | null>(null);
  const { mutate: updateBranding, isPending: isSavingBranding } = useUpdateTenantBranding();

  // Step 2: Team
  const [invites, setInvites] = useState([{ email: '', roleId: '' }]);
  const { data: rolesData } = useRoles();
  const roles = rolesData?.data ?? [];
  const { mutate: inviteUser } = useInviteUser();

  const handleSaveProfile = () => {
    if (!user?.tenantId) return;
    updateBranding(
      { id: user.tenantId, data: { description, logoFileId: uploadedLogoId } },
      { onSuccess: () => setCurrentStep('team') },
    );
  };

  const handleInviteTeam = () => {
    const validInvites = invites.filter(inv => inv.email && inv.roleId);
    if (validInvites.length === 0) {
      setCurrentStep('done');
      return;
    }

    let completed = 0;
    let failed = 0;
    for (const inv of validInvites) {
      inviteUser(
        { email: inv.email, roleId: inv.roleId, tenantId: user?.tenantId },
        {
          onSuccess: () => {
            completed++;
            if (completed + failed >= validInvites.length) setCurrentStep('done');
          },
          onError: () => {
            failed++;
            if (completed + failed >= validInvites.length) setCurrentStep('done');
          },
        },
      );
    }
  };

  const addInviteRow = () => {
    setInvites([...invites, { email: '', roleId: '' }]);
  };

  const updateInvite = (index: number, field: 'email' | 'roleId', value: string) => {
    const updated = [...invites];
    updated[index] = { ...updated[index], [field]: value };
    setInvites(updated);
  };

  const stepIndex = STEPS.indexOf(currentStep);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-background">
      <div className="w-full max-w-lg px-6">
        {/* Progress indicator */}
        <div className="mb-8 flex items-center justify-center gap-2">
          {STEPS.map((step, i) => (
            <div
              key={step}
              className={`h-2 w-12 rounded-full transition-colors ${
                i <= stepIndex ? 'bg-primary' : 'bg-border'
              }`}
            />
          ))}
        </div>

        {/* Step 1: Organization Profile */}
        {currentStep === 'profile' && (
          <div className="space-y-6">
            <div className="text-center">
              <div className="mb-4 inline-flex h-14 w-14 items-center justify-center rounded-2xl bg-primary/10">
                <Building className="h-7 w-7 [color:var(--active-text)]" />
              </div>
              <h2 className="text-2xl font-bold tracking-tight">{t('onboarding.profileTitle')}</h2>
              <p className="mt-1 text-sm text-muted-foreground">{t('onboarding.profileDesc')}</p>
            </div>

            <div className="space-y-4">
              <div>
                <Label>{t('onboarding.companyName')}</Label>
                <Input value={user?.tenantName ?? ''} disabled className="mt-1" />
              </div>
              <div>
                <Label>{t('onboarding.logo')}</Label>
                <div className="mt-1">
                  <FileUpload
                    accept="image/*"
                    maxSize={2 * 1024 * 1024}
                    mode="temp"
                    onUploaded={(fileId: string) => setUploadedLogoId(fileId)}
                  />
                </div>
              </div>
              <div>
                <Label>{t('onboarding.description')}</Label>
                <Textarea
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder={t('onboarding.descriptionPlaceholder')}
                  className="mt-1"
                  rows={3}
                />
              </div>
            </div>

            <div className="flex flex-wrap items-center justify-between gap-2">
              <Button variant="ghost" onClick={handleComplete}>
                {t('onboarding.skipAll')}
              </Button>
              <div className="flex items-center gap-2">
                {onRemindLater && (
                  <Button variant="outline" onClick={onRemindLater}>
                    {t('onboarding.remindLater')}
                  </Button>
                )}
                <Button onClick={handleSaveProfile} disabled={isSavingBranding}>
                  {t('common.next')} <ArrowRight className="h-4 w-4" />
                </Button>
              </div>
            </div>
          </div>
        )}

        {/* Step 2: Invite Team */}
        {currentStep === 'team' && (
          <div className="space-y-6">
            <div className="text-center">
              <div className="mb-4 inline-flex h-14 w-14 items-center justify-center rounded-2xl bg-primary/10">
                <Users className="h-7 w-7 [color:var(--active-text)]" />
              </div>
              <h2 className="text-2xl font-bold tracking-tight">{t('onboarding.teamTitle')}</h2>
              <p className="mt-1 text-sm text-muted-foreground">{t('onboarding.teamDesc')}</p>
            </div>

            <div className="space-y-3">
              {invites.map((inv, i) => (
                <div key={i} className="flex gap-2">
                  <Input
                    type="email"
                    placeholder={t('onboarding.emailPlaceholder')}
                    value={inv.email}
                    onChange={(e) => updateInvite(i, 'email', e.target.value)}
                    className="flex-1"
                  />
                  <Select value={inv.roleId} onValueChange={(v) => updateInvite(i, 'roleId', v)}>
                    <SelectTrigger className="w-36">
                      <SelectValue placeholder={t('onboarding.role')} />
                    </SelectTrigger>
                    <SelectContent>
                      {roles.filter(r => !r.isSystemRole || r.name === 'Admin').map((role) => (
                        <SelectItem key={role.id} value={role.id}>{role.name}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              ))}
              <Button variant="outline" size="sm" onClick={addInviteRow}>
                + {t('onboarding.addAnother')}
              </Button>
            </div>

            <div className="flex justify-between">
              <Button variant="ghost" onClick={() => setCurrentStep('done')}>
                {t('onboarding.skip')}
              </Button>
              <Button onClick={handleInviteTeam}>
                {t('onboarding.sendInvites')} <ArrowRight className="h-4 w-4" />
              </Button>
            </div>
          </div>
        )}

        {/* Step 3: Done */}
        {currentStep === 'done' && (
          <div className="space-y-6 text-center">
            <div className="mb-4 inline-flex h-14 w-14 items-center justify-center rounded-2xl bg-green-500/10">
              <Check className="h-7 w-7 text-green-600" />
            </div>
            <h2 className="text-2xl font-bold tracking-tight">{t('onboarding.doneTitle')}</h2>
            <p className="text-sm text-muted-foreground">{t('onboarding.doneDesc')}</p>
            <Button size="lg" onClick={handleComplete}>
              {t('onboarding.goToDashboard')}
            </Button>
          </div>
        )}
      </div>
    </div>
  );
}
