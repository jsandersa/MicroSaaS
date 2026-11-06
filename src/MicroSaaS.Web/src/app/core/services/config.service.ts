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
  defaultMarginPercentage?: number;
  businessActivity?: string | null;
  companyAddress?: string | null;
  companyCommune?: string | null;
}

export interface CertificateStatusDto {
  hasActiveCertificate: boolean;
  fileName: string | null;
  subject: string | null;
  expirationDate: string | null;
  isExpired: boolean;
  daysRemaining: number;
}

export interface CafAuthorizationDto {
  id: string;
  dteType: number;
  dteTypeName: string;
  startFolio: number;
  endFolio: number;
  currentFolio: number;
  totalFolios: number;
  usedFolios: number;
  availableFolios: number;
  usagePercentage: number;
  authorizedAt: string;
  expiresAt: string;
  isActive: boolean;
}

export interface RegisterCafPayload {
  dteType: number;
  startFolio: number;
  endFolio: number;
  xmlContent?: string | null;
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

  getCertificateStatus(): Observable<CertificateStatusDto> {
    return this.http.get<CertificateStatusDto>(`${this.certificatesUrl}/status`);
  }

  getCafAuthorizations(): Observable<CafAuthorizationDto[]> {
    return this.http.get<CafAuthorizationDto[]>(`${this.certificatesUrl}/caf`);
  }

  registerCafAuthorization(payload: RegisterCafPayload): Observable<{ id: string; message: string }> {
    return this.http.post<{ id: string; message: string }>(`${this.certificatesUrl}/caf`, payload);
  }

  uploadCertificate(file: File, password: string): Observable<string> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    formData.append('password', password);

    return this.http.post<string>(`${this.certificatesUrl}/upload`, formData);
  }
}
