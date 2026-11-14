import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** The session is loaded by an app initializer before the first navigation, so a sync check is enough. */
export const authGuard: CanActivateFn = (_route, state) =>
  inject(AuthService).user()
    ? true
    : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });

/** After authGuard: pages that need a saved profile send the player through onboarding first. */
export const onboardedGuard: CanActivateFn = (_route, state) =>
  inject(AuthService).user()?.onboarded
    ? true
    : inject(Router).createUrlTree(['/onboarding'], { queryParams: { returnUrl: state.url } });
