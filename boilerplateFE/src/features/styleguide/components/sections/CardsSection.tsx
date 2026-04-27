import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Section } from '../Section';

export function CardsSection() {
  return (
    <Section
      id="cards"
      eyebrow="Cards"
      title="Solid · Glass · Elevated"
      deck="solid is the default (backwards-compatible). glass uses .surface-glass — translucent over aurora. elevated lifts on hover for clickable list cards."
    >
      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle>solid</CardTitle>
            <CardDescription>Default — opaque card surface with shadow.</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              Use for the bulk of in-app content where cards sit on flat backgrounds.
            </p>
          </CardContent>
        </Card>
        <Card variant="glass">
          <CardHeader>
            <CardTitle>glass</CardTitle>
            <CardDescription>Translucent — picks up aurora behind it.</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              Use on landing/marketing surfaces and any context where a card sits on the aurora canvas.
            </p>
          </CardContent>
        </Card>
        <Card variant="elevated">
          <CardHeader>
            <CardTitle>elevated</CardTitle>
            <CardDescription>Lifts and gains shadow on hover.</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              Hover me. Use for clickable list cards or any card with affordance.
            </p>
          </CardContent>
        </Card>
      </div>
    </Section>
  );
}
