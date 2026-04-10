/// Build-time flavor identifier. Selected by the entry point
/// (`main_staging.dart` / `main_prod.dart`) and embedded in `AppConfig`.
enum Flavor {
  staging,
  prod;

  bool get isStaging => this == Flavor.staging;
  bool get isProd => this == Flavor.prod;
}
