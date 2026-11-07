import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PaymentGatewayConfigDto {
  activeProvider: 'MercadoPago' | 'Transbank' | 'GetNet' | 'SumUp' | string;
  integrationMode: 'API_INTEGRATED' | 'STANDALONE_VOUCHER' | string;
  publicKey: string;
  accessToken: string;
  commerceCode: string;
  defaultTerminalId: string;
  updatedAt?: string;
}

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

  readonly activePaymentProvider = signal<string>(
    (typeof window !== 'undefined' && localStorage.getItem('ACTIVE_PAYMENT_PROVIDER')) || 'MercadoPago'
  );

  readonly activeProviderShortName = computed(() => {
    switch (this.activePaymentProvider()) {
      case 'Transbank':
        return 'Transbank';
      case 'GetNet':
        return 'GetNet';
      case 'SumUp':
        return 'SumUp';
      default:
        return 'MP';
    }
  });

  readonly activeProviderFullName = computed(() => {
    switch (this.activePaymentProvider()) {
      case 'Transbank':
        return 'Transbank Webpay / POS';
      case 'GetNet':
        return 'GetNet Santander';
      case 'SumUp':
        return 'SumUp Chile';
      default:
        return 'Mercado Pago Point';
    }
  });

  getPaymentGateway(): Observable<PaymentGatewayConfigDto> {
    return this.http.get<PaymentGatewayConfigDto>(`${this.configUrl}/payment-gateway`).pipe(
      tap((cfg) => {
        if (cfg?.activeProvider) {
          this.setActivePaymentProvider(cfg.activeProvider);
        }
      })
    );
  }

  updatePaymentGateway(payload: PaymentGatewayConfigDto): Observable<PaymentGatewayConfigDto> {
    return this.http.put<PaymentGatewayConfigDto>(`${this.configUrl}/payment-gateway`, payload).pipe(
      tap((cfg) => {
        if (cfg?.activeProvider) {
          this.setActivePaymentProvider(cfg.activeProvider);
        }
      })
    );
  }

  testPaymentGateway(
    payload: PaymentGatewayConfigDto
  ): Observable<{ success: boolean; provider: string; message: string }> {
    return this.http.post<{ success: boolean; provider: string; message: string }>(
      `${this.configUrl}/payment-gateway/test`,
      payload
    );
  }

  setActivePaymentProvider(provider: string): void {
    this.activePaymentProvider.set(provider);
    if (typeof window !== 'undefined') {
      localStorage.setItem('ACTIVE_PAYMENT_PROVIDER', provider);
    }
  }

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
