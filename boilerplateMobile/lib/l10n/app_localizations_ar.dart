// ignore: unused_import
import 'package:intl/intl.dart' as intl;
import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for Arabic (`ar`).
class AppLocalizationsAr extends AppLocalizations {
  AppLocalizationsAr([String locale = 'ar']) : super(locale);

  @override
  String get appName => 'ستارتر';

  @override
  String get commonLoading => 'جاري التحميل...';

  @override
  String get commonRetry => 'إعادة المحاولة';

  @override
  String get commonSave => 'حفظ';

  @override
  String get commonCancel => 'إلغاء';

  @override
  String get commonDelete => 'حذف';

  @override
  String get commonEdit => 'تعديل';

  @override
  String get commonRequired => 'مطلوب';

  @override
  String get commonError => 'حدث خطأ';

  @override
  String get commonNoData => 'لا توجد بيانات';

  @override
  String get commonSuccess => 'تم بنجاح';

  @override
  String get loginTitle => 'تسجيل الدخول إلى حسابك';

  @override
  String get loginEmail => 'البريد الإلكتروني';

  @override
  String get loginPassword => 'كلمة المرور';

  @override
  String get loginSignIn => 'تسجيل الدخول';

  @override
  String get loginEmailRequired => 'البريد الإلكتروني مطلوب';

  @override
  String get loginEmailInvalid => 'أدخل بريد إلكتروني صالح';

  @override
  String get loginPasswordRequired => 'كلمة المرور مطلوبة';

  @override
  String get twoFactorTitle => 'المصادقة الثنائية';

  @override
  String get twoFactorSubtitle =>
      'افتح تطبيق المصادقة وأدخل الرمز المكون من 6 أرقام.';

  @override
  String get twoFactorCode => 'الرمز';

  @override
  String get twoFactorVerify => 'تحقق';

  @override
  String get profileTitle => 'الملف الشخصي';

  @override
  String get profileEditTitle => 'تعديل الملف الشخصي';

  @override
  String get profileFirstName => 'الاسم الأول';

  @override
  String get profileLastName => 'اسم العائلة';

  @override
  String get profileEmail => 'البريد الإلكتروني';

  @override
  String get profilePhone => 'الهاتف (اختياري)';

  @override
  String get profileName => 'الاسم';

  @override
  String get profileTwoFactor => 'المصادقة الثنائية';

  @override
  String get profileTwoFactorEnabled => 'مفعّل';

  @override
  String get profileTwoFactorDisabled => 'معطّل';

  @override
  String get profileTenant => 'المستأجر';

  @override
  String get profileEditButton => 'تعديل الملف الشخصي';

  @override
  String get profileChangePasswordButton => 'تغيير كلمة المرور';

  @override
  String get profileUpdated => 'تم تحديث الملف الشخصي';

  @override
  String get changePasswordTitle => 'تغيير كلمة المرور';

  @override
  String get changePasswordCurrent => 'كلمة المرور الحالية';

  @override
  String get changePasswordNew => 'كلمة المرور الجديدة';

  @override
  String get changePasswordConfirm => 'تأكيد كلمة المرور الجديدة';

  @override
  String get changePasswordMinLength => '6 أحرف على الأقل';

  @override
  String get changePasswordMismatch => 'كلمات المرور غير متطابقة';

  @override
  String get changePasswordSuccess => 'تم تغيير كلمة المرور بنجاح';

  @override
  String get changePasswordButton => 'تغيير كلمة المرور';

  @override
  String get notificationsTitle => 'الإشعارات';

  @override
  String get notificationsMarkAllRead => 'تحديد الكل كمقروء';

  @override
  String get notificationsEmpty => 'لا توجد إشعارات';

  @override
  String get notificationsEmptySubtitle => 'أنت على اطلاع بكل شيء!';

  @override
  String get notificationsLoading => 'جاري تحميل الإشعارات...';

  @override
  String get notificationTimeNow => 'الآن';

  @override
  String notificationTimeMinutes(int count) {
    return '$countد';
  }

  @override
  String notificationTimeHours(int count) {
    return '$countس';
  }

  @override
  String notificationTimeDays(int count) {
    return '$countي';
  }

  @override
  String notificationTimeWeeks(int count) {
    return '$countأ';
  }

  @override
  String get billingTitle => 'الفوترة';

  @override
  String get billingCurrentSubscription => 'الاشتراك الحالي';

  @override
  String get billingAvailablePlans => 'الخطط المتاحة';

  @override
  String get billingNoPlans => 'لا توجد خطط متاحة';

  @override
  String get billingPlan => 'الخطة';

  @override
  String get billingStatus => 'الحالة';

  @override
  String get billingInterval => 'الفوترة';

  @override
  String get billingIntervalMonthly => 'شهري';

  @override
  String get billingIntervalAnnual => 'سنوي';

  @override
  String get billingPrice => 'السعر';

  @override
  String get billingPeriod => 'الفترة';

  @override
  String get billingAutoRenew => 'تجديد تلقائي';

  @override
  String get billingYes => 'نعم';

  @override
  String get billingNo => 'لا';

  @override
  String get billingCurrentPlan => 'الحالية';

  @override
  String get billingFree => 'مجاني';

  @override
  String billingPerMonth(String price, String currency) {
    return '$price $currency/شهر';
  }

  @override
  String billingPerYear(String price, String currency) {
    return '$price $currency/سنة';
  }

  @override
  String billingTrialDays(int count) {
    return 'تجربة مجانية لمدة $count يوم';
  }

  @override
  String get billingLoading => 'جاري تحميل الفوترة...';

  @override
  String get billingSubscriptionCard => 'الفوترة والاشتراك';

  @override
  String get billingSubscriptionCardSubtitle => 'عرض خطتك وتفاصيل الفوترة';

  @override
  String get navHome => 'الرئيسية';

  @override
  String get navNotifications => 'الإشعارات';

  @override
  String get navProfile => 'الملف الشخصي';

  @override
  String get logout => 'تسجيل الخروج';

  @override
  String get errorNetwork => 'لا يوجد اتصال بالإنترنت';

  @override
  String get errorTimeout => 'انتهت مهلة الاتصال';

  @override
  String get errorServer => 'خطأ في الخادم. يرجى المحاولة لاحقاً.';

  @override
  String get errorUnknown => 'حدث خطأ ما';

  @override
  String stagingBanner(String url) {
    return 'تجريبي — $url';
  }
}
