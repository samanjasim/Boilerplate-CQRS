export function formatDelegationDates(
  startDate: string,
  endDate: string,
): { startDate: string; endDate: string } {
  return {
    startDate: `${startDate}T00:00:00.000Z`,
    endDate: `${endDate}T23:59:59.999Z`,
  };
}
