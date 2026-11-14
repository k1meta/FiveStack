import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

const authPage = () => import('./features/auth/auth-page').then((m) => m.AuthPage);

export const routes: Routes = [
  { path: 'login', loadComponent: authPage, data: { mode: 'login' } },
  { path: 'register', loadComponent: authPage, data: { mode: 'register' } },
  {
    path: 'onboarding',
    canActivate: [authGuard],
    loadComponent: () => import('./features/profile/onboarding-page').then((m) => m.OnboardingPage),
  },
  { path: 'u/:id', loadComponent: () => import('./features/profile/profile-page').then((m) => m.ProfilePage) },
  {
    path: 'settings',
    canActivate: [authGuard],
    loadComponent: () => import('./features/settings/settings-page').then((m) => m.SettingsPage),
  },
  { path: '**', redirectTo: '' }, // ponytail: players/squads land in M3-M4
];
