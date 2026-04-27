import { LogOut, Menu, Search, User, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';

import {
  LanguageSwitcher,
  NotificationBell,
  ThemeToggle,
  UserAvatar,
} from '@/components/common';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { ROUTES } from '@/config';
import { useLogout } from '@/features/auth/api';
import { cn } from '@/lib/utils';
import {
  selectSidebarCollapsed,
  selectSidebarOpen,
  selectUser,
  useAuthStore,
  useUIStore,
} from '@/stores';

export function Header() {
  const { t } = useTranslation();
  const user = useAuthStore(selectUser);
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const sidebarOpen = useUIStore(selectSidebarOpen);
  const toggleSidebar = useUIStore((state) => state.toggleSidebar);
  const navigate = useNavigate();
  const handleLogout = useLogout();

  // Search-bar trigger doubles as the mobile drawer trigger.
  // On lg+ this is a placeholder for the command palette (future plan).
  // For now the click handler still calls toggleSidebar — harmless on lg+
  // (sidebarOpen has no UI effect there) and opens the drawer on <lg.
  const onSearchClick = () => toggleSidebar();

  return (
    <header
      className={cn(
        'fixed top-3.5 z-30 h-12 flex items-center gap-2 rounded-2xl px-3',
        'surface-floating',
        'motion-safe:transition-all motion-safe:duration-300',
        // start edge follows sidebar width + 14px gap on lg+; flush on <lg
        'max-lg:ltr:left-3.5 max-lg:rtl:right-3.5',
        isCollapsed
          ? 'lg:ltr:left-[calc(4rem+1.75rem)] lg:rtl:right-[calc(4rem+1.75rem)]'
          : 'lg:ltr:left-[calc(15rem+1.75rem)] lg:rtl:right-[calc(15rem+1.75rem)]',
        'ltr:right-3.5 rtl:left-3.5'
      )}
    >
      {/* Search-bar trigger / mobile drawer trigger */}
      <button
        type="button"
        onClick={onSearchClick}
        aria-label={sidebarOpen ? t('nav.toggle.close') : t('nav.toggle.open')}
        aria-expanded={sidebarOpen}
        className={cn(
          'flex h-8 items-center gap-2 rounded-[9px] border border-white/10 bg-white/5 px-3',
          'text-sm text-muted-foreground',
          'motion-safe:transition-colors motion-safe:duration-150',
          'hover:bg-white/8 hover:text-foreground',
          'flex-1 max-w-[320px]'
        )}
      >
        {/* Mobile shows menu/X icon; desktop shows search icon */}
        <span className="lg:hidden">
          {sidebarOpen ? <X className="h-4 w-4" /> : <Menu className="h-4 w-4" />}
        </span>
        <Search className="hidden lg:block h-4 w-4 opacity-60" />
        <span className="hidden lg:inline flex-1 text-start">{t('header.searchPlaceholder')}</span>
        <span className="hidden lg:inline ms-auto rounded-md border border-white/15 bg-white/8 px-1.5 py-0.5 font-mono text-[10px] tracking-[0.05em] text-muted-foreground">
          ⌘K
        </span>
      </button>

      {/* Spacer pushes right cluster to the end */}
      <div className="flex-1 max-lg:hidden" />

      {/* Right cluster — language, theme, notifications, avatar */}
      <div className="flex items-center gap-1">
        <LanguageSwitcher />
        <ThemeToggle />
        <NotificationBell />

        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button
              type="button"
              className={cn(
                'ms-1 flex items-center gap-2 rounded-full border border-white/8 bg-white/5 ps-1 pe-3 py-1',
                'motion-safe:transition-colors motion-safe:duration-150',
                'hover:bg-white/8'
              )}
            >
              <UserAvatar firstName={user?.firstName} lastName={user?.lastName} size="sm" />
              <span className="hidden sm:inline text-sm font-medium text-foreground">
                {user?.firstName} {user?.lastName}
              </span>
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-56">
            <DropdownMenuLabel className="font-normal">
              <div className="flex flex-col space-y-1">
                <p className="text-sm font-medium">
                  {user?.firstName} {user?.lastName}
                </p>
                <p className="text-xs text-muted-foreground">{user?.email}</p>
              </div>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              onClick={() => navigate(ROUTES.PROFILE)}
              className="cursor-pointer"
            >
              <User className="h-4 w-4" />
              {t('profile.title')}
            </DropdownMenuItem>
            <DropdownMenuItem
              onClick={handleLogout}
              className="cursor-pointer text-destructive focus:text-destructive"
            >
              <LogOut className="h-4 w-4" />
              {t('header.logout')}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
