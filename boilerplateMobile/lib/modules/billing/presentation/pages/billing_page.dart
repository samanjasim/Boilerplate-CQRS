import 'package:boilerplate_mobile/core/widgets/empty_state.dart';
import 'package:boilerplate_mobile/core/widgets/error_view.dart';
import 'package:boilerplate_mobile/core/widgets/loading_view.dart';
import 'package:boilerplate_mobile/modules/billing/domain/entities/subscription_plan.dart';
import 'package:boilerplate_mobile/modules/billing/domain/entities/tenant_subscription.dart';
import 'package:boilerplate_mobile/modules/billing/presentation/cubit/billing_cubit.dart';
import 'package:boilerplate_mobile/modules/billing/presentation/cubit/billing_state.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class BillingPage extends StatefulWidget {
  const BillingPage({super.key});

  @override
  State<BillingPage> createState() => _BillingPageState();
}

class _BillingPageState extends State<BillingPage> {
  @override
  void initState() {
    super.initState();
    context.read<BillingCubit>().load();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Billing')),
      body: BlocBuilder<BillingCubit, BillingState>(
        builder: (context, state) => switch (state) {
          BillingInitial() || BillingLoading() =>
            const LoadingView(message: 'Loading billing...'),
          BillingError(:final message) => ErrorView(
              message: message,
              onRetry: () => context.read<BillingCubit>().load(),
            ),
          BillingLoaded(:final plans, :final subscription) =>
            RefreshIndicator(
              onRefresh: () => context.read<BillingCubit>().load(),
              child: ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  if (subscription != null) ...[
                    _SubscriptionCard(subscription: subscription),
                    const SizedBox(height: 24),
                  ],
                  Text(
                    'Available Plans',
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                  const SizedBox(height: 12),
                  if (plans.isEmpty)
                    const EmptyState(
                      icon: Icons.payments_outlined,
                      title: 'No plans available',
                    )
                  else
                    ...plans.map(
                      (plan) => _PlanCard(
                        plan: plan,
                        isCurrentPlan: subscription?.planSlug == plan.slug,
                      ),
                    ),
                ],
              ),
            ),
        },
      ),
    );
  }
}

class _SubscriptionCard extends StatelessWidget {
  const _SubscriptionCard({required this.subscription});
  final TenantSubscription subscription;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final statusColor = switch (subscription.status) {
      SubscriptionStatus.active || SubscriptionStatus.trialing =>
        Colors.green,
      SubscriptionStatus.pastDue => Colors.orange,
      SubscriptionStatus.canceled || SubscriptionStatus.expired => Colors.red,
    };

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(Icons.credit_card, color: theme.colorScheme.primary),
                const SizedBox(width: 8),
                Text(
                  'Current Subscription',
                  style: theme.textTheme.titleMedium,
                ),
              ],
            ),
            const SizedBox(height: 12),
            _DetailRow(
              label: 'Plan',
              value: subscription.planName,
            ),
            _DetailRow(
              label: 'Status',
              valueWidget: Chip(
                label: Text(
                  subscription.status.name.toUpperCase(),
                  style: TextStyle(color: statusColor, fontSize: 12),
                ),
                backgroundColor: statusColor.withValues(alpha: 0.1),
                materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                visualDensity: VisualDensity.compact,
                side: BorderSide.none,
              ),
            ),
            _DetailRow(
              label: 'Billing',
              value: subscription.billingInterval == BillingInterval.monthly
                  ? 'Monthly'
                  : 'Annual',
            ),
            _DetailRow(
              label: 'Price',
              value:
                  '${subscription.currentPrice.toStringAsFixed(2)} ${subscription.currency}',
            ),
            _DetailRow(
              label: 'Period',
              value:
                  '${_formatDate(subscription.currentPeriodStart)} — ${_formatDate(subscription.currentPeriodEnd)}',
            ),
            _DetailRow(
              label: 'Auto-renew',
              value: subscription.autoRenew ? 'Yes' : 'No',
            ),
          ],
        ),
      ),
    );
  }

  String _formatDate(DateTime dt) =>
      '${dt.year}-${dt.month.toString().padLeft(2, '0')}-${dt.day.toString().padLeft(2, '0')}';
}

class _PlanCard extends StatelessWidget {
  const _PlanCard({required this.plan, required this.isCurrentPlan});
  final SubscriptionPlan plan;
  final bool isCurrentPlan;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      shape: isCurrentPlan
          ? RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(16),
              side: BorderSide(color: theme.colorScheme.primary, width: 2),
            )
          : null,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(plan.name, style: theme.textTheme.titleMedium),
                ),
                if (isCurrentPlan)
                  Chip(
                    label: const Text('Current'),
                    backgroundColor:
                        theme.colorScheme.primaryContainer,
                    labelStyle: TextStyle(
                      color: theme.colorScheme.onPrimaryContainer,
                      fontSize: 12,
                    ),
                    materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                    visualDensity: VisualDensity.compact,
                    side: BorderSide.none,
                  ),
                if (plan.isFree)
                  const Chip(
                    label: Text('Free'),
                    materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                    visualDensity: VisualDensity.compact,
                    side: BorderSide.none,
                  ),
              ],
            ),
            if (plan.description != null) ...[
              const SizedBox(height: 4),
              Text(
                plan.description!,
                style: theme.textTheme.bodySmall,
              ),
            ],
            const SizedBox(height: 8),
            if (!plan.isFree) ...[
              Text(
                '${plan.monthlyPrice.toStringAsFixed(2)} ${plan.currency}/mo',
                style: theme.textTheme.titleLarge?.copyWith(
                  color: theme.colorScheme.primary,
                ),
              ),
              Text(
                '${plan.annualPrice.toStringAsFixed(2)} ${plan.currency}/yr',
                style: theme.textTheme.bodySmall,
              ),
            ],
            if (plan.trialDays > 0) ...[
              const SizedBox(height: 4),
              Text(
                '${plan.trialDays}-day free trial',
                style: theme.textTheme.bodySmall?.copyWith(
                  color: theme.colorScheme.primary,
                ),
              ),
            ],
            if (plan.features.isNotEmpty) ...[
              const SizedBox(height: 8),
              const Divider(),
              ...plan.features.map(
                (f) => Padding(
                  padding: const EdgeInsets.symmetric(vertical: 2),
                  child: Row(
                    children: [
                      Icon(
                        Icons.check_circle_outline,
                        size: 16,
                        color: theme.colorScheme.primary,
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          '${f.key}: ${f.value}',
                          style: theme.textTheme.bodySmall,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _DetailRow extends StatelessWidget {
  const _DetailRow({required this.label, this.value, this.valueWidget});
  final String label;
  final String? value;
  final Widget? valueWidget;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        children: [
          SizedBox(
            width: 100,
            child: Text(
              label,
              style: theme.textTheme.bodySmall?.copyWith(
                color: theme.colorScheme.onSurfaceVariant,
              ),
            ),
          ),
          Expanded(
            child: valueWidget ??
                Text(value ?? '', style: theme.textTheme.bodyMedium),
          ),
        ],
      ),
    );
  }
}
