import 'axios';

declare module 'axios' {
  export interface AxiosRequestConfig {
    /**
     * Set to true when the caller renders `validationErrors` (field-level
     * server validation) inline. The global error interceptor will then
     * skip the toast on 400-with-validationErrors responses, so the user
     * sees only the inline messages. Non-validation errors (401, 500, etc.)
     * still toast.
     */
    suppressValidationToast?: boolean;
  }
}
