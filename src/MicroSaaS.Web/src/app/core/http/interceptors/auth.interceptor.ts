import { HttpInterceptorFn } from '@angular/common/http';

export const TOKEN_STORAGE_KEY = 'auth_token';

/**
 * Interceptor funcional de autenticación que adjunta el token JWT ('Authorization: Bearer <token>')
 * almacenado en localStorage en cada petición HTTP saliente hacia la API.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = typeof window !== 'undefined'
    ? localStorage.getItem(TOKEN_STORAGE_KEY)
    : null;

  if (token) {
    const authReq = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });
    return next(authReq);
  }

  return next(req);
};

