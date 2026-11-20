import { Routes } from '@angular/router';
import { authGuard, onboardedGuard } from './core/auth.guard';

const authPage = () => import('./features/auth/auth-page').then((m) => m.AuthPage);

const legal = () => import('./features/legal/legal-pages');

const account = () => import('./features/auth/account-pages');

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./features/landing/landing-page').then((m) => m.LandingPage),
  },
  { path: 'privacy', loadComponent: () => legal().then((m) => m.PrivacyPage) },
  { path: 'terms', loadComponent: () => legal().then((m) => m.TermsPage) },
  { path: 'login', loadComponent: authPage, data: { mode: 'login' } },
  { path: 'register', loadComponent: authPage, data: { mode: 'register' } },
  { path: 'confirm-email', loadComponent: () => account().then((m) => m.ConfirmEmailPage) },
  { path: 'forgot-password', loadComponent: () => account().then((m) => m.ForgotPasswordPage) },
  { path: 'reset-password', loadComponent: () => account().then((m) => m.ResetPasswordPage) },
  {
    path: 'onboarding',
    canActivate: [authGuard],
    loadComponent: () => import('./features/profile/onboarding-page').then((m) => m.OnboardingPage),
  },
  {
    path: 'players',
    loadComponent: () => import('./features/players/players-page').then((m) => m.PlayersPage),
  },
  {
    path: 'u/:id',
    loadComponent: () => import('./features/profile/profile-page').then((m) => m.ProfilePage),
  },
  {
    path: 'squads',
    loadComponent: () => import('./features/squads/squads-page').then((m) => m.SquadsPage),
  },
  {
    path: 'squads/new',
    canActivate: [authGuard, onboardedGuard],
    loadComponent: () => import('./features/squads/squad-form').then((m) => m.SquadForm),
  },
  {
    path: 'squads/:id',
    loadComponent: () => import('./features/squads/squad-page').then((m) => m.SquadPage),
  },
  ...['messages', 'messages/:id'].map((path) => ({
    path,
    canActivate: [authGuard, onboardedGuard],
    loadComponent: () => import('./features/chat/messages-page').then((m) => m.MessagesPage),
  })),
  {
    path: 'settings',
    canActivate: [authGuard],
    loadComponent: () => import('./features/settings/settings-page').then((m) => m.SettingsPage),
  },
  { path: '**', redirectTo: '' },
];
