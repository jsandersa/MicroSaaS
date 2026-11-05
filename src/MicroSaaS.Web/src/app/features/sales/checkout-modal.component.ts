import { Component, EventEmitter, Input, Output, computed, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export type PaymentMethod = 'CASH' | 'CARD' | 'MIXED';
export type CheckoutStatus = 'IDLE' | 'WAITING_TERMINAL' | 'PROCESSING' | 'SUCCESS' | 'ERROR';

export interface PaymentDetails {
    method: PaymentMethod;
    cashAmount: number;
    cardAmount: number;
}

@Component({
    selector: 'app-checkout-modal',
    standalone: true,
    imports: [CommonModule, FormsModule],
    templateUrl: './checkout-modal.component.html',
    styleUrls: ['./checkout-modal.component.css']
})
export class CheckoutModalComponent {
    @Input({ required: true }) totalAmount!: number;
    @Output() confirm = new EventEmitter<PaymentDetails>();
    @Output() cancel = new EventEmitter<void>();

    // Estados Reactivos
    readonly selectedMethod = signal<PaymentMethod>('CASH');
    readonly status = signal<CheckoutStatus>('IDLE');
    readonly cashTendered = signal<number>(0);
    readonly cardTendered = signal<number>(0);

    constructor() {
        // Efecto para auto-completar montos según el método de pago
        effect(() => {
            const method = this.selectedMethod();
            if (method === 'CASH') {
                this.cashTendered.set(this.totalAmount);
                this.cardTendered.set(0);
            } else if (method === 'CARD') {
                this.cashTendered.set(0);
                this.cardTendered.set(this.totalAmount);
            } else if (method === 'MIXED') {
                // En mixto, el cajero digita el efectivo y calculamos el saldo para la tarjeta
                this.cashTendered.set(0);
                this.cardTendered.set(this.totalAmount);
            }
        }, { allowSignalWrites: true });
    }

    // Cálculos computados
    readonly remainingAmount = computed(() => {
        const remaining = this.totalAmount - (this.cashTendered() + this.cardTendered());
        return remaining > 0 ? remaining : 0;
    });

    readonly changeAmount = computed(() => {
        if (this.selectedMethod() === 'CARD') return 0;
        const change = (this.cashTendered() + this.cardTendered()) - this.totalAmount;
        return change > 0 ? change : 0;
    });

    readonly isPaymentValid = computed(() => {
        return (this.cashTendered() + this.cardTendered()) >= this.totalAmount;
    });

    // Manejo dinámico del pago mixto
    onCashTenderedChange(amount: number): void {
        this.cashTendered.set(amount);
        if (this.selectedMethod() === 'MIXED') {
            const remaining = this.totalAmount - amount;
            this.cardTendered.set(remaining > 0 ? remaining : 0);
        }
    }

    processPayment(): void {
        if (!this.isPaymentValid()) return;

        if (this.selectedMethod() === 'CARD' || this.selectedMethod() === 'MIXED') {
            // Bloqueamos la interfaz visualmente para esperar el Webhook de Mercado Pago
            this.status.set('WAITING_TERMINAL');
        } else {
            this.status.set('PROCESSING');
        }

        // Emitimos el evento hacia el SalesComponent para que ejecute el backend
        this.confirm.emit({
            method: this.selectedMethod(),
            cashAmount: this.cashTendered(),
            cardAmount: this.cardTendered()
        });
    }

    close(): void {
        if (this.status() === 'WAITING_TERMINAL' || this.status() === 'PROCESSING') return;
        this.cancel.emit();
    }
}