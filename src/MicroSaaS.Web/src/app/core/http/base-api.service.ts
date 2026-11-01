import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * Servicio base de infraestructura HTTP preparado para consumir un backend .NET 10 basado en CQRS
 * (Queries vía GET y Commands vía POST).
 */
@Injectable({
  providedIn: 'root'
})
export class BaseApiService {
  protected readonly http = inject(HttpClient);
  protected readonly baseUrl = environment.apiUrl.replace(/\/+$/, '');

  /**
   * Ejecuta una consulta (Query CQRS) mediante HTTP GET hacia el endpoint especificado.
   */
  public get<T>(
    endpoint: string,
    params?: HttpParams | Record<string, string | number | boolean | readonly (string | number | boolean)[]>
  ): Observable<T> {
    const url = this.buildUrl(endpoint);
    return this.http.get<T>(url, { params });
  }

  /**
   * Despacha un comando (Command CQRS) mediante HTTP POST hacia el endpoint especificado.
   */
  public post<T>(endpoint: string, body: unknown): Observable<T> {
    const url = this.buildUrl(endpoint);
    return this.http.post<T>(url, body);
  }

  private buildUrl(endpoint: string): string {
    let normalizedEndpoint = endpoint.startsWith('/') ? endpoint : `/${endpoint}`;
    if (this.baseUrl.endsWith('/api') && normalizedEndpoint.startsWith('/api/')) {
      normalizedEndpoint = normalizedEndpoint.slice(4);
    }
    return `${this.baseUrl}${normalizedEndpoint}`;
  }
}
