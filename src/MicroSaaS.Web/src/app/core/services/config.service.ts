import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface TenantConfiguration {
  id?: string;
  taxPercentage: number;
  currencyCode: string;
  currencySymbol: string;
  timeZoneId: string;
  receiptFooterText: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class ConfigService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl.replace(/\/api\/?$/, '');
  private readonly configUrl = `${this.baseUrl}/api/Configurations`;
  private readonly certificatesUrl = `${this.baseUrl}/api/Certificates`;

  getConfiguration(): Observable<TenantConfiguration> {
    return this.http.get<TenantConfiguration>(this.configUrl);
  }

  updateConfiguration(payload: TenantConfiguration): Observable<TenantConfiguration> {
    return this.http.put<TenantConfiguration>(this.configUrl, payload);
  }

  uploadCertificate(file: File, password: string): Observable<string> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    formData.append('password', password);

    return this.http.post<string>(`${this.certificatesUrl}/upload`, formData);
  }
}
