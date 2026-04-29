import { useTranslation } from 'react-i18next';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { cn } from '@/lib/utils';
import {
  FILE_CATEGORIES,
  getFileCategoryLabel,
} from '../utils/file-display';

export type FilesViewFilter = 'all' | 'mine' | 'shared' | 'public';

const VIEW_FILTERS: FilesViewFilter[] = ['all', 'mine', 'shared', 'public'];

export interface FilesFilterPanelProps {
  view: FilesViewFilter;
  onViewChange: (view: FilesViewFilter) => void;
  category: string;
  onCategoryChange: (category: string) => void;
  origin: string;
  onOriginChange: (origin: string) => void;
  searchTerm: string;
  onSearchTermChange: (searchTerm: string) => void;
}

export function FilesFilterPanel({
  view,
  onViewChange,
  category,
  onCategoryChange,
  origin,
  onOriginChange,
  searchTerm,
  onSearchTermChange,
}: FilesFilterPanelProps) {
  const { t } = useTranslation();

  return (
    <>
      <div className="flex gap-1 border-b">
        {VIEW_FILTERS.map((filter) => (
          <button
            key={filter}
            type="button"
            className={cn(
              '-mb-px border-b-2 px-4 py-2 text-sm transition-colors',
              view === filter
                ? 'border-primary font-medium text-foreground'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            )}
            onClick={() => onViewChange(filter)}
          >
            {t(`files.views.${filter}`)}
          </button>
        ))}
      </div>

      <Card>
        <CardContent className="py-4">
          <div className="flex flex-wrap items-center gap-4">
            <div className="w-48">
              <Select value={category} onValueChange={onCategoryChange}>
                <SelectTrigger>
                  <SelectValue placeholder={t('files.category')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">{t('files.allCategories')}</SelectItem>
                  {FILE_CATEGORIES.map((fileCategory) => (
                    <SelectItem key={fileCategory} value={fileCategory}>
                      {getFileCategoryLabel(fileCategory, t)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="w-48">
              <Select value={origin || 'all'} onValueChange={onOriginChange}>
                <SelectTrigger>
                  <SelectValue placeholder={t('files.allFiles')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">{t('files.allFiles')}</SelectItem>
                  <SelectItem value="UserUpload">{t('files.myFiles')}</SelectItem>
                  <SelectItem value="SystemGenerated">{t('files.systemFiles')}</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="min-w-[200px] flex-1">
              <Input
                placeholder={t('common.search')}
                value={searchTerm}
                onChange={(event) => onSearchTermChange(event.target.value)}
              />
            </div>
          </div>
        </CardContent>
      </Card>
    </>
  );
}
