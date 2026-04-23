import { useTranslation } from 'react-i18next';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

export type WindowValue = '7d' | '30d' | '90d' | 'all';

interface Props {
  value: WindowValue;
  onChange: (v: WindowValue) => void;
}

export function WindowSelector({ value, onChange }: Props) {
  const { t } = useTranslation();
  return (
    <Select value={value} onValueChange={(v) => onChange(v as WindowValue)}>
      <SelectTrigger className="w-44">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="7d">{t('workflow.analytics.window.7d')}</SelectItem>
        <SelectItem value="30d">{t('workflow.analytics.window.30d')}</SelectItem>
        <SelectItem value="90d">{t('workflow.analytics.window.90d')}</SelectItem>
        <SelectItem value="all">{t('workflow.analytics.window.all')}</SelectItem>
      </SelectContent>
    </Select>
  );
}
