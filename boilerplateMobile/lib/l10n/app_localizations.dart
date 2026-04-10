import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:intl/intl.dart' as intl;

import 'app_localizations_ar.dart';
import 'app_localizations_en.dart';

// ignore_for_file: type=lint

/// Callers can lookup localized strings with an instance of AppLocalizations
/// returned by `AppLocalizations.of(context)`.
///
/// Applications need to include `AppLocalizations.delegate()` in their app's
/// `localizationDelegates` list, and the locales they support in the app's
/// `supportedLocales` list. For example:
///
/// ```dart
/// import 'l10n/app_localizations.dart';
///
/// return MaterialApp(
///   localizationsDelegates: AppLocalizations.localizationsDelegates,
///   supportedLocales: AppLocalizations.supportedLocales,
///   home: MyApplicationHome(),
/// );
/// ```
///
/// ## Update pubspec.yaml
///
/// Please make sure to update your pubspec.yaml to include the following
/// packages:
///
/// ```yaml
/// dependencies:
///   # Internationalization support.
///   flutter_localizations:
///     sdk: flutter
///   intl: any # Use the pinned version from flutter_localizations
///
///   # Rest of dependencies
/// ```
///
/// ## iOS Applications
///
/// iOS applications define key application metadata, including supported
/// locales, in an Info.plist file that is built into the application bundle.
/// To configure the locales supported by your app, you’ll need to edit this
/// file.
///
/// First, open your project’s ios/Runner.xcworkspace Xcode workspace file.
/// Then, in the Project Navigator, open the Info.plist file under the Runner
/// project’s Runner folder.
///
/// Next, select the Information Property List item, select Add Item from the
/// Editor menu, then select Localizations from the pop-up menu.
///
/// Select and expand the newly-created Localizations item then, for each
/// locale your application supports, add a new item and select the locale
/// you wish to add from the pop-up menu in the Value field. This list should
/// be consistent with the languages listed in the AppLocalizations.supportedLocales
/// property.
abstract class AppLocalizations {
  AppLocalizations(String locale)
      : localeName = intl.Intl.canonicalizedLocale(locale.toString());

  final String localeName;

  static AppLocalizations of(BuildContext context) {
    return Localizations.of<AppLocalizations>(context, AppLocalizations)!;
  }

  static const LocalizationsDelegate<AppLocalizations> delegate =
      _AppLocalizationsDelegate();

  /// A list of this localizations delegate along with the default localizations
  /// delegates.
  ///
  /// Returns a list of localizations delegates containing this delegate along with
  /// GlobalMaterialLocalizations.delegate, GlobalCupertinoLocalizations.delegate,
  /// and GlobalWidgetsLocalizations.delegate.
  ///
  /// Additional delegates can be added by appending to this list in
  /// MaterialApp. This list does not have to be used at all if a custom list
  /// of delegates is preferred or required.
  static const List<LocalizationsDelegate<dynamic>> localizationsDelegates =
      <LocalizationsDelegate<dynamic>>[
    delegate,
    GlobalMaterialLocalizations.delegate,
    GlobalCupertinoLocalizations.delegate,
    GlobalWidgetsLocalizations.delegate,
  ];

  /// A list of this localizations delegate's supported locales.
  static const List<Locale> supportedLocales = <Locale>[
    Locale('ar'),
    Locale('en')
  ];

  /// No description provided for @appName.
  ///
  /// In en, this message translates to:
  /// **'Starter'**
  String get appName;

  /// No description provided for @commonLoading.
  ///
  /// In en, this message translates to:
  /// **'Loading...'**
  String get commonLoading;

  /// No description provided for @commonRetry.
  ///
  /// In en, this message translates to:
  /// **'Retry'**
  String get commonRetry;

  /// No description provided for @commonSave.
  ///
  /// In en, this message translates to:
  /// **'Save'**
  String get commonSave;

  /// No description provided for @commonCancel.
  ///
  /// In en, this message translates to:
  /// **'Cancel'**
  String get commonCancel;

  /// No description provided for @commonDelete.
  ///
  /// In en, this message translates to:
  /// **'Delete'**
  String get commonDelete;

  /// No description provided for @commonEdit.
  ///
  /// In en, this message translates to:
  /// **'Edit'**
  String get commonEdit;

