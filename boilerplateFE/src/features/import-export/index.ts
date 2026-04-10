import { lazy } from 'react';
import { registerSlot } from '@/lib/extensions';

/**
 * Import/Export module entry point.
 *
 * Contributes a button to the users-list toolbar that opens the ImportWizard
 * preconfigured for the Users entity type. The component is loaded lazily so
 * it doesn't ship in builds where the module is disabled.
 */
const UsersImportButton = lazy(() =>
  import('./components/UsersImportButton').then((m) => ({ default: m.UsersImportButton })),
);

export const importExportModule = {
  name: 'importExport',
  register(): void {
    registerSlot('users-list-toolbar', {
      id: 'importExport.users-import',
      module: 'importExport',
      order: 10,
      permission: 'System.ImportData',
      component: UsersImportButton,
    });
  },
};
