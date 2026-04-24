import { useTranslation } from 'react-i18next';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Label } from '@/components/ui/label';
import { Checkbox } from '@/components/ui/checkbox';
import type { FormFieldDefinition } from '@/types/workflow.types';

interface DynamicFormRendererProps {
  fields: FormFieldDefinition[];
  values: Record<string, unknown>;
  onChange: (name: string, value: unknown) => void;
  /** Per-field validation messages. One or more messages per field; every one is rendered. */
  errors?: Record<string, string[]>;
}

export function DynamicFormRenderer({ fields, values, onChange, errors }: DynamicFormRendererProps) {
  const { t } = useTranslation();

  return (
    <div className="space-y-4">
      {fields.map((field) => (
        <div key={field.name} className="space-y-1.5">
          {field.type !== 'checkbox' && (
            <Label htmlFor={`form-field-${field.name}`} className="text-sm font-medium text-foreground">
              {field.label}
              {field.required && <span className="text-destructive ms-0.5">*</span>}
            </Label>
          )}

          {field.type === 'text' && (
            <Input
              id={`form-field-${field.name}`}
              value={(values[field.name] as string) ?? ''}
              onChange={(e) => onChange(field.name, e.target.value)}
              placeholder={field.placeholder}
              maxLength={field.maxLength}
            />
          )}

          {field.type === 'textarea' && (
            <Textarea
              id={`form-field-${field.name}`}
              value={(values[field.name] as string) ?? ''}
              onChange={(e) => onChange(field.name, e.target.value)}
              placeholder={field.placeholder}
              maxLength={field.maxLength}
              rows={3}
            />
          )}

          {field.type === 'number' && (
            <Input
              id={`form-field-${field.name}`}
              type="number"
              value={(values[field.name] as number | undefined) ?? ''}
              onChange={(e) => {
                // Empty input becomes undefined (not ''), so the backend
                // validator sees a missing field instead of a string where
                // a number is expected.
                const raw = e.target.value;
                onChange(field.name, raw === '' ? undefined : Number(raw));
              }}
              placeholder={field.placeholder}
              min={field.min}
              max={field.max}
            />
          )}

          {field.type === 'date' && (
            <Input
              id={`form-field-${field.name}`}
              type="date"
              value={(values[field.name] as string) ?? ''}
              onChange={(e) => onChange(field.name, e.target.value)}
            />
          )}

          {field.type === 'select' && (
            <Select
              value={(values[field.name] as string) ?? ''}
              onValueChange={(val) => onChange(field.name, val)}
            >
              <SelectTrigger id={`form-field-${field.name}`}>
                <SelectValue placeholder={field.placeholder ?? t('workflow.forms.selectPlaceholder', 'Select...')} />
              </SelectTrigger>
              <SelectContent>
                {field.options?.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}

          {field.type === 'checkbox' && (
            <div className="flex items-center gap-2">
              <Checkbox
                id={`form-field-${field.name}`}
                checked={!!values[field.name]}
                onCheckedChange={(checked) => onChange(field.name, checked === true)}
              />
              <Label htmlFor={`form-field-${field.name}`} className="text-sm font-medium text-foreground">
                {field.label}
                {field.required && <span className="text-destructive ms-0.5">*</span>}
              </Label>
            </div>
          )}

          {field.description && (
            <p className="text-xs text-muted-foreground">{field.description}</p>
          )}

          {errors?.[field.name]?.map((msg, i) => (
            <p key={i} className="text-xs text-destructive">{msg}</p>
          ))}
        </div>
      ))}
    </div>
  );
}
