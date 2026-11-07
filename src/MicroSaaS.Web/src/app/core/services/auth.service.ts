import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TOKEN_STORAGE_KEY } from '../http/interceptors/auth.interceptor';
import { TENANT_ID_STORAGE_KEY } from '../http/interceptors/tenant.interceptor';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
}

export interface TenantUserDto {
  id: string;
  email: string;
  role: 'Owner' | 'Cashier' | 'Accountant' | string;
  cashRegisterId?: string | null;
  paymentDeviceId?: string | null;
  createdAt: string;
}

export interface UpsertTenantUserRequest {
  email: string;
  password: string;
  role: 'Owner' | 'Cashier' | 'Accountant' | string;
  cashRegisterId?: string | null;
  paymentDeviceId?: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl.replace(/\/api\/?$/, '');
  private readonly apiUrl = `${this.baseUrl}/api/Auth`;

  readonly isAuthenticated = signal<boolean>(this.hasToken());
  readonly userRole = signal<string>(this.extractClaimFromStoredToken('role') || 'Owner');
  readonly userEmail = signal<string>(this.extractClaimFromStoredToken('email') || '');
  readonly cashRegisterId = signal<string>(this.resolveCashRegisterId());
  readonly paymentDeviceId = signal<string>(this.resolvePaymentDeviceId());
  readonly isAccountant = computed(() => this.userRole().toLowerCase() === 'accountant');
  readonly isCashier = computed(() => this.userRole().toLowerCase() === 'cashier');
  readonly isOwner = computed(() => !this.isAccountant() && !this.isCashier());

  login(credentials: LoginRequest): Observable<LoginResponse | string> {
    return this.http.post<LoginResponse | string>(`${this.apiUrl}/login`, credentials).pipe(
      tap((response) => {
        const token = typeof response === 'string' ? response : response.token;
        this.saveToken(token);
      })
    );
  }

  getAccountantInfo(): Observable<{ hasAccountant: boolean; email: string }> {
    return this.http.get<{ hasAccountant: boolean; email: string }>(`${this.apiUrl}/accountant`);
  }

  upsertAccountant(email: string, password: string): Observable<{ email: string; message: string }> {
    return this.http.post<{ email: string; message: string }>(`${this.apiUrl}/accountant`, {
      email,
      password
    });
  }

  getTenantUsers(): Observable<TenantUserDto[]> {
    return this.http.get<TenantUserDto[]>(`${this.apiUrl}/users`);
  }

  upsertTenantUser(
    request: UpsertTenantUserRequest
  ): Observable<{ message: string; user: TenantUserDto }> {
    return this.http.post<{ message: string; user: TenantUserDto }>(`${this.apiUrl}/users`, request);
  }

  deleteTenantUser(id: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.apiUrl}/users/${id}`);
  }

  resetPassword(email: string, newPassword: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/reset-password`, {
      email,
      newPassword
    });
  }

  saveToken(token: string): void {
    localStorage.setItem(TOKEN_STORAGE_KEY, token);
    this.extractAndSaveTenantId(token);
    this.userRole.set(this.extractClaimFromStoredToken('role') || 'Owner');
    this.userEmail.set(this.extractClaimFromStoredToken('email') || '');
    this.cashRegisterId.set(this.resolveCashRegisterId());
    this.paymentDeviceId.set(this.resolvePaymentDeviceId());
    this.isAuthenticated.set(true);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_STORAGE_KEY);
  }

  logout(): void {
    localStorage.removeItem(TOKEN_STORAGE_KEY);
    localStorage.removeItem(TENANT_ID_STORAGE_KEY);
    this.userRole.set('Owner');
    this.userEmail.set('');
    this.cashRegisterId.set('CAJA-01');
    this.paymentDeviceId.set('MP-POINT-01');
    this.isAuthenticated.set(false);
  }

  private resolveCashRegisterId(): string {
    const fromClaim = this.extractClaimFromStoredToken('cash_register_id');
    if (fromClaim) return fromClaim;
    const email = this.extractClaimFromStoredToken('email');
    if (email.includes('2')) return 'CAJA-02';
    if (email.includes('3')) return 'CAJA-03';
    return 'CAJA-01';
  }

  private resolvePaymentDeviceId(): string {
    const fromClaim = this.extractClaimFromStoredToken('payment_device_id');
    if (fromClaim) return fromClaim;
    const email = this.extractClaimFromStoredToken('email');
    if (email.includes('2')) return 'MP-POINT-02';
    if (email.includes('3')) return 'MP-POINT-03';
    return 'MP-POINT-01';
  }

  private extractAndSaveTenantId(token: string): void {
    try {
      const payloadBase64 = token.split('.')[1];
      if (!payloadBase64) return;
      const decodedJson = atob(payloadBase64.replace(/-/g, '+').replace(/_/g, '/'));
      const payload = JSON.parse(decodedJson) as Record<string, string>;
      const tenantId = payload['tenant_id'] || payload['tenant_schema'];
      if (tenantId) {
        localStorage.setItem(TENANT_ID_STORAGE_KEY, tenantId);
      }
    } catch {
      // Ignorar si el token no es un JWT estándar decodificable
    }
  }

  private extractClaimFromStoredToken(claimKey: string): string {
    if (typeof window === 'undefined') return '';
    const token = localStorage.getItem(TOKEN_STORAGE_KEY);
    if (!token) return '';
    try {
      const payloadBase64 = token.split('.')[1];
      if (!payloadBase64) return '';
      const decodedJson = atob(payloadBase64.replace(/-/g, '+').replace(/_/g, '/'));
      const payload = JSON.parse(decodedJson) as Record<string, string>;
      if (claimKey === 'role') {
        return (
          payload['role'] ||
          payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ||
          ''
        );
      }
      if (claimKey === 'email') {
        return (
          payload['email'] ||
          payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ||
          ''
        );
      }
      return payload[claimKey] || '';
    } catch {
      return '';
    }
  }

  private hasToken(): boolean {
    return typeof window !== 'undefined' && !!localStorage.getItem(TOKEN_STORAGE_KEY);
  }
}
