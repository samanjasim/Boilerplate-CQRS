import { toast } from 'sonner';
import { importExportApi } from '../api';

export async function downloadImportErrors(jobId: string, errorMessage: string) {
  try {
    const url = await importExportApi.getImportErrorUrl(jobId);
    const link = document.createElement('a');
    link.href = url;
    link.target = '_blank';
    link.rel = 'noopener noreferrer';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  } catch {
    toast.error(errorMessage);
  }
}
