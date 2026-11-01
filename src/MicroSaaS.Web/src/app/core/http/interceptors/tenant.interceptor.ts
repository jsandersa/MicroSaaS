import { HttpInterceptorFn } from '@angular/common/http';

export const TENANT_ID_STORAGE_KEY = 'tenant_id';

/**
 * Interceptor funcional Multi-Tenant que inyecta la cabecera 'X-Tenant-ID'
 * en las peticiones salientes, excluyendo explícitamente rutas públicas de onboarding.
 */
export const tenantInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.url.includes('/api/onboarding') || req.url.includes('/api/tenants/register')) {
    return next(req);
  }

  const tenantId = typeof window !== 'undefined'
    ? localStorage.getItem(TENANT_ID_STORAGE_KEY)
    : null;

  if (tenantId) {
    const tenantReq = req.clone({
      setHeaders: {
        'X-Tenant-ID': tenantId
      }
    });
    return next(tenantReq);
  }

  return next(req);
};

