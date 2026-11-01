import { Injectable, inject, signal } from '@angular/core';
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

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl.replace(/\/api\/?$/, '');
  private readonly apiUrl = `${this.baseUrl}/api/Auth`;

  readonly isAuthenticated = signal<boolean>(this.hasToken());

  login(credentials: LoginRequest): Observable<LoginResponse | string> {
    return this.http.post<LoginResponse | string>(`${this.apiUrl}/login`, credentials).pipe(
      tap((response) => {
        const token = typeof response === 'string' ? response : response.token;
        this.saveToken(token);
      })
    );
  }

  saveToken(token: string): void {
    localStorage.setItem(TOKEN_STORAGE_KEY, token);
    this.extractAndSaveTenantId(token);
    this.isAuthenticated.set(true);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_STORAGE_KEY);
  }

  logout(): void {
    localStorage.removeItem(TOKEN_STORAGE_KEY);
    localStorage.removeItem(TENANT_ID_STORAGE_KEY);
    this.isAuthenticated.set(false);
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

  private hasToken(): boolean {
    return typeof window !== 'undefined' && !!localStorage.getItem(TOKEN_STORAGE_KEY);
  }
}
