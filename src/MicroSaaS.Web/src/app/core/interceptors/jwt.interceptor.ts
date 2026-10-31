import { HttpInterceptorFn } from '@angular/common/http';

export const TOKEN_STORAGE_KEY = 'auth_token';

/**
 * Interceptor funcional que adjunta el token JWT almacenado en localStorage
 * en la cabecera Authorization: Bearer <token> de cada petición HTTP saliente.
 */
export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
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

