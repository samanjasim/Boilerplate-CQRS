import {
  AiSection,
  ArchitectureSection,
  CodeSection,
  FeatureGrid,
  FooterCta,
  HeroSection,
  LandingNav,
  StatsStrip,
  TechStrip,
} from '@/features/landing/components';

export default function LandingPage() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      <LandingNav />
      <HeroSection />
      <TechStrip />
      <FeatureGrid />
      <AiSection />
      <CodeSection />
      <ArchitectureSection />
      <StatsStrip />
      <FooterCta />
    </div>
  );
}
