import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

void main() {
  test('core mobile code does not import optional module folders directly', () {
    final libDir = Directory('lib');
    final importDirective = RegExp(
      r'''^\s*import\s+['"]([^'"]+)['"]''',
      multiLine: true,
    );
    final offenders = <String>[];

    for (final entity in libDir.listSync(recursive: true)) {
      if (entity is! File || !entity.path.endsWith('.dart')) continue;

      final normalized = entity.path.replaceAll('\\', '/');
      if (normalized == 'lib/app/modules.config.dart') continue;
      if (normalized.startsWith('lib/modules/')) continue;

      final content = entity.readAsStringSync();
      final importsModule = importDirective.allMatches(content).any((match) {
        final target = match.group(1)!.replaceAll('\\', '/');
        if (target.startsWith('package:')) {
          return RegExp(r'^package:[^/]+/modules/').hasMatch(target);
        }

        return target.split('/').contains('modules');
      });

      if (importsModule) {
        offenders.add(normalized);
      }
    }

    expect(
      offenders,
      isEmpty,
      reason: 'Optional mobile modules must be imported only from '
          'lib/app/modules.config.dart or from inside lib/modules/**.',
    );
  });
}
