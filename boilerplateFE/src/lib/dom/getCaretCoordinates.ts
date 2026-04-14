// Mirror-div technique to measure caret position inside a textarea.
// Adapted from textarea-caret-position by Jonathan Ong (MIT).
// Used to anchor mention popups, emoji pickers, etc. directly at the caret —
// the same approach Slack/Discord/Teams use. Works in LTR and RTL because
// `direction` is among the mirrored properties.

const properties = [
  'direction',
  'boxSizing',
  'width',
  'height',
  'overflowX',
  'overflowY',
  'borderTopWidth',
  'borderRightWidth',
  'borderBottomWidth',
  'borderLeftWidth',
  'borderStyle',
  'paddingTop',
  'paddingRight',
  'paddingBottom',
  'paddingLeft',
  'fontStyle',
  'fontVariant',
  'fontWeight',
  'fontStretch',
  'fontSize',
  'fontSizeAdjust',
  'lineHeight',
  'fontFamily',
  'textAlign',
  'textTransform',
  'textIndent',
  'textDecoration',
  'letterSpacing',
  'wordSpacing',
  'tabSize',
  'MozTabSize',
] as const;

const isFirefox =
  typeof window !== 'undefined' && (window as unknown as { mozInnerScreenX?: number }).mozInnerScreenX != null;

export interface CaretCoordinates {
  top: number;
  left: number;
  height: number;
}

export function getCaretCoordinates(
  element: HTMLTextAreaElement | HTMLInputElement,
  position: number,
): CaretCoordinates {
  const div = document.createElement('div');
  document.body.appendChild(div);

  const style = div.style;
  const computed = getComputedStyle(element);
  const isInput = element.nodeName === 'INPUT';

  style.whiteSpace = 'pre-wrap';
  if (!isInput) style.wordWrap = 'break-word';
  style.position = 'absolute';
  style.visibility = 'hidden';

  for (const prop of properties) {
    // Reflect.set avoids the readonly-prop noise from keyof CSSStyleDeclaration
    // (length, parentRule); every item in `properties` is a writable CSS prop.
    Reflect.set(style, prop, computed[prop as keyof CSSStyleDeclaration] ?? '');
  }

  if (isFirefox) {
    if (element.scrollHeight > parseInt(computed.height, 10)) style.overflowY = 'scroll';
  } else {
    style.overflow = 'hidden';
  }

  div.textContent = element.value.substring(0, position);
  if (isInput) div.textContent = (div.textContent ?? '').replace(/\s/g, '\u00a0');

  const span = document.createElement('span');
  span.textContent = element.value.substring(position) || '.';
  div.appendChild(span);

  const coordinates: CaretCoordinates = {
    top: span.offsetTop + parseInt(computed.borderTopWidth, 10),
    left: span.offsetLeft + parseInt(computed.borderLeftWidth, 10),
    height: parseInt(computed.lineHeight, 10) || parseInt(computed.fontSize, 10) * 1.2,
  };

  document.body.removeChild(div);
  return coordinates;
}
