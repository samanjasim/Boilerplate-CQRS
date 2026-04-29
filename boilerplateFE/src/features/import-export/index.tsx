import { lazy } from 'react';
import { PermissionGuard } from '@/components/guards';
import { ROUTES } from '@/config';
import { PERMISSIONS } from '@/constants';
import type { WebModule } from '@/lib/modules';

/**
 * Import/Export module entry point.
 *
 * Contributes a button to the users-list toolbar that opens the ImportWizard
 * preconfigured for the Users entity type. The component is loaded lazily so
 * it doesn't ship in builds where the module is disabled.
 */
const UsersImportButton = lazy(() =>
  import('./components/UsersImportButton').then((m) => ({
    default: m.UsersImportButton,
  })),
);

const ImportExportPage = lazy(() => import('./pages/ImportExportPage'));

export const importExportModule: WebModule = {
  id: 'importExport',
  register(ctx): void {
    ctx.registerSlot('users-list-toolbar', {
      id: 'importExport.users-import',
      module: 'importExport',
      order: 10,
      permission: 'System.ImportData',
      component: UsersImportButton,
    });

    ctx.registerRoute({
      id: 'importExport.index',
      region: 'protected',
      order: 40,
      route: {
        element: (
          <PermissionGuard permissions={[PERMISSIONS.System.ExportData, PERMISSIONS.System.ImportData]} mode="any" />
        ),
        children: [{ path: ROUTES.IMPORT_EXPORT, element: <ImportExportPage /> }],
      },
    });
  },
};
