import { useTranslation } from 'react-i18next';
import { CalendarRange, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Input } from '@/components/ui/input';
import { formatDate } from '@/utils/format';

export interface DateRange {
  from?: string;
  to?: string;
}

interface DateRangePickerProps {
  value: DateRange;
  onChange: (next: DateRange) => void;
  placeholder?: string;
  className?: string;
}

function summarize(value: DateRange, fallback: string): string {
  if (value.from && value.to) return `${formatDate(value.from)} → ${formatDate(value.to)}`;
  if (value.from) return `${formatDate(value.from)} → …`;
  if (value.to) return `… → ${formatDate(value.to)}`;
  return fallback;
}

export function DateRangePicker({ value, onChange, placeholder, className }: DateRangePickerProps) {
  const { t } = useTranslation();
  const label = placeholder ?? t('common.dateRange');
  const hasValue = !!value.from || !!value.to;

  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button variant="outline" className={`min-w-56 justify-start gap-2 ${className ?? ''}`}>
          <CalendarRange className="h-4 w-4 text-muted-foreground" />
          <span className="truncate text-sm">{summarize(value, label)}</span>
          {hasValue && (
            <span
              role="button"
              tabIndex={0}
              aria-label={t('common.clear')}
              className="ms-auto inline-flex h-5 w-5 items-center justify-center rounded hover:bg-muted"
              onClick={(e) => {
                e.preventDefault();
                e.stopPropagation();
                onChange({});
              }}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault();
                  onChange({});
                }
              }}
            >
              <X className="h-3.5 w-3.5" />
            </span>
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-72 space-y-3" align="start">
        <div className="space-y-1">
          <Label htmlFor="date-from" className="text-xs">{t('common.from')}</Label>
          <Input
            id="date-from"
            type="date"
            value={value.from ?? ''}
            max={value.to}
            onChange={(e) => onChange({ ...value, from: e.target.value || undefined })}
          />
        </div>
        <div className="space-y-1">
          <Label htmlFor="date-to" className="text-xs">{t('common.to')}</Label>
          <Input
            id="date-to"
            type="date"
            value={value.to ?? ''}
            min={value.from}
            onChange={(e) => onChange({ ...value, to: e.target.value || undefined })}
          />
        </div>
        {hasValue && (
          <div className="flex justify-end">
            <Button variant="ghost" size="sm" onClick={() => onChange({})}>
              {t('common.clear')}
            </Button>
          </div>
        )}
      </PopoverContent>
    </Popover>
  );
}
