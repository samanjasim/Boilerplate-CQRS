export interface SystemSetting {
  id: string;
  key: string;
  value: string;
  description: string | null;
  category: string | null;
  dataType: string;
  isSecret: boolean;
  isOverridden: boolean;
}

export interface SettingGroup {
  category: string;
  settings: SystemSetting[];
}

export interface UpdateSettingData {
  key: string;
  value: string;
}
