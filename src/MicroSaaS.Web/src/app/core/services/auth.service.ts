import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TOKEN_STORAGE_KEY } from '../interceptors/jwt.interceptor';

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
    this.isAuthenticated.set(true);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_STORAGE_KEY);
  }

  logout(): void {
    localStorage.removeItem(TOKEN_STORAGE_KEY);
    this.isAuthenticated.set(false);
  }

  private hasToken(): boolean {
    return typeof window !== 'undefined' && !!localStorage.getItem(TOKEN_STORAGE_KEY);
  }
}

