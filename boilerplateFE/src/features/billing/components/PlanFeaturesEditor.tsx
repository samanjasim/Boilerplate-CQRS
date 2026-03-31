import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Spinner } from '@/components/ui/spinner';
import { usePlanOptions } from '../api';
import type { PlanFeatureEntry, PlanOption } from '@/types';

interface PlanFeaturesEditorProps {
  value: PlanFeatureEntry[];
  onChange: (features: PlanFeatureEntry[]) => void;
}

const SUPPORTED_LANGUAGES = ['en', 'ar', 'ku'] as const;

export function PlanFeaturesEditor({ value, onChange }: PlanFeaturesEditorProps) {
  const { t } = useTranslation();
  const { data: options, isLoading } = usePlanOptions();

  const grouped = useMemo(() => {
    if (!options) return new Map<string, PlanOption[]>();
    const map = new Map<string, PlanOption[]>();
    for (const opt of options) {
      const category = opt.category || 'General';
      const list = map.get(category) ?? [];
      list.push(opt);
      map.set(category, list);
    }
    return map;
  }, [options]);

  const entryMap = useMemo(() => {
    const map = new Map<string, PlanFeatureEntry>();
    for (const entry of value) {
      map.set(entry.key, entry);
    }
    return map;
  }, [value]);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-4">
        <Spinner size="sm" />
        <span className="ms-2 text-sm text-muted-foreground">{t('common.loading')}</span>
      </div>
    );
  }

  if (!options || options.length === 0) {
    return (
      <p className="text-sm text-muted-foreground py-2">
        {t('billing.noFeatureOptions', 'No feature options available.')}
      </p>
    );
  }

  const toggleFeature = (opt: PlanOption, checked: boolean) => {
    if (checked) {
      const newEntry: PlanFeatureEntry = {
        key: opt.key,
        value: opt.defaultValue,
        translations: {},
      };
      onChange([...value, newEntry]);
    } else {
      onChange(value.filter((e) => e.key !== opt.key));
    }
  };

  const updateValue = (key: string, newValue: string) => {
    onChange(
      value.map((e) => (e.key === key ? { ...e, value: newValue } : e)),
    );
  };

  const updateTranslation = (key: string, lang: string, label: string) => {
    onChange(
      value.map((e) => {
        if (e.key !== key) return e;
        return {
          ...e,
          translations: {
            ...e.translations,
            [lang]: { label },
          },
        };
      }),
    );
  };

  return (
    <div className="space-y-4">
      <Label>{t('billing.planFeatures', 'Plan Features')}</Label>
      {Array.from(grouped.entries()).map(([category, opts]) => (
        <div key={category} className="rounded-xl border bg-card p-3 space-y-3">
          <h4 className="text-sm font-semibold text-foreground">{category}</h4>
          {opts.map((opt) => {
            const entry = entryMap.get(opt.key);
            const isIncluded = !!entry;

            return (
              <div key={opt.key} className="space-y-2 border-t pt-2 first:border-t-0 first:pt-0">
                {/* Toggle row */}
                <label className="flex items-center gap-2 text-sm cursor-pointer select-none">
                  <input
                    type="checkbox"
                    checked={isIncluded}
                    onChange={(e) => toggleFeature(opt, e.target.checked)}
                    className="accent-primary"
                  />
                  <span className="font-medium text-foreground">{opt.name}</span>
                  {opt.description && (
                    <span className="text-muted-foreground text-xs">— {opt.description}</span>
                  )}
                </label>

                {/* Value + translations (only when included) */}
                {isIncluded && (
                  <div className="ms-6 space-y-2">
                    {/* Value input based on type */}
                    <div className="flex items-center gap-2">
                      <Label className="text-xs text-muted-foreground w-14 shrink-0">
                        {t('common.value', 'Value')}
                      </Label>
                      {opt.valueType === 'Boolean' ? (
                        <label className="flex items-center gap-2 text-sm cursor-pointer select-none">
                          <input
                            type="checkbox"
                            checked={entry.value === 'true'}
                            onChange={(e) =>
                              updateValue(opt.key, e.target.checked ? 'true' : 'false')
                            }
                            className="accent-primary"
                          />
                          <span className="text-muted-foreground text-xs">
                            {entry.value === 'true'
                              ? t('common.enabled', 'Enabled')
                              : t('common.disabled', 'Disabled')}
                          </span>
                        </label>
                      ) : opt.valueType === 'Integer' ? (
                        <Input
                          type="number"
                          className="h-8 w-32"
                          value={entry.value}
                          onChange={(e) => updateValue(opt.key, e.target.value)}
                        />
                      ) : (
                        <Input
                          type="text"
                          className="h-8 flex-1"
                          value={entry.value}
                          onChange={(e) => updateValue(opt.key, e.target.value)}
                        />
                      )}
                    </div>

                    {/* Translation inputs */}
                    <div className="space-y-1">
                      <span className="text-xs text-muted-foreground">
                        {t('billing.displayLabels', 'Display labels')}
                      </span>
                      <div className="grid gap-1.5 sm:grid-cols-3">
                        {SUPPORTED_LANGUAGES.map((lang) => (
                          <div key={lang} className="flex items-center gap-1.5">
                            <span className="text-xs text-muted-foreground uppercase w-6 shrink-0">
                              {lang}
                            </span>
                            <Input
                              type="text"
                              className="h-7 text-xs"
                              placeholder={`${opt.name} (${lang})`}
                              value={entry.translations?.[lang]?.label ?? ''}
                              onChange={(e) =>
                                updateTranslation(opt.key, lang, e.target.value)
                              }
                            />
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      ))}
    </div>
  );
}
