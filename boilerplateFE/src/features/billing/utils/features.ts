import type { PlanFeatureEntry } from '@/types';

/**
 * Get up to 6 feature highlights for plan cards (excludes disabled features).
 */
export function getFeatureHighlights(features: PlanFeatureEntry[]): string[] {
  if (!Array.isArray(features) || features.length === 0) return [];
  return features
    .filter((f) => !(f.value === 'false'))
    .slice(0, 6)
    .map((f) => {
      const label = f.translations?.en?.label;
      if (label) return label;
      return f.key.replace(/([A-Z])/g, ' $1').replace(/^./, (s) => s.toUpperCase()).trim();
    });
}

/**
 * Get all feature labels for plan selector (excludes disabled features).
 */
export function getFeatureLabels(features: PlanFeatureEntry[]): string[] {
  if (!Array.isArray(features) || features.length === 0) return [];
  return features
    .filter((f) => !(f.value === 'false'))
    .map((f) => {
      const label = f.translations?.en?.label;
      if (label) return label;
      return f.key.replace(/([A-Z])/g, ' $1').replace(/^./, (s) => s.toUpperCase()).trim();
    });
}
