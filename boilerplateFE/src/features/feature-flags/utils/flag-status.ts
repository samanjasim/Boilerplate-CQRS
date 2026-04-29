import type { TFunction } from 'i18next';
import type { FeatureFlagDto } from '../api';

export type FlagStatusVariant = 'healthy' | 'failed' | 'info' | 'secondary';

const isBooleanType = (flag: FeatureFlagDto): boolean =>
  flag.valueType === 'Boolean' || (flag.valueType as unknown) === 0;

const isTruthyBoolean = (value: string | null | undefined): boolean =>
  (value ?? '').trim().toLowerCase() === 'true';

export function getFlagStatus(flag: FeatureFlagDto, t: TFunction): {
  variant: FlagStatusVariant;
  label: string;
} {
  if (flag.tenantOverrideValue !== null) {
    return { variant: 'info', label: t('featureFlags.status.perTenant') };
  }
  if (!isBooleanType(flag)) {
    return { variant: 'secondary', label: t('featureFlags.status.configured') };
  }
  return isTruthyBoolean(flag.resolvedValue)
    ? { variant: 'healthy', label: t('featureFlags.status.on') }
    : { variant: 'failed', label: t('featureFlags.status.off') };
}
