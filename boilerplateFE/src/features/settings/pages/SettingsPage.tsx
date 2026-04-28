import { useState, useEffect, useCallback, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Save, Eye, EyeOff, RotateCcw } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Spinner } from '@/components/ui/spinner';
import { PageHeader } from '@/components/common';
import { cn } from '@/lib/utils';
import { useSettings, useUpdateSettings } from '@/features/settings/api';
import { useAssignableRoles } from '@/features/roles/api/roles.queries';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import type { SystemSetting, UpdateSettingData } from '@/types';
import { SettingsCategoryNav } from '../components/SettingsCategoryNav';

/** Resolves an i18n label for a setting key.
 *  Tries `settings.{lastPart}` (camelCase), falls back to description, then raw key. */
function resolveLabel(
  t: (key: string) => string,
  setting: SystemSetting
): string {
  const parts = setting.key.split('.');
  const lastPart = parts[parts.length - 1] ?? setting.key;
  // Convert PascalCase to camelCase for i18n key
  const camelKey = lastPart.charAt(0).toLowerCase() + lastPart.slice(1);
  const i18nKey = `settings.${camelKey}`;
  const translated = t(i18nKey);
  // If translation returns the key itself, it doesn't exist
  if (translated !== i18nKey) return translated;
  if (setting.description) return setting.description;
  return lastPart;
}

/** Resolves an i18n label for a category name. */
function resolveCategoryLabel(t: (key: string) => string, category: string): string {
  const lowerKey = category.toLowerCase();
  const i18nKey = `settings.${lowerKey}`;
  const translated = t(i18nKey);
  if (translated !== i18nKey) return translated;
  return category;
}

// ---------------------------------------------------------------------------
// RoleSelectInput — dropdown for role-select settings (e.g. default role)
// ---------------------------------------------------------------------------

