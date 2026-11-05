import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';

export interface StockUpdate {
    productId: string;
    newStock: number;
}

@Injectable({
    providedIn: 'root'
})
export class InventorySignalRService {
    private hubConnection!: signalR.HubConnection;
    private readonly stockUpdatesSource = new Subject<StockUpdate>();

    public readonly stockUpdates$ = this.stockUpdatesSource.asObservable();

    // Puerto real expuesto por el contenedor microsaas-api en Docker (8080:8080)
    private readonly hubUrl = 'http://localhost:8080/hubs/inventory';

    constructor() {
        this.buildConnection();
        this.ensureConnection();
    }

    private buildConnection(): void {
        this.hubConnection = new signalR.HubConnectionBuilder()
            .withUrl(this.hubUrl, {
                withCredentials: true
            })
            .withAutomaticReconnect([0, 2000, 5000, 10000])
            .configureLogging(signalR.LogLevel.Information)
            .build();

        this.hubConnection.on('ReceiveStockUpdate', (productId: string, newStock: number) => {
            console.log(`🔄 SignalR evento recibido -> ProductId: "${productId}", Nuevo Stock: ${newStock}`);
            this.stockUpdatesSource.next({ productId, newStock });
        });

        this.hubConnection.onreconnecting(error => {
            console.warn('⚠️ SignalR: Reconectando...', error);
        });

        this.hubConnection.onreconnected(connectionId => {
            console.log(`✅ SignalR: Reconectado con ID: ${connectionId}`);
        });

        this.hubConnection.onclose(error => {
            console.error('❌ SignalR: Conexión cerrada', error);
        });
    }

    public ensureConnection(): void {
        if (!this.hubConnection) {
            this.buildConnection();
        }

        if (this.hubConnection.state === signalR.HubConnectionState.Disconnected) {
            this.hubConnection
                .start()
                .then(() => console.log(`✅ SignalR conectado exitosamente a: ${this.hubUrl}`))
                .catch(err => console.error(`❌ Error conectando SignalR a ${this.hubUrl}:`, err));
        }
    }
}