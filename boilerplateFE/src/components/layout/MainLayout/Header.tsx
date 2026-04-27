import { LogOut, User, Menu, X, ArrowLeft } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useNavigate, Link } from 'react-router-dom';
import {
  useAuthStore,
  selectUser,
  useUIStore,
  selectSidebarCollapsed,
  selectSidebarOpen,
  selectBackNavigation,
} from '@/stores';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { cn } from '@/lib/utils';
import { useLogout } from '@/features/auth/api';
import { LanguageSwitcher, ThemeToggle, NotificationBell, UserAvatar } from '@/components/common';
import { ROUTES } from '@/config';

export function Header() {
  const { t } = useTranslation();
  const user = useAuthStore(selectUser);
  const isCollapsed = useUIStore(selectSidebarCollapsed);
  const sidebarOpen = useUIStore(selectSidebarOpen);
  const backNavigation = useUIStore(selectBackNavigation);
  const toggleSidebar = useUIStore((state) => state.toggleSidebar);
  const navigate = useNavigate();
  const handleLogout = useLogout();

  return (
    <header
      className={cn(
        'fixed top-0 z-30 flex h-14 items-center justify-between px-6 transition-all duration-300',
        'surface-glass border-b border-border/40',
        isCollapsed
          ? 'ltr:left-16 rtl:right-16 ltr:right-0 rtl:left-0'
          : 'ltr:left-60 rtl:right-60 ltr:right-0 rtl:left-0'
      )}
    >
      {/* Left side: back button or mobile menu toggle */}
      <div className="flex items-center gap-3">
        <Button
          variant="ghost"
          size="icon"
          onClick={toggleSidebar}
          className="lg:hidden"
          aria-label={sidebarOpen ? 'Close navigation' : 'Open navigation'}
          aria-expanded={sidebarOpen}
        >
          {sidebarOpen ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
        </Button>
        {backNavigation && (
          <Link
            to={backNavigation.to}
            className="hidden lg:flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground transition-colors"
          >
            <ArrowLeft className="h-4 w-4 rtl:rotate-180" />
            <span>{backNavigation.label}</span>
          </Link>
        )}
      </div>

      {/* Right side: controls */}
      <div className="flex items-center gap-1">
        <LanguageSwitcher />
        <ThemeToggle />
        <NotificationBell />

        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button className="flex items-center gap-2.5 ltr:ml-2 rtl:mr-2 rounded-xl px-2 py-1.5 transition-colors duration-150 hover:bg-secondary/80">
              <UserAvatar firstName={user?.firstName} lastName={user?.lastName} size="sm" />
              <span className="hidden sm:inline text-sm font-medium text-foreground">
                {user?.firstName} {user?.lastName}
              </span>
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-56">
            <DropdownMenuLabel className="font-normal">
              <div className="flex flex-col space-y-1">
                <p className="text-sm font-medium">{user?.firstName} {user?.lastName}</p>
                <p className="text-xs text-muted-foreground">{user?.email}</p>
              </div>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => navigate(ROUTES.PROFILE)} className="cursor-pointer">
              <User className="h-4 w-4" />
              {t('profile.title')}
            </DropdownMenuItem>
            <DropdownMenuItem onClick={handleLogout} className="cursor-pointer text-destructive focus:text-destructive">
              <LogOut className="h-4 w-4" />
              {t('header.logout')}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
