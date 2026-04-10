import 'package:boilerplate_mobile/core/extensions/context_extensions.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/cubit/login_cubit.dart';
import 'package:boilerplate_mobile/core/features/auth/presentation/cubit/login_state.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// Page shown when login returns `LoginRequires2FA`.
///
/// The user enters their TOTP code and the cubit re-sends the login
/// request with the code attached.
class TwoFactorPage extends StatefulWidget {
  const TwoFactorPage({
    required this.email,
    required this.password,
    super.key,
  });

  final String email;
  final String password;

  @override
  State<TwoFactorPage> createState() => _TwoFactorPageState();
}

class _TwoFactorPageState extends State<TwoFactorPage> {
  final _codeController = TextEditingController();

  @override
  void dispose() {
    _codeController.dispose();
    super.dispose();
  }

  void _onSubmit() {
    final code = _codeController.text.trim();
    if (code.isEmpty) return;

    context.read<LoginCubit>().verify2FA(
          email: widget.email,
          password: widget.password,
          code: code,
        );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l = context.l10n;

    return Scaffold(
      appBar: AppBar(title: Text(l.twoFactorTitle)),
      body: BlocListener<LoginCubit, LoginState>(
        listener: (context, state) {
          if (state is LoginError) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text(state.message),
                backgroundColor: theme.colorScheme.error,
              ),
            );
          }
          // On success, AuthCubit handles navigation.
        },
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 24),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Icon(
                Icons.security,
                size: 64,
                color: theme.colorScheme.primary,
              ),
              const SizedBox(height: 24),
              Text(
                l.twoFactorTitle,
                textAlign: TextAlign.center,
                style: theme.textTheme.headlineMedium,
              ),
              const SizedBox(height: 8),
              Text(
                l.twoFactorSubtitle,
                textAlign: TextAlign.center,
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: theme.colorScheme.onSurfaceVariant,
                ),
              ),
              const SizedBox(height: 32),
              TextFormField(
                controller: _codeController,
                keyboardType: TextInputType.number,
                textAlign: TextAlign.center,
                maxLength: 6,
                autofocus: true,
                style: theme.textTheme.headlineMedium?.copyWith(
                  letterSpacing: 8,
                ),
                decoration: InputDecoration(
                  labelText: l.twoFactorCode,
                  counterText: '',
                ),
                onFieldSubmitted: (_) => _onSubmit(),
              ),
              const SizedBox(height: 24),
              BlocBuilder<LoginCubit, LoginState>(
                builder: (context, state) {
                  final isLoading = state is LoginLoading;
                  return FilledButton(
                    onPressed: isLoading ? null : _onSubmit,
                    child: isLoading
                        ? const SizedBox(
                            height: 20,
                            width: 20,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : Text(l.twoFactorVerify),
                  );
                },
              ),
            ],
          ),
        ),
      ),
    );
  }
}
