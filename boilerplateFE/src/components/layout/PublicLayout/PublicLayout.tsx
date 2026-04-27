import { Outlet } from 'react-router-dom';

export function PublicLayout() {
  return (
    <div className="aurora-canvas relative min-h-screen">
      <div aria-hidden className="aurora-layer-2" />
      <Outlet />
    </div>
  );
}
