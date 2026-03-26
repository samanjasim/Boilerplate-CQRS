import { Sun, Moon } from 'lucide-react';
import { useUIStore, selectTheme } from '@/stores';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

interface ThemeToggleProps {
  variant?: 'ghost' | 'text';
  className?: string;
}

export function ThemeToggle({ variant = 'ghost', className }: ThemeToggleProps) {
  const theme = useUIStore(selectTheme);
  const setTheme = useUIStore((state) => state.setTheme);

  const toggleTheme = () => {
    setTheme(theme === 'dark' ? 'light' : 'dark');
  };

  if (variant === 'text') {
    return (
      <button
        onClick={toggleTheme}
        className={cn(
          'rounded-xl p-2 text-muted-foreground hover:bg-secondary hover:text-foreground transition-all duration-150',
          className
        )}
      >
        {theme === 'dark' ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
      </button>
    );
  }

  return (
    <Button variant="ghost" size="sm" onClick={toggleTheme} className={className}>
      {theme === 'dark' ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
    </Button>
  );
}
