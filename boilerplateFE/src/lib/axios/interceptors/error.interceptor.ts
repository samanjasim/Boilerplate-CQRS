import type { AxiosInstance, AxiosError } from 'axios';
import { toast } from 'sonner';
import type { ApiError } from '@/types';

const getErrorMessage = (error: AxiosError<ApiError>): string => {
  const status = error.response?.status;
  const errorData = error.response?.data;

  if (!error.response) {
    if (error.code === 'ERR_NETWORK') {
      return 'Unable to connect to the server. Please check your internet connection.';
    }
    if (error.code === 'ECONNABORTED') {
      return 'Request timed out. Please try again.';
    }
    return 'Network error. Please check your connection.';
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
      return errorData?.detail || errorData?.title || 'Invalid request. Please check your input.';

    case 401:
      return errorData?.detail || 'Invalid credentials. Please check your email and password.';

    case 403:
      return errorData?.detail || 'You do not have permission to perform this action.';

    case 404:
      return errorData?.detail || 'The requested resource was not found.';

    case 409:
      return errorData?.detail || errorData?.title || 'A conflict occurred. The resource may already exist.';

    case 422:
      return errorData?.detail || 'Validation failed. Please check your input.';

    case 500:
      return 'An internal server error occurred. Please try again later.';

    case 502:
    case 503:
    case 504:
      return 'The server is temporarily unavailable. Please try again later.';

    default:
      if (errorData?.detail) {
        return errorData.detail;
      }
      if (errorData?.title) {
        return errorData.title;
      }
      return 'An unexpected error occurred. Please try again.';
  }
};

export const setupErrorInterceptor = (client: AxiosInstance): void => {
  client.interceptors.response.use(
    (response) => response,
    (error: AxiosError<ApiError>) => {
      const message = getErrorMessage(error);

      const isLoginEndpoint = error.config?.url?.includes('/Auth/login');

      const status = error.response?.status;
      if ((status !== 401 && status !== 403) || isLoginEndpoint) {
        toast.error(message);
      }

      (error as AxiosError & { parsedMessage: string }).parsedMessage = message;

      return Promise.reject(error);
    }
  );
};
