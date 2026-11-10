import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { authInterceptor } from './auth/auth.interceptor';
import { AuthService } from './auth/auth.service';

// Zoneless: Angular 22 apps ship without zone.js (no polyfill in angular.json), so change detection runs
// when a signal read by a template changes, on template events, and on markForCheck.
// That is why auth state lives in signals: updating them is what refreshes the UI.
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Runs before the first navigation, so guards already know whether the user is signed in.
    provideAppInitializer(() => inject(AuthService).init()),
    provideRouter(routes),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
  ],
};
