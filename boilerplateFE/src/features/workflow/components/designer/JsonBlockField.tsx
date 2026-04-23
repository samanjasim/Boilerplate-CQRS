import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';

interface Props {
  label: string;
  placeholder?: string;
  value: unknown;
  onChange: (parsed: unknown) => void;
  disabled?: boolean;
}

export function JsonBlockField({ label, placeholder, value, onChange, disabled }: Props) {
  const { t } = useTranslation();
  const [text, setText] = useState(() => (value == null ? '' : JSON.stringify(value, null, 2)));
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setText(value == null ? '' : JSON.stringify(value, null, 2));
  }, [value]);

  const commit = () => {
    if (text.trim() === '') { setError(null); onChange(null); return; }
    try {
      const parsed = JSON.parse(text);
      setError(null);
      onChange(parsed);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  const format = () => {
    if (text.trim() === '') return;
    try {
      const parsed = JSON.parse(text);
      setText(JSON.stringify(parsed, null, 2));
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between">
        <Label className="text-xs font-medium">{label}</Label>
        {!disabled && (
          <div className="flex items-center gap-1">
            <Button type="button" size="sm" variant="ghost" onClick={format} disabled={!text.trim()}>
              {t('workflow.designer.json.format')}
            </Button>
            <Button type="button" size="sm" variant="ghost" onClick={() => { setText(''); onChange(null); setError(null); }}>
              {t('workflow.designer.json.reset')}
            </Button>
          </div>
        )}
      </div>
      <textarea
        className="w-full rounded-xl border border-border bg-background p-2 font-mono text-xs leading-relaxed min-h-[140px]"
        value={text}
        onChange={e => setText(e.target.value)}
        onBlur={commit}
        placeholder={placeholder ?? t('workflow.designer.json.hintPlaceholder')}
        disabled={disabled}
        spellCheck={false}
      />
      {error && (
        <p className="text-[11px] text-destructive">
          {t('workflow.designer.json.parseError', { message: error })}
        </p>
      )}
    </div>
  );
}
