import { Injectable, signal } from '@angular/core';
import { TENANT_ID_STORAGE_KEY } from '../http/interceptors/tenant.interceptor';

@Injectable({
  providedIn: 'root'
})
export class TenantService {
  readonly currentTenantId = signal<string | null>(this.readTenantId());

  getTenantId(): string | null {
    const stored = this.readTenantId();
    if (stored !== this.currentTenantId()) {
      this.currentTenantId.set(stored);
    }
    return stored;
  }

  setTenantId(tenantId: string): void {
    if (typeof window !== 'undefined') {
      localStorage.setItem(TENANT_ID_STORAGE_KEY, tenantId);
    }
    this.currentTenantId.set(tenantId);
  }

  clearTenantId(): void {
    if (typeof window !== 'undefined') {
      localStorage.removeItem(TENANT_ID_STORAGE_KEY);
    }
    this.currentTenantId.set(null);
  }

  private readTenantId(): string | null {
    return typeof window !== 'undefined'
      ? localStorage.getItem(TENANT_ID_STORAGE_KEY)
      : null;
  }
}

