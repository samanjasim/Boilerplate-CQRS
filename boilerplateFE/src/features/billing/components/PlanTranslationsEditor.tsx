import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertCircle, Code2, Languages, RotateCcw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { cn } from '@/lib/utils';
import {
  parseTranslations,
  serializeTranslations,
  type PlanTranslations,
} from '../utils/plan-translations';

/** Supported preview locales — must match the app's i18n locales. */
const LOCALES = [
  { code: 'en', dir: 'ltr' as const },
  { code: 'ar', dir: 'rtl' as const },
  { code: 'ku', dir: 'rtl' as const },
];

export interface PlanTranslationsEditorProps {
  /** Raw JSON string stored on `SubscriptionPlan.translations`. */
  value: string;
  onChange: (next: string) => void;
}

/**
 * Per-locale name + description editor for `SubscriptionPlan.translations`.
 * The shape stored on the wire is `{"en": {"name": "..."}, ...}`. This
 * component exposes a structured row per supported locale, with a power-user
 * "Edit JSON" toggle that falls back to raw editing.
 *
 * If the incoming value isn't parseable, the editor automatically opens in
 * raw mode so users can fix it without losing data.
 */
export function PlanTranslationsEditor({ value, onChange }: PlanTranslationsEditorProps) {
  const { t } = useTranslation();
  const incomingParsed = useMemo(() => parseTranslations(value), [value]);
  const incomingValid = incomingParsed !== null;

  const [rawMode, setRawMode] = useState(!incomingValid);
  const [rawDraft, setRawDraft] = useState(value);
  const [rawError, setRawError] = useState<string | null>(null);

  // Local structured state is rebuilt from `value` on every change; this keeps
  // the editor in lock-step with form-state resets (e.g., dialog re-open).
  const map: PlanTranslations = incomingParsed ?? {};

  // Sync local raw draft when value changes externally (e.g., dialog reopens).
  // The setStates here mirror external prop changes into local UI state (raw
  // textarea draft + auto-open raw mode on parse failure). The rule fires on
  // the first call only; the second is conditional and isn't flagged.
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setRawDraft(value);
    if (!incomingValid && !rawMode) setRawMode(true);
  }, [value, incomingValid, rawMode]);

  const setLocaleField = (code: string, field: 'name' | 'description', next: string) => {
    const updated: PlanTranslations = {
      ...map,
      [code]: { ...map[code], [field]: next },
    };
    onChange(serializeTranslations(updated));
  };

  const handleApplyRaw = () => {
    if (!rawDraft.trim()) {
      setRawError(null);
      onChange('');
      setRawMode(false);
      return;
    }
    const parsed = parseTranslations(rawDraft);
    if (parsed === null) {
      setRawError(t('billing.planForm.translations.invalidJson'));
      return;
    }
    setRawError(null);
    onChange(serializeTranslations(parsed));
    setRawMode(false);
  };

  const handleClearAll = () => {
    onChange('');
    setRawDraft('');
    setRawError(null);
  };

  const hasAnyContent = Object.values(map).some(
    (entry) => entry?.name?.trim() || entry?.description?.trim(),
  );

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between flex-wrap gap-2">
        <div className="flex items-center gap-2 text-xs font-medium text-foreground">
          <Languages className="h-3.5 w-3.5 text-muted-foreground" />
          {t('billing.translations')}
        </div>
        <div className="flex items-center gap-2">
          {hasAnyContent && (
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="h-7 px-2 text-[11px] text-muted-foreground hover:text-foreground"
              onClick={handleClearAll}
            >
              <RotateCcw className="h-3 w-3 ltr:mr-1 rtl:ml-1" />
              {t('billing.planForm.translations.clearAll')}
            </Button>
          )}
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="h-7 px-2 text-[11px] text-muted-foreground hover:text-foreground"
            onClick={() => {
              setRawDraft(value);
              setRawError(null);
              setRawMode((v) => !v);
            }}
            aria-pressed={rawMode}
          >
            <Code2 className="h-3 w-3 ltr:mr-1 rtl:ml-1" />
            {rawMode
              ? t('billing.planForm.translations.exitRaw')
              : t('billing.planForm.translations.editRaw')}
          </Button>
        </div>
      </div>

      <p className="text-[11px] text-muted-foreground/80">
        {t('billing.planForm.translations.hint')}
      </p>

      {rawMode ? (
        <div className="space-y-2">
          <Label htmlFor="pf-translations-raw" className="sr-only">
            {t('billing.translations')}
          </Label>
          <Textarea
            id="pf-translations-raw"
            rows={6}
            placeholder='{"ar": {"name": "..."}, "ku": {"name": "..."}}'
            value={rawDraft}
            onChange={(e) => {
              setRawDraft(e.target.value);
              setRawError(null);
            }}
            className="font-mono text-xs"
            aria-invalid={!!rawError}
          />
          {rawError && (
            <div className="flex items-center gap-1.5 text-[11px] text-destructive">
              <AlertCircle className="h-3 w-3 shrink-0" />
              {rawError}
            </div>
          )}
          <div className="flex justify-end">
            <Button type="button" size="sm" onClick={handleApplyRaw}>
              {t('billing.planForm.translations.applyRaw')}
            </Button>
          </div>
        </div>
      ) : (
        <div className="space-y-2">
          {LOCALES.map((locale) => {
            const entry = map[locale.code] ?? {};
            return (
              <div
                key={locale.code}
                className="grid gap-2 sm:grid-cols-[64px_minmax(0,1fr)] items-start rounded-lg border border-border/40 bg-background/40 p-2.5"
              >
                <div className="flex items-center gap-1.5 pt-1.5">
                  <span className="rounded-md bg-foreground/10 px-1.5 py-0.5 text-[10px] font-mono font-semibold uppercase tracking-wide text-foreground/80">
                    {locale.code}
                  </span>
                  {locale.dir === 'rtl' && (
                    <span className="text-[9px] uppercase tracking-[0.1em] text-muted-foreground/70">
                      RTL
                    </span>
                  )}
                </div>
                <div className="space-y-1.5">
                  <Input
                    id={`pf-tr-name-${locale.code}`}
                    dir={locale.dir}
                    placeholder={t('billing.planForm.translations.namePlaceholder', {
                      locale: locale.code.toUpperCase(),
                    })}
                    value={entry.name ?? ''}
                    onChange={(e) => setLocaleField(locale.code, 'name', e.target.value)}
                    className={cn('text-sm', locale.dir === 'rtl' && 'text-end')}
                  />
                  <Input
                    id={`pf-tr-desc-${locale.code}`}
                    dir={locale.dir}
                    placeholder={t('billing.planForm.translations.descriptionPlaceholder')}
                    value={entry.description ?? ''}
                    onChange={(e) => setLocaleField(locale.code, 'description', e.target.value)}
                    className={cn('text-xs text-muted-foreground', locale.dir === 'rtl' && 'text-end')}
                  />
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