  /// No description provided for @commonRequired.
  ///
  /// In en, this message translates to:
  /// **'Required'**
  String get commonRequired;

  /// No description provided for @commonError.
  ///
  /// In en, this message translates to:
  /// **'An error occurred'**
  String get commonError;

  /// No description provided for @commonNoData.
  ///
  /// In en, this message translates to:
  /// **'No data available'**
  String get commonNoData;

  /// No description provided for @commonSuccess.
  ///
  /// In en, this message translates to:
  /// **'Success'**
  String get commonSuccess;

  /// No description provided for @loginTitle.
  ///
  /// In en, this message translates to:
  /// **'Sign in to your account'**
  String get loginTitle;

  /// No description provided for @loginEmail.
  ///
  /// In en, this message translates to:
  /// **'Email'**
  String get loginEmail;

  /// No description provided for @loginPassword.
  ///
  /// In en, this message translates to:
  /// **'Password'**
  String get loginPassword;

  /// No description provided for @loginSignIn.
  ///
  /// In en, this message translates to:
  /// **'Sign In'**
  String get loginSignIn;

  /// No description provided for @loginEmailRequired.
  ///
  /// In en, this message translates to:
  /// **'Email is required'**
  String get loginEmailRequired;

  /// No description provided for @loginEmailInvalid.
  ///
  /// In en, this message translates to:
  /// **'Enter a valid email'**
  String get loginEmailInvalid;

  /// No description provided for @loginPasswordRequired.
  ///
  /// In en, this message translates to:
  /// **'Password is required'**
  String get loginPasswordRequired;

  /// No description provided for @twoFactorTitle.
  ///
  /// In en, this message translates to:
  /// **'Two-Factor Authentication'**
  String get twoFactorTitle;

  /// No description provided for @twoFactorSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Open your authenticator app and enter the 6-digit code.'**
  String get twoFactorSubtitle;

  /// No description provided for @twoFactorCode.
  ///
  /// In en, this message translates to:
  /// **'Code'**
  String get twoFactorCode;

  /// No description provided for @twoFactorVerify.
  ///
  /// In en, this message translates to:
  /// **'Verify'**
  String get twoFactorVerify;

  /// No description provided for @profileTitle.
  ///
  /// In en, this message translates to:
  /// **'Profile'**
  String get profileTitle;

  /// No description provided for @profileEditTitle.
  ///
  /// In en, this message translates to:
  /// **'Edit Profile'**
  String get profileEditTitle;

  /// No description provided for @profileFirstName.
  ///
  /// In en, this message translates to:
  /// **'First Name'**
  String get profileFirstName;

  /// No description provided for @profileLastName.
  ///
  /// In en, this message translates to:
  /// **'Last Name'**
  String get profileLastName;

  /// No description provided for @profileEmail.
  ///
  /// In en, this message translates to:
  /// **'Email'**
  String get profileEmail;

  /// No description provided for @profilePhone.
  ///
  /// In en, this message translates to:
  /// **'Phone (optional)'**
  String get profilePhone;

  /// No description provided for @profileName.
  ///
  /// In en, this message translates to:
  /// **'Name'**
  String get profileName;

  /// No description provided for @profileTwoFactor.
  ///
  /// In en, this message translates to:
  /// **'Two-Factor Auth'**
  String get profileTwoFactor;

  /// No description provided for @profileTwoFactorEnabled.
  ///
  /// In en, this message translates to:
  /// **'Enabled'**
  String get profileTwoFactorEnabled;

  /// No description provided for @profileTwoFactorDisabled.
  ///
  /// In en, this message translates to:
  /// **'Disabled'**
  String get profileTwoFactorDisabled;

  /// No description provided for @profileTenant.
  ///
  /// In en, this message translates to:
  /// **'Tenant'**
  String get profileTenant;

  /// No description provided for @profileEditButton.
  ///
  /// In en, this message translates to:
  /// **'Edit Profile'**
  String get profileEditButton;

  /// No description provided for @profileChangePasswordButton.
  ///
  /// In en, this message translates to:
  /// **'Change Password'**
  String get profileChangePasswordButton;

  /// No description provided for @profileUpdated.
  ///
  /// In en, this message translates to:
  /// **'Profile updated'**
  String get profileUpdated;

