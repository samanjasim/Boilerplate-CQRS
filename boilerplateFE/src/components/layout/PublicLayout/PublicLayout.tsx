import { Outlet } from 'react-router-dom';
import { LanguageSwitcher, ThemeToggle } from '@/components/common';

export function PublicLayout() {
  return (
    <div className="aurora-canvas relative min-h-screen">
      <div className="absolute top-4 right-4 z-50 flex items-center gap-2 text-white [&_button]:text-white/80 [&_button:hover]:text-white">
        <LanguageSwitcher variant="text" />
        <ThemeToggle variant="text" />
      </div>
      <Outlet />
    </div>
  );
}
