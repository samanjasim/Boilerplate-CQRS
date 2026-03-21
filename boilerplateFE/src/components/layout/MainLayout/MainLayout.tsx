import { Outlet } from 'react-router-dom';
import { useUIStore, selectSidebarCollapsed } from '@/stores';
import { cn } from '@/lib/utils';
import { Sidebar } from './Sidebar';
import { Header } from './Header';

export function MainLayout() {
  const isCollapsed = useUIStore(selectSidebarCollapsed);

  return (
    <div className="min-h-screen bg-background">
      <Sidebar />
      <Header />
      <main
        className={cn(
          'pt-16 transition-all duration-300',
          isCollapsed ? 'ltr:pl-16 rtl:pr-16' : 'ltr:pl-64 rtl:pr-64'
        )}
      >
        <div className="p-6">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
