import { useCallback, useMemo, useState } from 'react';
import type { PaginationMeta, PaginationParams, SortParams } from '@/types';
import { getPersistedPageSize, persistPageSize } from '@/components/common/pagination-utils';

type FiltersBase = object;

type ListPageParams<TFilters extends FiltersBase> = TFilters & PaginationParams & SortParams;

/**
 * Minimum shape we need from a TanStack Query result. We don't insist on
 * `UseQueryResult<PaginatedResponse<T>>` because most feature query hooks
 * return loosely-typed `any` from their `apiClient.get(...).then(r => r.data)`
 * chains, and forcing the strict type would gate adoption on retyping every
 * feature API.
 */
interface PaginatedQueryResult<TRow> {
  data?: { data?: TRow[]; pagination?: PaginationMeta } | undefined;
  isLoading: boolean;
  isError: boolean;
  isFetching: boolean;
  refetch: () => unknown;
}

/**
 * Accepts any function shape that consumes a params object and returns a
 * paginated query result. The permissive `params` type lets consumers pass
 * existing feature hooks (e.g. `useProducts(params?: Record<string, unknown>)`)
 * without retyping their feature APIs. The runtime always passes a plain
 * object, so any consumer is safe.
 */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type AnyQueryHook<TRow> = (params: any) => PaginatedQueryResult<TRow>;

interface UseListPageOptions<TFilters extends FiltersBase, TRow> {
  /**
   * TanStack Query hook that accepts the merged params object. Examples:
   *   useUsers, useProducts, useAuditLogs.
   */
  queryHook: AnyQueryHook<TRow>;
  /** Static values merged into every request — typically sort defaults. */
  defaults?: Partial<TFilters & SortParams>;
  /** Initial filter values. Empty/undefined entries are omitted from requests. */
  initialFilters?: Partial<TFilters>;
  /** Override persisted page size for first render. */
  initialPageSize?: number;
}

/**
 * Composes the standard list-page state machine: pagination + filters + a
 * TanStack query hook. Eliminates ~40 lines of boilerplate per list page.
 *
 * Returns rich derived state so consumers can render with minimal logic:
 *   - `data` / `pagination` — already-unwrapped from the API envelope
 *   - `isInitialLoading` — first load, no cached data yet
 *   - `isEmpty` — query settled with zero rows
 *   - `setFilter(key, value)` — sets filter; resets page to 1; empty value clears
 *   - `setPage`, `setPageSize`, `resetFilters`, `refetch`
 */
export function useListPage<TFilters extends FiltersBase, TRow>({
  queryHook,
  defaults,
  initialFilters,
  initialPageSize,
}: UseListPageOptions<TFilters, TRow>) {
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(initialPageSize ?? getPersistedPageSize());
  const [filters, setFilters] = useState<Partial<TFilters>>(initialFilters ?? {});

  const params = useMemo(() => {
    const cleaned: Record<string, unknown> = {};
    for (const [key, value] of Object.entries(filters)) {
      if (value !== '' && value !== undefined && value !== null) {
        cleaned[key] = value;
      }
    }
    return {
      pageNumber,
      pageSize,
      ...(defaults as object),
      ...cleaned,
    } as ListPageParams<TFilters>;
  }, [pageNumber, pageSize, filters, defaults]);

  const query = queryHook(params);

  const data = query.data?.data ?? [];
  const pagination = query.data?.pagination;

  const setFilter = useCallback(
    <K extends keyof TFilters>(key: K, value: TFilters[K] | '' | undefined | null) => {
      setFilters((prev) => {
        const next = { ...prev };
        if (value === '' || value === undefined || value === null) {
          delete next[key];
        } else {
          next[key] = value as TFilters[K];
        }
        return next;
      });
      setPageNumber(1);
    },
    [],
  );

  const handlePageSizeChange = useCallback((size: number) => {
    persistPageSize(size);
    setPageSize(size);
    setPageNumber(1);
  }, []);

  const resetFilters = useCallback(() => {
    setFilters(initialFilters ?? {});
    setPageNumber(1);
  }, [initialFilters]);

  return {
    // data
    data: data as TRow[],
    pagination,
    // status
    isLoading: query.isLoading,
    isError: query.isError,
    isFetching: query.isFetching,
    isInitialLoading: query.isLoading && !query.data,
    isEmpty: !query.isLoading && data.length === 0,
    // current state
    params,
    filters,
    pageNumber,
    pageSize,
    // actions
    setFilter,
    setPage: setPageNumber,
    setPageSize: handlePageSizeChange,
    resetFilters,
    refetch: query.refetch,
  };
}

export type UseListPageReturn<TRow> = ReturnType<typeof useListPage<FiltersBase, TRow>>;
