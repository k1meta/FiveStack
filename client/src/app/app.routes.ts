import { Routes } from '@angular/router';

const authPage = () => import('./features/auth/auth-page').then((m) => m.AuthPage);

export const routes: Routes = [
  { path: 'login', loadComponent: authPage, data: { mode: 'login' } },
  { path: 'register', loadComponent: authPage, data: { mode: 'register' } },
  { path: '**', redirectTo: '' }, // ponytail: players/squads/settings land in M2-M4
];
