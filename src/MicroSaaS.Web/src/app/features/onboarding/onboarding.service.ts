import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseApiService } from '../../core/http/base-api.service';

export interface RegisterTenantCommand {
  companyName: string;
  rutEmpresa: string;
  adminEmail: string;
  password: string;
}

export interface RegisterTenantResponse {
  tenantId: string;
}

@Injectable({
  providedIn: 'root'
})
export class OnboardingService extends BaseApiService {
  registerTenant(command: RegisterTenantCommand): Observable<RegisterTenantResponse> {
    const payload = {
      ...command,
      rut: command.rutEmpresa,
      legalName: command.companyName,
      businessLine: 'Comercialización y Servicios',
      address: 'Santiago, Chile',
      adminEmail: command.adminEmail,
      adminPassword: command.password
    };

    return this.post<RegisterTenantResponse>('/api/tenants/register', payload);
  }
}