function RoleSelectInput({ value, onChange }: { value: string; onChange: (val: string) => void }) {
  const { t } = useTranslation();
  const { data: roles = [], isLoading } = useAssignableRoles(undefined, { enabled: true });

  return (
    <Select value={value || 'none'} onValueChange={(val) => onChange(val === 'none' ? '' : val)}>
      <SelectTrigger className="max-w-md" disabled={isLoading}>
        <SelectValue placeholder={t('settings.selectRole')} />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="none">{t('settings.noDefaultRole')}</SelectItem>
        {roles.map((role) => (
          <SelectItem key={role.id} value={role.id}>{role.name}</SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

// ---------------------------------------------------------------------------
// SettingInput — renders the correct input based on dataType
// ---------------------------------------------------------------------------

interface SettingInputProps {
  setting: SystemSetting;
  value: string;
  onChange: (key: string, value: string) => void;
  onToggle: (key: string) => void;
  isSecretVisible: boolean;
  onToggleSecret: (key: string) => void;
  t: (key: string) => string;
}

function SettingInput({
  setting,
  value,
  onChange,
  onToggle,
  isSecretVisible,
  onToggleSecret,
  t,
}: SettingInputProps) {
  const { dataType, key: settingKey, isSecret } = setting;

  // Role select dropdown (e.g. registration.default_role_id)
  if (dataType === 'role-select') {
    return <RoleSelectInput value={value} onChange={(val) => onChange(settingKey, val)} />;
  }

  // Boolean toggle
  if (dataType === 'boolean') {
    const isOn = value === 'true';
    return (
      <div className="flex items-center gap-3">
        <button
          type="button"
          role="switch"
          aria-checked={isOn}
          onClick={() => onToggle(settingKey)}
          className={cn(
            'relative inline-flex h-6 w-11 shrink-0 cursor-pointer items-center rounded-full transition-colors duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/20 focus-visible:ring-offset-2',
            isOn ? 'bg-primary' : 'bg-border'
          )}
        >
          <span
            className={cn(
              'pointer-events-none block h-5 w-5 rounded-full bg-white shadow-lg ring-0 transition-transform',
              isOn ? 'translate-x-5' : 'translate-x-0'
            )}
          />
        </button>
        <span className={cn('text-sm font-medium', isOn ? 'text-primary' : 'text-muted-foreground')}>
          {isOn ? t('common.enabled') : t('common.disabled')}
        </span>
      </div>
    );
  }

  // Secret / password field with eye toggle
  if (isSecret || dataType === 'password') {
    return (
      <div className="relative flex-1 max-w-md">
        <Input
          type={isSecretVisible ? 'text' : 'password'}
          value={value}
          onChange={(e) => onChange(settingKey, e.target.value)}
        />
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="absolute top-0 ltr:right-0 rtl:left-0 h-full px-3"
          onClick={() => onToggleSecret(settingKey)}
          aria-label={isSecretVisible ? t('common.hidePassword') : t('common.showPassword')}
        >
          {isSecretVisible ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
        </Button>
      </div>
    );
  }

  // Map remaining dataTypes to HTML input types
  const typeMap: Record<string, string> = {
    number: 'number',
    email: 'email',
    url: 'url',
    text: 'text',
  };

  const inputType = typeMap[dataType] ?? 'text';

  return (
    <Input
      type={inputType}
      value={value}
      onChange={(e) => onChange(settingKey, e.target.value)}
      className="max-w-md"
    />
  );
}

// ---------------------------------------------------------------------------
// SettingsPage
// ---------------------------------------------------------------------------

export default function SettingsPage() {
  const { t } = useTranslation();
  const { data: groups, isLoading, isError } = useSettings();
  const { mutate: updateSettings, isPending } = useUpdateSettings();

  // Currently active tab (category name)
  const [activeTab, setActiveTab] = useState<string>('');

  // Local copy of values keyed by setting key
  const [localValues, setLocalValues] = useState<Record<string, string>>({});
  // Track which secret fields are visible
  const [visibleSecrets, setVisibleSecrets] = useState<Record<string, boolean>>({});

  // Build initial values map and set first tab from server data
  useEffect(() => {
    if (groups) {
      const values: Record<string, string> = {};
      for (const group of groups) {
        for (const setting of group.settings) {
          values[setting.key] = setting.value;
        }
      }
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setLocalValues(values);
      if (!activeTab && groups[0]) {
        setActiveTab(groups[0].category);
      }
    }
  }, [groups, activeTab]);

  // Flat map of all settings for easy lookup
  const settingsMap = useMemo(() => {
    const map: Record<string, SystemSetting> = {};
    if (groups) {
      for (const group of groups) {
        for (const setting of group.settings) {
          map[setting.key] = setting;
        }
      }
    }
    return map;
  }, [groups]);

  const handleChange = useCallback((key: string, value: string) => {
    setLocalValues((prev) => ({ ...prev, [key]: value }));
  }, []);

  const toggleBoolean = useCallback((key: string) => {
    setLocalValues((prev) => ({
      ...prev,
      [key]: prev[key] === 'true' ? 'false' : 'true',
    }));
  }, []);

  const toggleSecretVisibility = useCallback((key: string) => {
    setVisibleSecrets((prev) => ({ ...prev, [key]: !prev[key] }));
  }, []);

  const handleReset = useCallback(
    (key: string) => {
      const setting = settingsMap[key];
      if (!setting) return;
      setLocalValues((prev) => ({ ...prev, [key]: setting.value }));
    },
    [settingsMap]
  );

  const handleResetAll = useCallback(() => {
    if (!groups) return;

    const values: Record<string, string> = {};
    for (const group of groups) {
      for (const setting of group.settings) {
        values[setting.key] = setting.value;
      }
    }
    setLocalValues(values);
  }, [groups]);

  // Count unsaved changes across ALL tabs
  const changedCount = useMemo(() => {
    if (!groups) return 0;
    let count = 0;
    for (const group of groups) {
      for (const setting of group.settings) {
        if (localValues[setting.key] !== setting.value) count++;
      }
    }
    return count;
  }, [groups, localValues]);

  // Warn before navigating away with unsaved changes
  useEffect(() => {
    if (changedCount === 0) return;
    const handler = (e: BeforeUnloadEvent) => { e.preventDefault(); };
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [changedCount]);

  const handleSave = useCallback(() => {
    if (!groups) return;

    const changed: UpdateSettingData[] = [];
    for (const group of groups) {
      for (const setting of group.settings) {
        const newValue = localValues[setting.key];
        if (newValue !== undefined && newValue !== setting.value) {
          changed.push({ key: setting.key, value: newValue });
        }
      }
    }

    if (changed.length === 0) return;
    updateSettings(changed);
  }, [groups, localValues, updateSettings]);

  const categoryItems = useMemo(() => {
    if (!groups) return [];
    return groups.map((group) => ({
      category: group.category,
      label: resolveCategoryLabel(t, group.category),
      count: group.settings.length,
    }));
  }, [groups, t]);

  // Current active group
  const activeGroup = useMemo(() => {
    if (!groups) return null;
    return groups.find((g) => g.category === activeTab) ?? null;
  }, [groups, activeTab]);

  // ---------- Render states ----------

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-20">
        <Spinner size="lg" />
      </div>
    );
  }

  if (isError) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('settings.title')} />
        <Card>
          <CardContent className="py-10 text-center">
            <p className="text-muted-foreground">{t('common.errorOccurred')}</p>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (!groups || groups.length === 0) {
    return (
      <div className="space-y-6">
        <PageHeader title={t('settings.title')} />
        <Card>
          <CardContent className="py-10 text-center">
            <p className="text-muted-foreground">{t('settings.noSettings')}</p>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div
      className="space-y-6"
      style={{ ['--settings-save-bar-h' as string]: changedCount > 0 ? '72px' : '0px' }}
    >
      <PageHeader
        title={t('settings.title')}
        subtitle={t('settings.subtitle')}
      />

      <div className="grid gap-6 lg:grid-cols-[200px_minmax(0,1fr)]">
        <SettingsCategoryNav
          items={categoryItems}
          activeCategory={activeTab}
          onSelect={setActiveTab}
        />

        <div className="min-w-0">
          {activeGroup && (
            <Card variant="glass">
              <CardContent className="py-6">
                <div className="mb-6">
                  <h3 className="text-base font-semibold text-foreground">
                    {resolveCategoryLabel(t, activeGroup.category)}
                  </h3>
                  <p className="text-sm text-muted-foreground mt-0.5">
                    {t('settings.subtitle')}
                  </p>
                </div>

                <div className="border-t border-border/40 pt-6">
                  <div className="grid gap-6 sm:grid-cols-2">
                  {activeGroup.settings.map((setting: SystemSetting) => {
                    const currentValue = localValues[setting.key] ?? setting.value;
                    const label = resolveLabel(t, setting);

                    return (
                      <div
                        key={setting.id}
                        className="space-y-1.5"
                      >
                        <div>
                          <div className="flex items-center gap-2">
                            <Label className="text-sm font-medium text-foreground">{label}</Label>
                            {setting.isOverridden && (
                              <Badge variant="outline" className="bg-primary/10 text-primary">
                                {t('settings.overridden')}
                              </Badge>
                            )}
                            {setting.isSecret && (
                              <Badge variant="secondary">{t('settings.secret')}</Badge>
                            )}
                          </div>
                          {setting.description && (
                            <p className="text-xs text-muted-foreground mt-1">
                              {setting.description}
                            </p>
                          )}
                        </div>

                        <div className="flex items-center gap-2">
                          <SettingInput
                            setting={setting}
                            value={currentValue}
                            onChange={handleChange}
                            onToggle={toggleBoolean}
                            isSecretVisible={!!visibleSecrets[setting.key]}
                            onToggleSecret={toggleSecretVisibility}
                            t={t}
                          />

                          {setting.isOverridden && (
                            <Button
                              type="button"
                              variant="ghost"
                              size="sm"
                              onClick={() => handleReset(setting.key)}
                              title={t('settings.reset')}
                            >
                              <RotateCcw className="h-4 w-4" />
                            </Button>
                          )}
                        </div>
                      </div>
                    );
                  })}
                  </div>
                </div>
              </CardContent>
            </Card>
          )}
        </div>
      </div>

      {changedCount > 0 && (
        <div className="fixed bottom-4 end-4 z-40 flex flex-wrap items-center justify-end gap-3 rounded-2xl surface-glass-strong px-4 py-3 shadow-float">
          <Badge variant="info">{t('settings.unsavedChanges', { count: changedCount })}</Badge>
          <Button type="button" variant="ghost" onClick={handleResetAll}>
            <RotateCcw className="h-4 w-4" />
            {t('common.reset')}
          </Button>
          <Button type="button" onClick={handleSave} disabled={isPending}>
            {isPending ? <Spinner size="sm" /> : <Save className="h-4 w-4" />}
            {isPending ? t('common.saving') : t('settings.save')}
          </Button>
        </div>
      )}
    </div>
  );
}
