import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { TOKEN_STORAGE_KEY } from './auth.interceptor';

/**
 * Interceptor funcional centralizado de errores HTTP (401 Unauthorized, 402 Payment Required, 500, etc.).
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !req.url.toLowerCase().includes('/login')) {
        if (typeof window !== 'undefined') {
          localStorage.removeItem(TOKEN_STORAGE_KEY);
        }
        void router.navigateByUrl('/login');
      } else if (error.status === 402) {
        void router.navigateByUrl('/billing');
      }

      return throwError(() => error);
    })
  );
};

