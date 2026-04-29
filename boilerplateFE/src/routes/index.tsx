import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { createRoutes } from './routes';

let cachedRouter: ReturnType<typeof createBrowserRouter> | null = null;

function getRouter() {
  if (!cachedRouter) {
    cachedRouter = createBrowserRouter(createRoutes());
  }
  return cachedRouter;
}

export function AppRouter() {
  return <RouterProvider router={getRouter()} />;
}
