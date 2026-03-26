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
import type { SystemSetting, UpdateSettingData } from '@/types';

/** Resolves an i18n label for a setting key.
 *  Tries `settings.{lastPart}` (camelCase), falls back to description, then raw key. */
function resolveLabel(
  t: (key: string) => string,
  setting: SystemSetting
): string {
  const parts = setting.key.split('.');
  const lastPart = parts[parts.length - 1];
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
      setLocalValues(values);
      if (groups.length > 0 && !activeTab) {
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
      if (!settingsMap[key]) return;
      setLocalValues((prev) => ({ ...prev, [key]: settingsMap[key].value }));
    },
    [settingsMap]
  );

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

  const handleSave = useCallback(() => {
    if (!groups) return;

    const changed: UpdateSettingData[] = [];
    for (const group of groups) {
      for (const setting of group.settings) {
        if (localValues[setting.key] !== setting.value) {
          changed.push({ key: setting.key, value: localValues[setting.key] });
        }
      }
    }

    if (changed.length === 0) return;
    updateSettings(changed);
  }, [groups, localValues, updateSettings]);

  // Derive categories from API response
  const categories = useMemo(() => {
    if (!groups) return [];
    return groups.map((g) => g.category);
  }, [groups]);

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
    <div className="space-y-6">
      <PageHeader
        title={t('settings.title')}
        subtitle={t('settings.subtitle')}
        actions={
          <Button onClick={handleSave} disabled={isPending || changedCount === 0}>
            <Save className="h-4 w-4" />
            {isPending ? t('common.saving') : t('settings.save')}
            {changedCount > 0 && (
              <Badge variant="secondary" className="ms-2">
                {t('settings.unsavedChanges', { count: changedCount })}
              </Badge>
            )}
          </Button>
        }
      />

      {/* Mobile: horizontal scrollable pills */}
      <div className="flex gap-2 overflow-x-auto pb-2 md:hidden">
        {categories.map((cat) => (
          <button
            key={cat}
            type="button"
            onClick={() => setActiveTab(cat)}
            className={cn(
              'shrink-0 rounded-full px-4 py-1.5 text-sm font-medium transition-colors',
              activeTab === cat
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted text-muted-foreground hover:bg-muted/80'
            )}
          >
            {resolveCategoryLabel(t, cat)}
          </button>
        ))}
      </div>

      {/* Desktop: vertical tabs left + form right */}
      <div className="flex gap-6">
        {/* Vertical tab list — hidden on mobile */}
        <nav className="hidden md:flex w-48 shrink-0 flex-col">
          {categories.map((cat) => (
            <button
              key={cat}
              type="button"
              onClick={() => setActiveTab(cat)}
              className={cn(
                'px-4 py-2.5 text-sm text-start transition-colors duration-150 cursor-pointer ltr:border-l-2 rtl:border-r-2',
                activeTab === cat
                  ? 'state-active-border font-semibold [color:var(--active-text)]'
                  : 'border-transparent state-hover'
              )}
            >
              {resolveCategoryLabel(t, cat)}
            </button>
          ))}
        </nav>

        {/* Settings form for active tab */}
        <div className="flex-1 min-w-0">
          {activeGroup && (
            <Card>
              <CardContent className="py-6">
                <div className="mb-6">
                  <h3 className="text-base font-semibold text-foreground">
                    {resolveCategoryLabel(t, activeGroup.category)}
                  </h3>
                  <p className="text-sm text-muted-foreground mt-0.5">General application configuration</p>
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
                              <Badge variant="outline">{t('settings.overridden')}</Badge>
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
    </div>
  );
}