  /// No description provided for @changePasswordTitle.
  ///
  /// In en, this message translates to:
  /// **'Change Password'**
  String get changePasswordTitle;

  /// No description provided for @changePasswordCurrent.
  ///
  /// In en, this message translates to:
  /// **'Current Password'**
  String get changePasswordCurrent;

  /// No description provided for @changePasswordNew.
  ///
  /// In en, this message translates to:
  /// **'New Password'**
  String get changePasswordNew;

  /// No description provided for @changePasswordConfirm.
  ///
  /// In en, this message translates to:
  /// **'Confirm New Password'**
  String get changePasswordConfirm;

  /// No description provided for @changePasswordMinLength.
  ///
  /// In en, this message translates to:
  /// **'At least 6 characters'**
  String get changePasswordMinLength;

  /// No description provided for @changePasswordMismatch.
  ///
  /// In en, this message translates to:
  /// **'Passwords do not match'**
  String get changePasswordMismatch;

  /// No description provided for @changePasswordSuccess.
  ///
  /// In en, this message translates to:
  /// **'Password changed successfully'**
  String get changePasswordSuccess;

  /// No description provided for @changePasswordButton.
  ///
  /// In en, this message translates to:
  /// **'Change Password'**
  String get changePasswordButton;

  /// No description provided for @notificationsTitle.
  ///
  /// In en, this message translates to:
  /// **'Notifications'**
  String get notificationsTitle;

  /// No description provided for @notificationsMarkAllRead.
  ///
  /// In en, this message translates to:
  /// **'Mark all read'**
  String get notificationsMarkAllRead;

  /// No description provided for @notificationsEmpty.
  ///
  /// In en, this message translates to:
  /// **'No notifications'**
  String get notificationsEmpty;

  /// No description provided for @notificationsEmptySubtitle.
  ///
  /// In en, this message translates to:
  /// **'You\'\'re all caught up!'**
  String get notificationsEmptySubtitle;

  /// No description provided for @notificationsLoading.
  ///
  /// In en, this message translates to:
  /// **'Loading notifications...'**
  String get notificationsLoading;

  /// No description provided for @notificationTimeNow.
  ///
  /// In en, this message translates to:
  /// **'now'**
  String get notificationTimeNow;

  /// No description provided for @notificationTimeMinutes.
  ///
  /// In en, this message translates to:
  /// **'{count}m'**
  String notificationTimeMinutes(int count);

  /// No description provided for @notificationTimeHours.
  ///
  /// In en, this message translates to:
  /// **'{count}h'**
  String notificationTimeHours(int count);

  /// No description provided for @notificationTimeDays.
  ///
  /// In en, this message translates to:
  /// **'{count}d'**
  String notificationTimeDays(int count);

  /// No description provided for @notificationTimeWeeks.
  ///
  /// In en, this message translates to:
  /// **'{count}w'**
  String notificationTimeWeeks(int count);

  /// No description provided for @billingTitle.
  ///
  /// In en, this message translates to:
  /// **'Billing'**
  String get billingTitle;

  /// No description provided for @billingCurrentSubscription.
  ///
  /// In en, this message translates to:
  /// **'Current Subscription'**
  String get billingCurrentSubscription;

  /// No description provided for @billingAvailablePlans.
  ///
  /// In en, this message translates to:
  /// **'Available Plans'**
  String get billingAvailablePlans;

  /// No description provided for @billingNoPlans.
  ///
  /// In en, this message translates to:
  /// **'No plans available'**
  String get billingNoPlans;

  /// No description provided for @billingPlan.
  ///
  /// In en, this message translates to:
  /// **'Plan'**
  String get billingPlan;

  /// No description provided for @billingStatus.
  ///
  /// In en, this message translates to:
  /// **'Status'**
  String get billingStatus;

  /// No description provided for @billingInterval.
  ///
  /// In en, this message translates to:
  /// **'Billing'**
  String get billingInterval;

  /// No description provided for @billingIntervalMonthly.
  ///
  /// In en, this message translates to:
  /// **'Monthly'**
  String get billingIntervalMonthly;

  /// No description provided for @billingIntervalAnnual.
  ///
  /// In en, this message translates to:
  /// **'Annual'**
  String get billingIntervalAnnual;

