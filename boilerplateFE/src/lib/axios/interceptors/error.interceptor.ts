import type { AxiosInstance, AxiosError } from 'axios';
import { toast } from 'sonner';
import i18n from '@/i18n';
import type { ApiError } from '@/types';

const getErrorMessage = (error: AxiosError<ApiError>): string => {
  const status = error.response?.status;
  const errorData = error.response?.data;

  if (!error.response) {
    if (error.code === 'ERR_NETWORK') {
      return i18n.t('errors.networkError');
    }
    if (error.code === 'ECONNABORTED') {
      return i18n.t('errors.timeout');
    }
    return i18n.t('errors.networkError');
  }

  if (errorData?.message) {
    return errorData.message;
  }

  if (errorData?.validationErrors) {
    const errorMessages = Object.entries(errorData.validationErrors)
      .map(([, messages]) => {
        if (Array.isArray(messages) && messages.length > 0) {
          return messages[0];
        }
        return null;
      })
      .filter(Boolean);
    if (errorMessages.length > 0) {
      return errorMessages[0] as string;
    }
  }

  switch (status) {
    case 400:
      if (errorData?.errors) {
        const errorMessages = Object.entries(errorData.errors)
          .map(([, messages]) => {
            if (Array.isArray(messages) && messages.length > 0) {
              return messages[0];
            }
            return null;
          })
          .filter(Boolean);
        if (errorMessages.length > 0) {
          return errorMessages[0] as string;
        }
      }
      return errorData?.detail || errorData?.title || i18n.t('errors.badRequest');

    case 401:
      return errorData?.detail || i18n.t('errors.unauthorized');

    case 403:
      return errorData?.detail || i18n.t('errors.forbidden');

    case 404:
      return errorData?.detail || i18n.t('errors.notFound');

    case 409:
      return errorData?.detail || errorData?.title || i18n.t('errors.conflict');

    case 422:
      return errorData?.detail || i18n.t('errors.validationFailed');

    case 500:
      return i18n.t('errors.serverError');

    case 502:
    case 503:
    case 504:
      return i18n.t('errors.serviceUnavailable');

    default:
      if (errorData?.detail) {
        return errorData.detail;
      }
      if (errorData?.title) {
        return errorData.title;
      }
      return i18n.t('errors.unknownError');
  }
};

export const setupErrorInterceptor = (client: AxiosInstance): void => {
  client.interceptors.response.use(
    (response) => response,
    (error: AxiosError<ApiError>) => {
      const message = getErrorMessage(error);

      const isLoginEndpoint = error.config?.url?.includes('/Auth/login');
      const status = error.response?.status;
      const hasValidationErrors =
        !!error.response?.data?.validationErrors &&
        Object.keys(error.response.data.validationErrors).length > 0;
      const suppressForInline =
        error.config?.suppressValidationToast === true && hasValidationErrors;

      if (!suppressForInline && ((status !== 401 && status !== 403) || isLoginEndpoint)) {
        toast.error(message);
      }

      (error as AxiosError & { parsedMessage: string }).parsedMessage = message;

      return Promise.reject(error);
    }
  );
};
