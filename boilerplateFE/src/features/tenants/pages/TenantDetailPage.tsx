import { useState, useCallback, useMemo, useEffect } from 'react';
import { useParams, useLocation, Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  UserCheck,
  Ban,
  UserX,
  Save,
  Trash2,
} from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Spinner } from '@/components/ui/spinner';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader, InfoField, ConfirmDialog, FileUpload } from '@/components/common';
import { cn } from '@/lib/utils';
import { useAuthStore, selectUser } from '@/stores';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  useTenant,
  useActivateTenant,
  useSuspendTenant,
  useDeactivateTenant,
  useUpdateTenantBranding,
  useUpdateTenantBusinessInfo,
  useUpdateTenantCustomText,
  useSetTenantDefaultRole,
} from '../api';
import { useAssignableRoles } from '@/features/roles/api';
import { useBackNavigation } from '@/hooks';
import { ROUTES } from '@/config';
import { formatDate } from '@/utils/format';
import { toast } from 'sonner';

import { STATUS_BADGE_VARIANT, PERMISSIONS } from '@/constants';
import { usePermissions } from '@/hooks';
import { TenantFeatureFlagsTab } from '../components/TenantFeatureFlagsTab';
import { ActivityTab } from '../components/ActivityTab';
import { SubscriptionTab } from '../components/SubscriptionTab';

type TabKey = 'overview' | 'branding' | 'businessInfo' | 'customText' | 'activity' | 'featureFlags' | 'subscription';
type LangKey = 'en' | 'ar' | 'ku';

// Tabs are now computed dynamically based on permissions in the component
const LANGUAGES: LangKey[] = ['en', 'ar', 'ku'];

/** Safely parse a JSON string into a per-language object. */
function parseLocalizedJson(value: string | null): Record<LangKey, string> {
  const empty: Record<LangKey, string> = { en: '', ar: '', ku: '' };
  if (!value) return empty;
  try {
    const parsed = JSON.parse(value) as Record<string, string>;
    return {
      en: parsed.en ?? '',
      ar: parsed.ar ?? '',
      ku: parsed.ku ?? '',
    };
  } catch {
    // If it's a plain string, assign it to 'en'
    return { en: value, ar: '', ku: '' };
  }
}

