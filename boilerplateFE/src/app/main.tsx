import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { registerAllModules } from '@/config/modules.config';
import '@/i18n';
import '@/styles/index.css';

// Bootstrap optional modules into the slot/capability registries before React
// mounts. Core pages render <Slot id="..." /> and read whatever is registered.
registerAllModules();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>
);
