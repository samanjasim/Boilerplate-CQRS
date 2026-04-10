// ignore: unused_import
import 'package:intl/intl.dart' as intl;
import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for English (`en`).
class AppLocalizationsEn extends AppLocalizations {
  AppLocalizationsEn([String locale = 'en']) : super(locale);

  @override
  String get appName => 'Starter';

  @override
  String get commonLoading => 'Loading...';

  @override
  String get commonRetry => 'Retry';

  @override
  String get commonSave => 'Save';

  @override
  String get commonCancel => 'Cancel';

  @override
  String get commonDelete => 'Delete';

  @override
  String get commonEdit => 'Edit';

  @override
  String get commonRequired => 'Required';

  @override
  String get commonError => 'An error occurred';

  @override
  String get commonNoData => 'No data available';

  @override
  String get commonSuccess => 'Success';

  @override
  String get loginTitle => 'Sign in to your account';

  @override
  String get loginEmail => 'Email';

  @override
  String get loginPassword => 'Password';

  @override
  String get loginSignIn => 'Sign In';

  @override
  String get loginEmailRequired => 'Email is required';

  @override
  String get loginEmailInvalid => 'Enter a valid email';

  @override
  String get loginPasswordRequired => 'Password is required';

  @override
  String get twoFactorTitle => 'Two-Factor Authentication';

  @override
  String get twoFactorSubtitle =>
      'Open your authenticator app and enter the 6-digit code.';

  @override
  String get twoFactorCode => 'Code';

  @override
  String get twoFactorVerify => 'Verify';

  @override
  String get profileTitle => 'Profile';

  @override
  String get profileEditTitle => 'Edit Profile';

  @override
  String get profileFirstName => 'First Name';

  @override
  String get profileLastName => 'Last Name';

  @override
  String get profileEmail => 'Email';

  @override
  String get profilePhone => 'Phone (optional)';

  @override
  String get profileName => 'Name';

  @override
  String get profileTwoFactor => 'Two-Factor Auth';

  @override
  String get profileTwoFactorEnabled => 'Enabled';

  @override
  String get profileTwoFactorDisabled => 'Disabled';

  @override
  String get profileTenant => 'Tenant';

  @override
  String get profileEditButton => 'Edit Profile';

  @override
  String get profileChangePasswordButton => 'Change Password';

  @override
  String get profileUpdated => 'Profile updated';

  @override
  String get changePasswordTitle => 'Change Password';

  @override
  String get changePasswordCurrent => 'Current Password';

  @override
  String get changePasswordNew => 'New Password';

  @override
  String get changePasswordConfirm => 'Confirm New Password';

  @override
  String get changePasswordMinLength => 'At least 6 characters';

  @override
  String get changePasswordMismatch => 'Passwords do not match';

  @override
  String get changePasswordSuccess => 'Password changed successfully';

  @override
  String get changePasswordButton => 'Change Password';

  @override
  String get notificationsTitle => 'Notifications';

  @override
  String get notificationsMarkAllRead => 'Mark all read';

  @override
  String get notificationsEmpty => 'No notifications';

  @override
  String get notificationsEmptySubtitle => 'You\'re all caught up!';

  @override
  String get notificationsLoading => 'Loading notifications...';

  @override
  String get notificationTimeNow => 'now';

  @override
  String notificationTimeMinutes(int count) {
    return '${count}m';
  }

  @override
  String notificationTimeHours(int count) {
    return '${count}h';
  }

  @override
  String notificationTimeDays(int count) {
    return '${count}d';
  }

  @override
  String notificationTimeWeeks(int count) {
    return '${count}w';
  }

  @override
  String get billingTitle => 'Billing';

  @override
  String get billingCurrentSubscription => 'Current Subscription';

  @override
  String get billingAvailablePlans => 'Available Plans';

  @override
  String get billingNoPlans => 'No plans available';

  @override
  String get billingPlan => 'Plan';

  @override
  String get billingStatus => 'Status';

  @override
  String get billingInterval => 'Billing';

  @override
  String get billingIntervalMonthly => 'Monthly';

  @override
  String get billingIntervalAnnual => 'Annual';

  @override
  String get billingPrice => 'Price';

  @override
  String get billingPeriod => 'Period';

  @override
  String get billingAutoRenew => 'Auto-renew';

  @override
  String get billingYes => 'Yes';

  @override
  String get billingNo => 'No';

  @override
  String get billingCurrentPlan => 'Current';

  @override
  String get billingFree => 'Free';

  @override
  String billingPerMonth(String price, String currency) {
    return '$price $currency/mo';
  }

  @override
  String billingPerYear(String price, String currency) {
    return '$price $currency/yr';
  }

  @override
  String billingTrialDays(int count) {
    return '$count-day free trial';
  }

  @override
  String get billingLoading => 'Loading billing...';

  @override
  String get billingSubscriptionCard => 'Billing & Subscription';

  @override
  String get billingSubscriptionCardSubtitle =>
      'View your plan and billing details';

  @override
  String get navHome => 'Home';

  @override
  String get navNotifications => 'Notifications';

  @override
  String get navProfile => 'Profile';

  @override
  String get logout => 'Logout';

  @override
  String get errorNetwork => 'No internet connection';

  @override
  String get errorTimeout => 'Connection timed out';

  @override
  String get errorServer => 'Server error. Please try again later.';

  @override
  String get errorUnknown => 'Something went wrong';

  @override
  String stagingBanner(String url) {
    return 'Staging — $url';
  }
}