export default function TenantDetailPage() {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();
  const location = useLocation();
  const user = useAuthStore(selectUser);
  const { hasPermission } = usePermissions();

  // Self-service mode: tenant user accessing /organization
  const selfService = location.pathname === ROUTES.ORGANIZATION;
  const tenantId = selfService ? user?.tenantId : id;

  // Guard: platform admin (no tenantId) shouldn't access /organization
  if (selfService && !tenantId) {
    return <Navigate to={ROUTES.DASHBOARD} replace />;
  }

  const { data: tenant, isLoading } = useTenant(tenantId!);

  useBackNavigation(
    selfService ? ROUTES.DASHBOARD : ROUTES.TENANTS.LIST,
    selfService ? t('nav.dashboard') : t('tenants.backToTenants')
  );

  const isPlatformAdmin = !user?.tenantId;

  // Permission-driven tabs
  const TABS: TabKey[] = [
    'overview',
    ...(hasPermission(PERMISSIONS.Tenants.Update) ? ['branding' as TabKey, 'businessInfo' as TabKey, 'customText' as TabKey] : []),
    ...(hasPermission(PERMISSIONS.System.ViewAuditLogs) ? ['activity' as TabKey] : []),
    ...(hasPermission(PERMISSIONS.FeatureFlags.View) ? ['featureFlags' as TabKey] : []),
    ...(isPlatformAdmin && hasPermission(PERMISSIONS.Billing?.ManageTenantSubscriptions) ? ['subscription' as TabKey] : []),
  ];

  const [activeTab, setActiveTab] = useState<TabKey>('overview');
  const [statusAction, setStatusAction] = useState<'suspend' | 'deactivate' | null>(null);

  // Status mutations
  const { mutate: activateTenant } = useActivateTenant();
  const { mutate: suspendTenant, isPending: isSuspending } = useSuspendTenant();
  const { mutate: deactivateTenant, isPending: isDeactivating } = useDeactivateTenant();

  // Default registration role
  const { mutate: setDefaultRole, isPending: isSavingDefaultRole } = useSetTenantDefaultRole();
  const { data: assignableRoles } = useAssignableRoles(tenantId, { enabled: !!tenantId && activeTab === 'overview' });
  const availableRoles = assignableRoles ?? [];

  // Branding state
  const [uploadedLogoId, setUploadedLogoId] = useState<string | null>(null);
  const [uploadedFaviconId, setUploadedFaviconId] = useState<string | null>(null);
  const [removeLogo, setRemoveLogo] = useState(false);
  const [removeFavicon, setRemoveFavicon] = useState(false);
  const [primaryColor, setPrimaryColor] = useState('');
  const [secondaryColor, setSecondaryColor] = useState('');
  const [description, setDescription] = useState('');
  const [logoPreview, setLogoPreview] = useState<string | null>(null);
  const [faviconPreview, setFaviconPreview] = useState<string | null>(null);

  // Business info state
  const [address, setAddress] = useState('');
  const [phone, setPhone] = useState('');
  const [website, setWebsite] = useState('');
  const [taxId, setTaxId] = useState('');

  // Custom text state
  const [loginPageTitle, setLoginPageTitle] = useState<Record<LangKey, string>>({ en: '', ar: '', ku: '' });
  const [loginPageSubtitle, setLoginPageSubtitle] = useState<Record<LangKey, string>>({ en: '', ar: '', ku: '' });
  const [emailFooterText, setEmailFooterText] = useState<Record<LangKey, string>>({ en: '', ar: '', ku: '' });
  const [activeLang, setActiveLang] = useState<LangKey>('en');

  // Initialize form state from tenant data
  useEffect(() => {
    if (!tenant) return;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setPrimaryColor(tenant.primaryColor ?? '');
    setSecondaryColor(tenant.secondaryColor ?? '');
    setDescription(tenant.description ?? '');
    setAddress(tenant.address ?? '');
    setPhone(tenant.phone ?? '');
    setWebsite(tenant.website ?? '');
    setTaxId(tenant.taxId ?? '');
    setLoginPageTitle(parseLocalizedJson(tenant.loginPageTitle));
    setLoginPageSubtitle(parseLocalizedJson(tenant.loginPageSubtitle));
    setEmailFooterText(parseLocalizedJson(tenant.emailFooterText));
  }, [tenant]);

  // Clean up preview URLs on unmount
  useEffect(() => () => {
    if (logoPreview) URL.revokeObjectURL(logoPreview);
    if (faviconPreview) URL.revokeObjectURL(faviconPreview);
  }, [logoPreview, faviconPreview]);

  // Branding mutation
  const { mutate: updateBranding, isPending: isSavingBranding } = useUpdateTenantBranding();
  const { mutate: updateBusinessInfo, isPending: isSavingBusiness } = useUpdateTenantBusinessInfo();
  const { mutate: updateCustomText, isPending: isSavingCustomText } = useUpdateTenantCustomText();

  // eslint-disable-next-line react-hooks/preserve-manual-memoization
  const handleSaveBranding = useCallback(() => {
    const hexRegex = /^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$/;
    if (primaryColor && !hexRegex.test(primaryColor)) {
      toast.error(t('tenants.invalidColor'));
      return;
    }
    if (secondaryColor && !hexRegex.test(secondaryColor)) {
      toast.error(t('tenants.invalidColor'));
      return;
    }
    updateBranding(
      {
        id: tenantId!,
        data: {
          logoFileId: uploadedLogoId ?? undefined,
          faviconFileId: uploadedFaviconId ?? undefined,
          primaryColor: primaryColor || undefined,
          secondaryColor: secondaryColor || undefined,
          description: description || undefined,
          removeLogo,
          removeFavicon,
        },
      },
      {
        onSuccess: () => {
          setUploadedLogoId(null);
          setUploadedFaviconId(null);
          setLogoPreview(null);
          setFaviconPreview(null);
          setRemoveLogo(false);
          setRemoveFavicon(false);
        },
      }
    );
  }, [id, uploadedLogoId, uploadedFaviconId, primaryColor, secondaryColor, description, removeLogo, removeFavicon, updateBranding, t]);

  const handleSaveBusinessInfo = useCallback(() => {
    updateBusinessInfo({
      id: tenantId!,
      data: { address, phone, website, taxId },
    });
  }, [tenantId, address, phone, website, taxId, updateBusinessInfo]);

  const handleSaveCustomText = useCallback(() => {
    updateCustomText({
      id: tenantId!,
      data: {
        loginPageTitle: JSON.stringify(loginPageTitle),
        loginPageSubtitle: JSON.stringify(loginPageSubtitle),
        emailFooterText: JSON.stringify(emailFooterText),
      },
    });
  }, [tenantId, loginPageTitle, loginPageSubtitle, emailFooterText, updateCustomText]);

  const logoPreviewUrl = useMemo(() => {
    if (logoPreview) return logoPreview;
    if (removeLogo) return null;
    return tenant?.logoUrl ?? null;
  }, [logoPreview, removeLogo, tenant?.logoUrl]);

  const faviconPreviewUrl = useMemo(() => {
    if (faviconPreview) return faviconPreview;
    if (removeFavicon) return null;
    return tenant?.faviconUrl ?? null;
  }, [faviconPreview, removeFavicon, tenant?.faviconUrl]);

  if (isLoading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  if (!tenant) {
    return <div className="text-muted-foreground">{t('common.noResults')}</div>;
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={tenant.name}
      />

      {/* Mobile: horizontal scrollable pills */}
      <div className="flex gap-2 overflow-x-auto pb-2 md:hidden">
        {TABS.map((tab) => (
          <button
            key={tab}
            type="button"
            onClick={() => setActiveTab(tab)}
            className={cn(
              'shrink-0 rounded-full px-4 py-1.5 text-sm font-medium transition-colors',
              activeTab === tab
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted text-muted-foreground hover:bg-muted/80'
            )}
          >
            {t(`tenants.${tab}`)}
          </button>
        ))}
      </div>

      {/* Desktop: vertical tabs + content */}
      <div className="flex gap-6">
        {/* Vertical tab list */}
        <nav className="hidden md:flex w-48 shrink-0 flex-col gap-1">
          {TABS.map((tab) => (
            <button
              key={tab}
              type="button"
              onClick={() => setActiveTab(tab)}
              className={cn(
                'px-4 py-2.5 text-sm text-start transition-colors duration-150 cursor-pointer ltr:border-l-2 rtl:border-r-2',
                activeTab === tab
                  ? 'state-active-border font-semibold [color:var(--active-text)]'
                  : 'border-transparent state-hover'
              )}
            >
              {t(`tenants.${tab}`)}
            </button>
          ))}
        </nav>

        {/* Tab content */}
        <div className="flex-1 min-w-0">
          {/* -- Overview Tab -- */}
          {activeTab === 'overview' && (
            <Card>
              <CardContent className="py-6">
                <div className="flex items-start gap-4 mb-6">
                  <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-primary/10 text-lg font-bold text-primary overflow-hidden">
                    {tenant.logoUrl ? (
                      <img
                        src={tenant.logoUrl}
                        alt={tenant.name}
                        className="h-14 w-14 object-cover"
                      />
                    ) : (
                      tenant.name.charAt(0)
                    )}
                  </div>
                  <div className="min-w-0 flex-1">
                    <h2 className="text-xl font-bold text-foreground">{tenant.name}</h2>
                    {tenant.slug && (
                      <p className="text-muted-foreground">{tenant.slug}</p>
                    )}
                  </div>
                  <Badge variant={STATUS_BADGE_VARIANT[tenant.status] || 'default'}>
                    {tenant.status}
                  </Badge>
                </div>

                <div className="grid gap-x-6 gap-y-4 sm:grid-cols-2 lg:grid-cols-3">
                  <InfoField label={t('tenants.name')}>
                    <span>{tenant.name}</span>
                  </InfoField>
                  <InfoField label={t('tenants.slug')}>
                    <span>{tenant.slug || '-'}</span>
                  </InfoField>
                  <InfoField label={t('common.createdAt')}>
                    {tenant.createdAt ? formatDate(tenant.createdAt, 'long') : '-'}
                  </InfoField>
                </div>

                {/* Default Registration Role */}
                {hasPermission(PERMISSIONS.Tenants.Update) && (
                  <div className="border-t pt-4 mt-6">
                    <h4 className="text-sm font-medium text-foreground mb-2">{t('tenants.defaultRegistrationRole')}</h4>
                    <p className="text-xs text-muted-foreground mb-3">{t('tenants.defaultRegistrationRoleDesc')}</p>
                    <div className="flex items-center gap-3">
                      <Select
                        value={tenant.defaultRegistrationRoleId ?? '__none__'}
                        onValueChange={(value) => {
                          const roleId = value === '__none__' ? null : value;
                          setDefaultRole({ id: tenantId!, roleId });
                        }}
                      >
                        <SelectTrigger className="max-w-xs">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="__none__">{t('tenants.useGlobalDefault')}</SelectItem>
                          {availableRoles.map((role) => (
                            <SelectItem key={role.id} value={role.id}>
                              {role.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      {isSavingDefaultRole && <Spinner size="sm" />}
                    </div>
                  </div>
                )}

                <div className="flex items-center gap-2 border-t pt-4 mt-6">
                  {(tenant.status === 'Suspended' || tenant.status === 'Deactivated') && (
                    <Button variant="outline" size="sm" onClick={() => activateTenant(tenantId!, {
                      onError: () => toast.error(t('tenants.activateError')),
                    })}>
                      <UserCheck className="h-4 w-4" />
                      {t('tenants.activate')}
                    </Button>
                  )}
                  {tenant.status === 'Active' && (
                    <>
                      <Button variant="outline" size="sm" onClick={() => setStatusAction('suspend')}>
                        <Ban className="h-4 w-4" />
                        {t('tenants.suspend')}
                      </Button>
                      <Button variant="outline" size="sm" onClick={() => setStatusAction('deactivate')}>
                        <UserX className="h-4 w-4" />
                        {t('tenants.deactivate')}
                      </Button>
                    </>
                  )}
                </div>
              </CardContent>
            </Card>
          )}

          {/* -- Branding Tab -- */}
          {activeTab === 'branding' && (
            <Card>
              <CardContent className="py-6 space-y-6">
                <h3 className="text-lg font-semibold text-foreground">
                  {t('tenants.branding')}
                </h3>

                {/* Logo */}
                <div className="space-y-2">
                  <Label>{t('tenants.logo')}</Label>
                  {logoPreviewUrl && (
                    <div className="flex items-center gap-3">
                      <img
                        src={logoPreviewUrl}
                        alt={t('tenants.logo')}
                        className="h-16 w-16 rounded-lg border object-cover"
                      />
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => {
                          setUploadedLogoId(null);
                          setLogoPreview(null);
                          setRemoveLogo(true);
                        }}
                      >
                        <Trash2 className="h-4 w-4" />
                        {t('tenants.removeLogo')}
                      </Button>
                    </div>
                  )}
                  {!logoPreviewUrl && (
                    <FileUpload
                      onUpload={() => {}}
                      mode="temp"
                      onFileSelected={(file) => {
                        if (file.type.startsWith('image/')) {
                          setLogoPreview(URL.createObjectURL(file));
                        }
                      }}
                      onUploaded={(fileId) => {
                        setUploadedLogoId(fileId);
                        setRemoveLogo(false);
                      }}
                      accept="image/*"
                      maxSize={5 * 1024 * 1024}
                    />
                  )}
                </div>

                {/* Favicon */}
                <div className="space-y-2">
                  <Label>{t('tenants.favicon')}</Label>
                  {faviconPreviewUrl && (
                    <div className="flex items-center gap-3">
                      <img
                        src={faviconPreviewUrl}
                        alt={t('tenants.favicon')}
                        className="h-8 w-8 rounded border object-cover"
                      />
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => {
                          setUploadedFaviconId(null);
                          setFaviconPreview(null);
                          setRemoveFavicon(true);
                        }}
                      >
                        <Trash2 className="h-4 w-4" />
                        {t('common.remove')}
                      </Button>
                    </div>
                  )}
                  {!faviconPreviewUrl && (
                    <FileUpload
                      onUpload={() => {}}
                      mode="temp"
                      onFileSelected={(file) => {
                        if (file.type.startsWith('image/')) {
                          setFaviconPreview(URL.createObjectURL(file));
                        }
                      }}
                      onUploaded={(fileId) => {
                        setUploadedFaviconId(fileId);
                        setRemoveFavicon(false);
                      }}
                      accept="image/*"
                      maxSize={2 * 1024 * 1024}
                    />
                  )}
                </div>

                {/* Primary Color */}
                <div className="space-y-2">
                  <Label>{t('tenants.primaryColor')}</Label>
                  <div className="flex items-center gap-3">
                    <Input
                      type="text"
                      value={primaryColor}
                      onChange={(e) => setPrimaryColor(e.target.value)}
                      placeholder="#3b82f6"
                      className="max-w-xs"
                    />
                    <input
                      type="color"
                      value={primaryColor || '#3b82f6'}
                      onChange={(e) => setPrimaryColor(e.target.value)}
                      className="h-10 w-10 cursor-pointer rounded border p-0.5"
                    />
                    {primaryColor && (
                      <div
                        className="h-10 w-10 rounded border"
                        style={{ backgroundColor: primaryColor }}
                        title={t('tenants.colorPreview')}
                      />
                    )}
                  </div>
                </div>

                {/* Secondary Color */}
                <div className="space-y-2">
                  <Label>{t('tenants.secondaryColor')}</Label>
                  <div className="flex items-center gap-3">
                    <Input
                      type="text"
                      value={secondaryColor}
                      onChange={(e) => setSecondaryColor(e.target.value)}
                      placeholder="#64748b"
                      className="max-w-xs"
                    />
                    <input
                      type="color"
                      value={secondaryColor || '#64748b'}
                      onChange={(e) => setSecondaryColor(e.target.value)}
                      className="h-10 w-10 cursor-pointer rounded border p-0.5"
                    />
                    {secondaryColor && (
                      <div
                        className="h-10 w-10 rounded border"
                        style={{ backgroundColor: secondaryColor }}
                        title={t('tenants.colorPreview')}
                      />
                    )}
                  </div>
                </div>

                {/* Description */}
                <div className="space-y-2">
                  <Label>{t('tenants.description')}</Label>
                  <Textarea
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                    rows={3}
                    className="max-w-lg"
                  />
                </div>

                <div className="border-t pt-4">
                  <Button onClick={handleSaveBranding} disabled={isSavingBranding}>
                    <Save className="h-4 w-4" />
                    {isSavingBranding ? t('common.saving') : t('common.save')}
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}

          {/* -- Business Info Tab -- */}
          {activeTab === 'businessInfo' && (
            <Card>
              <CardContent className="py-6 space-y-6">
                <h3 className="text-lg font-semibold text-foreground">
                  {t('tenants.businessInfo')}
                </h3>

                <div className="space-y-2">
                  <Label>{t('tenants.address')}</Label>
                  <Textarea
                    value={address}
                    onChange={(e) => setAddress(e.target.value)}
                    rows={3}
                    className="max-w-lg"
                  />
                </div>

                <div className="space-y-2">
                  <Label>{t('tenants.phone')}</Label>
                  <Input
                    type="text"
                    value={phone}
                    onChange={(e) => setPhone(e.target.value)}
                    className="max-w-md"
                  />
                </div>

                <div className="space-y-2">
                  <Label>{t('tenants.website')}</Label>
                  <Input
                    type="url"
                    value={website}
                    onChange={(e) => setWebsite(e.target.value)}
                    placeholder="https://"
                    className="max-w-md"
                  />
                </div>

                <div className="space-y-2">
                  <Label>{t('tenants.taxId')}</Label>
                  <Input
                    type="text"
                    value={taxId}
                    onChange={(e) => setTaxId(e.target.value)}
                    className="max-w-md"
                  />
                </div>

                <div className="border-t pt-4">
                  <Button onClick={handleSaveBusinessInfo} disabled={isSavingBusiness}>
                    <Save className="h-4 w-4" />
                    {isSavingBusiness ? t('common.saving') : t('common.save')}
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}

          {/* -- Activity Tab -- */}
          {activeTab === 'activity' && <ActivityTab />}

          {/* -- Feature Flags Tab -- */}
          {activeTab === 'featureFlags' && tenantId && (
            <TenantFeatureFlagsTab tenantId={tenantId} />
          )}

          {/* -- Subscription Tab (SuperAdmin only) -- */}
          {activeTab === 'subscription' && tenantId && (
            <SubscriptionTab tenantId={tenantId} />
          )}

          {/* -- Custom Text Tab -- */}
          {activeTab === 'customText' && (
            <Card>
              <CardContent className="py-6 space-y-6">
                <h3 className="text-lg font-semibold text-foreground">
                  {t('tenants.customText')}
                </h3>

                {/* Language sub-tabs */}
                <div className="flex gap-2">
                  {LANGUAGES.map((lang) => (
                    <button
                      key={lang}
                      type="button"
                      onClick={() => setActiveLang(lang)}
                      className={cn(
                        'rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
                        activeLang === lang
                          ? 'bg-primary text-primary-foreground'
                          : 'bg-muted text-muted-foreground hover:bg-muted/80'
                      )}
                    >
                      {lang.toUpperCase()}
                    </button>
                  ))}
                </div>

                {/* Login Page Title */}
                <div className="space-y-2">
                  <Label>{t('tenants.loginPageTitle')}</Label>
                  <Input
                    type="text"
                    value={loginPageTitle[activeLang]}
                    onChange={(e) =>
                      setLoginPageTitle((prev) => ({
                        ...prev,
                        [activeLang]: e.target.value,
                      }))
                    }
                    className="max-w-lg"
                    dir={activeLang === 'en' ? 'ltr' : 'rtl'}
                  />
                </div>

                {/* Login Page Subtitle */}
                <div className="space-y-2">
                  <Label>{t('tenants.loginPageSubtitle')}</Label>
                  <Textarea
                    value={loginPageSubtitle[activeLang]}
                    onChange={(e) =>
                      setLoginPageSubtitle((prev) => ({
                        ...prev,
                        [activeLang]: e.target.value,
                      }))
                    }
                    rows={2}
                    className="max-w-lg"
                    dir={activeLang === 'en' ? 'ltr' : 'rtl'}
                  />
                </div>

                {/* Email Footer Text */}
                <div className="space-y-2">
                  <Label>{t('tenants.emailFooterText')}</Label>
                  <Textarea
                    value={emailFooterText[activeLang]}
                    onChange={(e) =>
                      setEmailFooterText((prev) => ({
                        ...prev,
                        [activeLang]: e.target.value,
                      }))
                    }
                    rows={2}
                    className="max-w-lg"
                    dir={activeLang === 'en' ? 'ltr' : 'rtl'}
                  />
                </div>

                {/* Preview section */}
                <div className="space-y-2">
                  <Label>{t('tenants.preview')}</Label>
                  <div className="rounded-lg border bg-muted/50 p-6 max-w-lg">
                    <div className="flex flex-col items-center text-center gap-2">
                      {tenant.logoUrl && (
                        <img
                          src={tenant.logoUrl}
                          alt={tenant.name}
                          className="h-12 w-12 rounded-lg object-cover mb-2"
                        />
                      )}
                      <h4 className="text-lg font-bold text-foreground">
                        {loginPageTitle[activeLang] || tenant.name}
                      </h4>
                      <p className="text-sm text-muted-foreground">
                        {loginPageSubtitle[activeLang] || ''}
                      </p>
                    </div>
                  </div>
                </div>

                <div className="border-t pt-4">
                  <Button onClick={handleSaveCustomText} disabled={isSavingCustomText}>
                    <Save className="h-4 w-4" />
                    {isSavingCustomText ? t('common.saving') : t('common.save')}
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}
        </div>
      </div>

      <ConfirmDialog
        isOpen={!!statusAction}
        onClose={() => setStatusAction(null)}
        onConfirm={() => {
          if (statusAction === 'suspend') {
            suspendTenant(tenantId!, {
              onSuccess: () => setStatusAction(null),
              onError: () => {
                toast.error(t('tenants.suspendError'));
                setStatusAction(null);
              },
            });
          } else if (statusAction === 'deactivate') {
            deactivateTenant(tenantId!, {
              onSuccess: () => setStatusAction(null),
              onError: () => {
                toast.error(t('tenants.deactivateError'));
                setStatusAction(null);
              },
            });
          }
        }}
        title={statusAction === 'suspend' ? t('tenants.suspend') : t('tenants.deactivate')}
        description={
          statusAction === 'suspend'
            ? t('tenants.suspendConfirm', { name: tenant.name })
            : t('tenants.deactivateConfirm', { name: tenant.name })
        }
        confirmLabel={statusAction === 'suspend' ? t('tenants.suspend') : t('tenants.deactivate')}
        isLoading={isSuspending || isDeactivating}
      />
    </div>
  );
}
