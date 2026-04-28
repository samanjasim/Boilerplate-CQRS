import { useMemo, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/utils';

interface JsonViewProps {
  payload: string | null | undefined;
  className?: string;
}

type JsonValue = string | number | boolean | null | JsonObject | JsonArray;
interface JsonObject {
  [key: string]: JsonValue;
}
type JsonArray = JsonValue[];

function tryParse(payload: string | null | undefined): { ok: true; value: JsonValue } | { ok: false; raw: string } {
  if (payload == null) return { ok: false, raw: '' };
  try {
    return { ok: true, value: JSON.parse(payload) as JsonValue };
  } catch {
    return { ok: false, raw: payload };
  }
}

function renderValue(value: JsonValue, indent: number, lineNo: { n: number }): ReactNode {
  const pad = '  '.repeat(indent);

  if (value === null) {
    return <span className="text-[var(--color-violet-600)] dark:text-[var(--color-violet-400)]">null</span>;
  }

  if (typeof value === 'boolean' || typeof value === 'number') {
    return (
      <span className="text-[var(--color-violet-600)] dark:text-[var(--color-violet-400)]">
        {String(value)}
      </span>
    );
  }

  if (typeof value === 'string') {
    return <span className="text-emerald-600 dark:text-emerald-400">"{value}"</span>;
  }

  if (Array.isArray(value)) {
    if (value.length === 0) return <span className="text-muted-foreground">[]</span>;
    return (
      <>
        <span className="text-muted-foreground">[</span>
        {value.map((item, i) => {
          lineNo.n += 1;
          return (
            <div key={i} className="-mx-2 flex px-2 hover:bg-[var(--hover-bg)]">
              <span className="w-8 shrink-0 select-none text-xs text-primary/50">{lineNo.n}</span>
              <span>
                {pad} {renderValue(item, indent + 1, lineNo)}
                {i < value.length - 1 ? ',' : ''}
              </span>
            </div>
          );
        })}
        <div className="-mx-2 flex px-2">
          <span className="w-8 shrink-0 select-none text-xs text-primary/50">&nbsp;</span>
          <span className="text-muted-foreground">{pad}]</span>
        </div>
      </>
    );
  }

  const entries = Object.entries(value);
  if (entries.length === 0) return <span className="text-muted-foreground">{'{}'}</span>;

  return (
    <>
      <span className="text-muted-foreground">{'{'}</span>
      {entries.map(([key, val], i) => {
        lineNo.n += 1;
        return (
          <div key={key} className="-mx-2 flex px-2 hover:bg-[var(--hover-bg)]">
            <span className="w-8 shrink-0 select-none text-xs text-primary/50">{lineNo.n}</span>
            <span>
              {pad} <span className="text-primary">"{key}"</span>
              <span className="text-muted-foreground">: </span>
              {renderValue(val, indent + 1, lineNo)}
              {i < entries.length - 1 ? ',' : ''}
            </span>
          </div>
        );
      })}
      <div className="-mx-2 flex px-2">
        <span className="w-8 shrink-0 select-none text-xs text-primary/50">&nbsp;</span>
        <span className="text-muted-foreground">{pad}{'}'}</span>
      </div>
    </>
  );
}

export function JsonView({ payload, className }: JsonViewProps) {
  const { t } = useTranslation();
  const parsed = useMemo(() => tryParse(payload), [payload]);

  if (payload == null || payload === '') {
    return (
      <div className={cn('text-sm italic text-muted-foreground', className)}>
        {t('auditLogs.detail.noPayload')}
      </div>
    );
  }

  if (!parsed.ok) {
    return (
      <div className={cn('font-mono text-xs', className)} dir="ltr">
        <div className="mb-2 text-xs text-muted-foreground">{t('auditLogs.detail.rawPayload')}</div>
        <pre className="whitespace-pre-wrap break-all">{parsed.raw}</pre>
      </div>
    );
  }

  const lineNo = { n: 1 };

  return (
    <div
      className={cn('overflow-x-auto font-mono text-xs leading-relaxed', className)}
      dir="ltr"
      role="region"
      aria-label={t('auditLogs.detail.eventPayload')}
      tabIndex={0}
    >
      <div className="-mx-2 flex px-2">
        <span className="w-8 shrink-0 select-none text-xs text-primary/50">{lineNo.n}</span>
        <span>{renderValue(parsed.value, 0, lineNo)}</span>
      </div>
    </div>
  );
}
