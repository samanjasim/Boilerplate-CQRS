export interface DashboardAnalytics {
  period: string;
  enabledSections: string[];
  summary: Record<string, SummaryMetric>;
  charts: Record<string, TimeSeriesPoint[]>;
}

export interface SummaryMetric {
  current: number;
  previous: number;
  trend: number | null;
}

export interface TimeSeriesPoint {
  date: string;
  value: number;
}
