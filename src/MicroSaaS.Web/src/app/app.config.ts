import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { routes } from './app.routes';
import { tenantInterceptor } from './core/http/interceptors/tenant.interceptor';
import { authInterceptor } from './core/http/interceptors/auth.interceptor';
import { errorInterceptor } from './core/http/interceptors/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([
        tenantInterceptor,
        authInterceptor,
        errorInterceptor
      ])
    ),
    provideAnimationsAsync()
  ]
};
