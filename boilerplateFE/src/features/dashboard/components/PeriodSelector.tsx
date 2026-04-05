import { useTranslation } from 'react-i18next';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

interface PeriodSelectorProps {
  value: string;
  onChange: (v: string) => void;
}

const PERIODS = ['7d', '30d', '90d', '12m'] as const;

export function PeriodSelector({ value, onChange }: PeriodSelectorProps) {
  const { t } = useTranslation();

  return (
    <Select value={value} onValueChange={onChange}>
      <SelectTrigger className="w-40">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {PERIODS.map((period) => (
          <SelectItem key={period} value={period}>
            {t(`dashboard.${period}`)}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
