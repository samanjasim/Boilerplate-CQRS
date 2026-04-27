import { Outlet } from 'react-router-dom';

export function PublicLayout() {
  return (
    <div className="aurora-canvas relative min-h-screen">
      <Outlet />
    </div>
  );
}
