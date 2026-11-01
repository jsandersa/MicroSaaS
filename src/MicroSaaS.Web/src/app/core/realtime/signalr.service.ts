import { Injectable, inject, signal } from '@angular/core';
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

  private hubConnection: HubConnection | null = null;
  private readonly stockUpdatedSubject = new Subject<StockDeductedEvent>();

  /**
   * Observable que emite cada vez que el backend notifica el evento "StockDeducted".
   */
  readonly onStockUpdated$: Observable<StockDeductedEvent> =
    this.stockUpdatedSubject.asObservable();

  readonly isConnected = signal<boolean>(false);

  /**
   * Inicia la conexión con el Hub de SignalR (/hubs/inventory) inyectando el JWT
   * y el identificador del Tenant ('X-Tenant-ID').
   */
  async startConnection(): Promise<void> {
    if (
      this.hubConnection &&
      (this.hubConnection.state === HubConnectionState.Connected ||
        this.hubConnection.state === HubConnectionState.Connecting ||
        this.hubConnection.state === HubConnectionState.Reconnecting)
    ) {
      return;
    }

    const baseHost = environment.apiUrl.replace(/\/api\/?$/, '').replace(/\/+$/, '');
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
      .configureLogging(LogLevel.Warning)
      .build();

    this.registerListeners();

    try {
      await this.hubConnection.start();
      this.isConnected.set(true);
    } catch (err) {
      this.isConnected.set(false);
      console.warn('[SignalR] No se pudo establecer conexión con /hubs/inventory:', err);
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

    this.hubConnection.off('StockDeducted');
    this.hubConnection.on('StockDeducted', (payload: StockDeductedEvent) => {
      if (payload && typeof payload.productId === 'string' && typeof payload.newStock === 'number') {
        this.stockUpdatedSubject.next({
          productId: payload.productId,
          newStock: payload.newStock
        });
      }
    });

    this.hubConnection.onreconnected(() => this.isConnected.set(true));
    this.hubConnection.onclose(() => this.isConnected.set(false));
  }
}

