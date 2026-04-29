import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

void main() {
  test('core mobile code does not import optional module folders directly', () {
    final libDir = Directory('lib');
    final offenders = <String>[];

    for (final entity in libDir.listSync(recursive: true)) {
      if (entity is! File || !entity.path.endsWith('.dart')) continue;

      final normalized = entity.path.replaceAll('\\', '/');
      if (normalized == 'lib/app/modules.config.dart') continue;
      if (normalized.startsWith('lib/modules/')) continue;

      final content = entity.readAsStringSync();
      final importsModule = RegExp(
        r"import\s+'package:[^']+/modules/",
      ).hasMatch(content);

      if (importsModule) {
        offenders.add(normalized);
      }
    }

    expect(
      offenders,
      isEmpty,
      reason:
          'Optional mobile modules must be imported only from '
          'lib/app/modules.config.dart or from inside lib/modules/**.',
    );
  });
}
