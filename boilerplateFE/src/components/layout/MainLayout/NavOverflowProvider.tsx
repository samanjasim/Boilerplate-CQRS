import { NavOverflowContext, useNavOverflowImpl } from './useNavOverflow';

export function NavOverflowProvider({ children }: { children: React.ReactNode }) {
  const value = useNavOverflowImpl();
  return <NavOverflowContext.Provider value={value}>{children}</NavOverflowContext.Provider>;
}
