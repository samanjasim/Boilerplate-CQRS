import { cn } from '@/lib/utils';

function getInitials(firstName?: string, lastName?: string): string {
  const f = firstName?.charAt(0)?.toUpperCase() ?? '';
  const l = lastName?.charAt(0)?.toUpperCase() ?? '';
  return f + l || '?';
}

interface UserAvatarProps {
  firstName?: string;
  lastName?: string;
  size?: 'xs' | 'sm' | 'md' | 'lg';
  className?: string;
}

const sizes = {
  xs: 'h-6 w-6 text-[10px]',
  sm: 'h-8 w-8 text-xs',
  md: 'h-10 w-10 text-sm',
  lg: 'h-12 w-12 text-base',
};

export function UserAvatar({ firstName, lastName, size = 'sm', className }: UserAvatarProps) {
  const initials = getInitials(firstName, lastName);

  return (
    <div
      className={cn(
        'flex items-center justify-center rounded-lg btn-primary-gradient font-semibold text-primary-foreground shrink-0',
        sizes[size],
        className
      )}
    >
      {initials}
    </div>
  );
}
