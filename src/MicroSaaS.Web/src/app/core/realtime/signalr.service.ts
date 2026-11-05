import { Injectable, inject, signal, NgZone } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel
} from '@microsoft/signalr';
import { Observable, Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../services/auth.service';
import { TenantService } from '../services/tenant.service';

export interface StockDeductedEvent {
  productId: string;
  newStock: number;
}

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private readonly authService = inject(AuthService);
  private readonly tenantService = inject(TenantService);
  private readonly ngZone = inject(NgZone);

  private hubConnection: HubConnection | null = null;
  private readonly stockUpdatedSubject = new Subject<StockDeductedEvent>();

  readonly onStockUpdated$: Observable<StockDeductedEvent> =
    this.stockUpdatedSubject.asObservable();

  readonly isConnected = signal<boolean>(false);

  async startConnection(): Promise<void> {
    if (
      this.hubConnection &&
      (this.hubConnection.state === HubConnectionState.Connected ||
        this.hubConnection.state === HubConnectionState.Connecting ||
        this.hubConnection.state === HubConnectionState.Reconnecting)
    ) {
      return;
    }

    // Si environment.apiUrl es '/api' (relativo), forzamos http://localhost:8080
    // para evitar que NGINX en el puerto 4200 rechace el POST /negotiate con 405 Not Allowed
    const cleanedUrl = environment.apiUrl.replace(/\/api\/?$/, '').replace(/\/+$/, '');
    const baseHost = cleanedUrl.startsWith('http') ? cleanedUrl : 'http://localhost:8080';

    const tenantId = this.tenantService.getTenantId() ?? '';
    const hubUrl = tenantId
      ? `${baseHost}/hubs/inventory?X-Tenant-ID=${encodeURIComponent(tenantId)}`
      : `${baseHost}/hubs/inventory`;

    this.hubConnection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => this.authService.getToken() ?? '',
        headers: tenantId ? { 'X-Tenant-ID': tenantId } : undefined
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    this.registerListeners();

    try {
      await this.hubConnection.start();
      this.isConnected.set(true);
      console.log('✅ [SignalRService] Conectado exitosamente a:', hubUrl);
    } catch (err) {
      this.isConnected.set(false);
      console.error('❌ [SignalRService] Error conectando a /hubs/inventory:', err);
    }
  }

  async stopConnection(): Promise<void> {
    if (!this.hubConnection) {
      return;
    }

    try {
      await this.hubConnection.stop();
    } finally {
      this.isConnected.set(false);
    }
  }

  private registerListeners(): void {
    if (!this.hubConnection) {
      return;
    }

    const processIncomingStock = (payload: any, arg2?: any) => {
      console.log('📦 [SignalRService] Evento StockDeducted recibido:', payload, arg2);

      let pId: string | undefined;
      let nStock: number | undefined;

      if (payload && typeof payload === 'object') {
        pId = payload.productId ?? payload.ProductId ?? payload.productid;
        const rawStock = payload.newStock ?? payload.NewStock ?? payload.newstock;
        nStock = rawStock !== undefined ? Number(rawStock) : undefined;
      } else if (typeof payload === 'string' && arg2 !== undefined) {
        pId = payload;
        nStock = Number(arg2);
      }

      if (pId && nStock !== undefined && !Number.isNaN(nStock)) {
        this.ngZone.run(() => {
          this.stockUpdatedSubject.next({
            productId: pId!,
            newStock: nStock!
          });
        });
      }
    };

    this.hubConnection.off('StockDeducted');
    this.hubConnection.on('StockDeducted', processIncomingStock);

    this.hubConnection.onreconnected(() => this.isConnected.set(true));
    this.hubConnection.onclose(() => this.isConnected.set(false));
  }
}