import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full'
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login.component').then((m) => m.LoginComponent)
  },
  {
    path: 'onboarding',
    loadComponent: () =>
      import('./features/onboarding/onboarding.component').then((m) => m.OnboardingComponent)
  },
  {
    path: 'inventory',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/inventory/product-list.component').then((m) => m.ProductListComponent)
  },
  {
    path: 'settings',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/settings/settings.component').then((m) => m.SettingsComponent)
  },
  {
    path: 'billing',
    loadComponent: () =>
      import('./features/billing/billing.component').then((m) => m.BillingComponent)
  }
];
