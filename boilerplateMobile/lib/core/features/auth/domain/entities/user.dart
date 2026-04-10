/// Domain entity representing the authenticated user.
///
/// Pure Dart — no Flutter, no freezed, no JSON annotations.
/// The data layer maps DTOs to/from this entity.
class User {
  const User({
    required this.id,
    required this.email,
    required this.firstName,
    required this.lastName,
    required this.roles,
    required this.permissions,
    required this.createdAt,
    this.username,
    this.phoneNumber,
    this.status,
    this.emailConfirmed = false,
    this.twoFactorEnabled = false,
    this.lastLoginAt,
    this.tenantId,
    this.tenantName,
    this.tenantSlug,
  });

  final String id;
  final String email;
  final String firstName;
  final String lastName;
  final String? username;
  final String? phoneNumber;
  final String? status;
  final bool emailConfirmed;
  final bool twoFactorEnabled;
  final DateTime? lastLoginAt;
  final DateTime createdAt;
  final List<String> roles;
  final Set<String> permissions;
  final String? tenantId;
  final String? tenantName;
  final String? tenantSlug;

  String get fullName => '$firstName $lastName'.trim();
}