  /// No description provided for @billingPrice.
  ///
  /// In en, this message translates to:
  /// **'Price'**
  String get billingPrice;

  /// No description provided for @billingPeriod.
  ///
  /// In en, this message translates to:
  /// **'Period'**
  String get billingPeriod;

  /// No description provided for @billingAutoRenew.
  ///
  /// In en, this message translates to:
  /// **'Auto-renew'**
  String get billingAutoRenew;

  /// No description provided for @billingYes.
  ///
  /// In en, this message translates to:
  /// **'Yes'**
  String get billingYes;

  /// No description provided for @billingNo.
  ///
  /// In en, this message translates to:
  /// **'No'**
  String get billingNo;

  /// No description provided for @billingCurrentPlan.
  ///
  /// In en, this message translates to:
  /// **'Current'**
  String get billingCurrentPlan;

  /// No description provided for @billingFree.
  ///
  /// In en, this message translates to:
  /// **'Free'**
  String get billingFree;

  /// No description provided for @billingPerMonth.
  ///
  /// In en, this message translates to:
  /// **'{price} {currency}/mo'**
  String billingPerMonth(String price, String currency);

  /// No description provided for @billingPerYear.
  ///
  /// In en, this message translates to:
  /// **'{price} {currency}/yr'**
  String billingPerYear(String price, String currency);

  /// No description provided for @billingTrialDays.
  ///
  /// In en, this message translates to:
  /// **'{count}-day free trial'**
  String billingTrialDays(int count);

  /// No description provided for @billingLoading.
  ///
  /// In en, this message translates to:
  /// **'Loading billing...'**
  String get billingLoading;

  /// No description provided for @billingSubscriptionCard.
  ///
  /// In en, this message translates to:
  /// **'Billing & Subscription'**
  String get billingSubscriptionCard;

  /// No description provided for @billingSubscriptionCardSubtitle.
  ///
  /// In en, this message translates to:
  /// **'View your plan and billing details'**
  String get billingSubscriptionCardSubtitle;

  /// No description provided for @navHome.
  ///
  /// In en, this message translates to:
  /// **'Home'**
  String get navHome;

  /// No description provided for @navNotifications.
  ///
  /// In en, this message translates to:
  /// **'Notifications'**
  String get navNotifications;

  /// No description provided for @navProfile.
  ///
  /// In en, this message translates to:
  /// **'Profile'**
  String get navProfile;

  /// No description provided for @logout.
  ///
  /// In en, this message translates to:
  /// **'Logout'**
  String get logout;

  /// No description provided for @errorNetwork.
  ///
  /// In en, this message translates to:
  /// **'No internet connection'**
  String get errorNetwork;

  /// No description provided for @errorTimeout.
  ///
  /// In en, this message translates to:
  /// **'Connection timed out'**
  String get errorTimeout;

  /// No description provided for @errorServer.
  ///
  /// In en, this message translates to:
  /// **'Server error. Please try again later.'**
  String get errorServer;

  /// No description provided for @errorUnknown.
  ///
  /// In en, this message translates to:
  /// **'Something went wrong'**
  String get errorUnknown;

  /// No description provided for @stagingBanner.
  ///
  /// In en, this message translates to:
  /// **'Staging — {url}'**
  String stagingBanner(String url);
}

class _AppLocalizationsDelegate
    extends LocalizationsDelegate<AppLocalizations> {
  const _AppLocalizationsDelegate();

  @override
  Future<AppLocalizations> load(Locale locale) {
    return SynchronousFuture<AppLocalizations>(lookupAppLocalizations(locale));
  }

  @override
  bool isSupported(Locale locale) =>
      <String>['ar', 'en'].contains(locale.languageCode);

  @override
  bool shouldReload(_AppLocalizationsDelegate old) => false;
}

AppLocalizations lookupAppLocalizations(Locale locale) {
  // Lookup logic when only language code is specified.
  switch (locale.languageCode) {
    case 'ar':
      return AppLocalizationsAr();
    case 'en':
      return AppLocalizationsEn();
  }

  throw FlutterError(
      'AppLocalizations.delegate failed to load unsupported locale "$locale". This is likely '
      'an issue with the localizations generation tool. Please file an issue '
      'on GitHub with a reproducible sample app and the gen-l10n configuration '
      'that was used.');
}
