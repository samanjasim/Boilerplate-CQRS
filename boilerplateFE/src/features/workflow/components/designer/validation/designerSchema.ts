import { z } from 'zod';
import type { WorkflowStateConfig, WorkflowTransitionConfig } from '@/types/workflow.types';

const SLUG = /^[A-Za-z][A-Za-z0-9_]*$/;
const KNOWN_TYPES = ['Initial', 'HumanTask', 'SystemAction', 'Terminal'] as const;

export const stateSchema = z.object({
  name: z.string()
    .min(1, { message: 'nameRequired' })
    .max(80)
    .regex(SLUG, { message: 'nameSlug' }),
  displayName: z.string().min(1, { message: 'displayNameRequired' }).max(120),
  type: z.enum(KNOWN_TYPES, { message: 'typeUnknown' }),
  assignee: z.unknown().optional().nullable(),
  actions: z.array(z.string()).optional().nullable(),
  onEnter: z.unknown().optional().nullable(),
  onExit: z.unknown().optional().nullable(),
  formFields: z.unknown().optional().nullable(),
  parallel: z.unknown().optional().nullable(),
  sla: z.object({
    reminderAfterHours: z.number().nullable().optional(),
    escalateAfterHours: z.number().nullable().optional(),
  }).optional().nullable().refine(sla => {
    if (!sla) return true;
    const r = sla.reminderAfterHours;
    const e = sla.escalateAfterHours;
    if (r == null || e == null) return true;
    return r < e;
  }, { message: 'slaOrder' }),
  uiPosition: z.object({ x: z.number(), y: z.number() }).optional().nullable(),
});

export const transitionSchema = z.object({
  from: z.string().min(1),
  to: z.string().min(1),
  trigger: z.string().min(1, { message: 'triggerRequired' }),
  type: z.string().optional(),
  condition: z.unknown().optional().nullable(),
});

export interface ValidationIssue {
  path: string;        // e.g. "states[2].name" or "graph"
  messageKey: string;  // i18n key under workflow.designer.errors
  params?: Record<string, unknown>;
}

export function validateDefinition(
  states: WorkflowStateConfig[],
  transitions: WorkflowTransitionConfig[],
): ValidationIssue[] {
  const issues: ValidationIssue[] = [];

  // Per-state schema
  states.forEach((state, i) => {
    const result = stateSchema.safeParse(state);
    if (!result.success) {
      for (const issue of result.error.issues) {
        issues.push({
          path: `states[${i}].${issue.path.join('.')}`,
          messageKey: issue.message || 'unknown',
        });
      }
    }
    // HumanTask requires assignee strategy
    if (state.type === 'HumanTask') {
      const a = state.assignee;
      if (!a || !(a as { strategy?: string }).strategy) {
        issues.push({
          path: `states[${i}].assignee`,
          messageKey: 'assigneeRequiredForHumanTask',
        });
      }
    }
  });

  // Uniqueness
  const seenNames = new Set<string>();
  states.forEach((state, i) => {
    const key = state.name?.toLowerCase();
    if (!key) return;
    if (seenNames.has(key)) {
      issues.push({ path: `states[${i}].name`, messageKey: 'nameUnique' });
    }
    seenNames.add(key);
  });

  // Exactly one Initial
  const initialCount = states.filter(s => s.type === 'Initial').length;
  if (initialCount !== 1) {
    issues.push({ path: 'graph', messageKey: 'exactlyOneInitial', params: { count: initialCount } });
  }

  // At least one Terminal
  if (!states.some(s => s.type === 'Terminal')) {
    issues.push({ path: 'graph', messageKey: 'atLeastOneTerminal' });
  }

  // Transitions
  const stateByName = new Map(states.map(s => [s.name.toLowerCase(), s]));
  transitions.forEach((t, i) => {
    const result = transitionSchema.safeParse(t);
    if (!result.success) {
      for (const issue of result.error.issues) {
        issues.push({
          path: `transitions[${i}].${issue.path.join('.')}`,
          messageKey: issue.message || 'unknown',
        });
      }
    }

    const from = stateByName.get(t.from?.toLowerCase());
    if (!from) issues.push({ path: `transitions[${i}].from`, messageKey: 'fromUnknown' });
    else if (from.type === 'Terminal') issues.push({ path: `transitions[${i}].from`, messageKey: 'fromTerminal' });

    if (!stateByName.get(t.to?.toLowerCase())) {
      issues.push({ path: `transitions[${i}].to`, messageKey: 'toUnknown' });
    }
  });

  // Duplicate (from, trigger)
  const seenPairs = new Set<string>();
  transitions.forEach((t, i) => {
    if (!t.trigger) return;
    const key = `${t.from.toLowerCase()}::${t.trigger.toLowerCase()}`;
    if (seenPairs.has(key)) {
      issues.push({ path: `transitions[${i}]`, messageKey: 'duplicateFromTrigger' });
    }
    seenPairs.add(key);
  });

  return issues;
}
