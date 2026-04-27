import { Inbox } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/common/EmptyState';
import { PageHeader } from '@/components/common/PageHeader';
import { Pagination } from '@/components/common/Pagination';
import { Section } from '../Section';

export function CommonSection() {
  const [page, setPage] = useState(3);
  return (
    <Section
      id="common"
      eyebrow="Common components"
      title="PageHeader · Pagination · EmptyState"
      deck="The core list-page chrome. PageHeader uses J4 section-title typography. Pagination's active page renders with the gradient + glow primary. EmptyState becomes a glass tile with copper accent."
    >
      <div className="surface-glass rounded-2xl p-6">
        <PageHeader
          title="Tenants"
          subtitle="142 active · 8 added this month"
          actions={<Button>+ New tenant</Button>}
        />
      </div>

      <div className="surface-glass rounded-2xl p-6">
        <Pagination
          pagination={{
            pageNumber: page,
            pageSize: 20,
            totalPages: 8,
            totalCount: 156,
            hasNextPage: page < 8,
            hasPreviousPage: page > 1,
          }}
          onPageChange={setPage}
          onPageSizeChange={() => {}}
        />
      </div>

      <div className="surface-glass rounded-2xl">
        <EmptyState
          icon={Inbox}
          title="No notifications yet"
          description="When something happens that needs your attention, it'll show up here."
          action={{ label: 'Configure preferences', onClick: () => {} }}
        />
      </div>
    </Section>
  );
}
