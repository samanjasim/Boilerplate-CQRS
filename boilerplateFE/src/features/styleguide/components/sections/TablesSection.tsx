import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Section } from '../Section';

const ROWS = [
  { name: 'Acme Corporation', plan: 'enterprise', users: 42, status: 'healthy' as const },
  { name: 'Globex Industries', plan: 'pro', users: 31, status: 'healthy' as const },
  { name: 'Initech Systems', plan: 'pro', users: 28, status: 'healthy' as const },
  { name: 'Hooli AI', plan: 'enterprise', users: 87, status: 'pending' as const },
  { name: 'Pied Piper', plan: 'starter', users: 9, status: 'healthy' as const },
];

export function TablesSection() {
  return (
    <Section
      id="tables"
      eyebrow="Tables"
      title="Glass surface, eyebrow headers"
      deck="Container is .surface-glass. Header row has a subtle copper tint and uppercase 10px tracked labels. Row padding is slightly denser than the previous default."
    >
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Plan</TableHead>
            <TableHead>Users</TableHead>
            <TableHead>Status</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {ROWS.map((r) => (
            <TableRow key={r.name}>
              <TableCell className="font-medium">{r.name}</TableCell>
              <TableCell className="font-mono text-xs text-muted-foreground">{r.plan}</TableCell>
              <TableCell className="font-mono text-xs">{r.users}</TableCell>
              <TableCell>
                <Badge variant={r.status === 'pending' ? 'pending' : 'healthy'}>
                  {r.status === 'pending' ? 'Trial' : 'Active'}
                </Badge>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Section>
  );
}
