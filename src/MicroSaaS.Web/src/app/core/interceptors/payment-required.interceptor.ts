import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

/**
 * Interceptor global que captura respuestas HTTP 402 (Payment Required)
 * en todas las peticiones salientes y redirige forzosamente al usuario a la vista /billing.
 */
export const paymentRequiredInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 402) {
        void router.navigateByUrl('/billing');
      }
      return throwError(() => error);
    })
  );
};
