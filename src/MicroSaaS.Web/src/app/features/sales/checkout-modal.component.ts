import { Component, EventEmitter, Input, OnInit, Output, computed, signal, effect, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { rutValidator } from '../../shared/validators/rut.validator';
import { ConfigService } from '../../core/services/config.service';
import { AuthService } from '../../core/services/auth.service';

export type PaymentMethod = 'CASH' | 'CARD' | 'MIXED';
export type CheckoutStatus = 'IDLE' | 'WAITING_TERMINAL' | 'PROCESSING' | 'SUCCESS' | 'ERROR';

export interface PaymentDetails {
    method: PaymentMethod;
    cashAmount: number;
    cardAmount: number;
    dteType: number;
    customerRut: string;
    customerName: string;
}

@Component({
    selector: 'app-checkout-modal',
    standalone: true,
    imports: [CommonModule, FormsModule],
    templateUrl: './checkout-modal.component.html',
    styleUrls: ['./checkout-modal.component.css']
})
export class CheckoutModalComponent implements OnInit {
    readonly configService = inject(ConfigService);
    readonly authService = inject(AuthService);

    @Input({ required: true }) totalAmount!: number;
    @Input() initialDteType = 39;
    @Input() initialCustomerRut = '';
    @Input() initialCustomerName = '';

    @Output() confirm = new EventEmitter<PaymentDetails>();
    @Output() cancel = new EventEmitter<void>();

    // Estados Reactivos de Pago y DTE
    readonly selectedMethod = signal<PaymentMethod>('CASH');
    readonly selectedDteType = signal<number>(39);
    readonly customerRut = signal<string>('');
    readonly customerName = signal<string>('');
    readonly invoiceValidationError = signal<string | null>(null);

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
                this.cashTendered.set(0);
                this.cardTendered.set(this.totalAmount);
            }
        }, { allowSignalWrites: true });
    }

    ngOnInit(): void {
        this.selectedDteType.set(this.initialDteType || 39);
        this.customerRut.set(this.initialCustomerRut || '');
        this.customerName.set(this.initialCustomerName || '');
    }

    selectDteType(type: number): void {
        this.selectedDteType.set(type);
        this.invoiceValidationError.set(null);
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

    onCashTenderedChange(amount: number): void {
        this.cashTendered.set(amount);
        if (this.selectedMethod() === 'MIXED') {
            const remaining = this.totalAmount - amount;
            this.cardTendered.set(remaining > 0 ? remaining : 0);
        }
    }

    processPayment(): void {
        if (!this.isPaymentValid()) return;

        this.invoiceValidationError.set(null);
        const rut = this.customerRut().trim();
        const name = this.customerName().trim();

        if (this.selectedDteType() === 33) {
            if (!rut) {
                this.invoiceValidationError.set('Para emitir Factura Electrónica (33) debes ingresar el RUT de la empresa cliente.');
                return;
            }
            const rutError = rutValidator({ value: rut } as any);
            if (rutError) {
                this.invoiceValidationError.set('El RUT ingresado no es válido (Módulo 11). Ejemplo válido: 76.543.210-K o 11.111.111-1.');
                return;
            }
            if (!name) {
                this.invoiceValidationError.set('Para emitir Factura Electrónica (33) debes ingresar la Razón Social del cliente.');
                return;
            }
        } else if (rut) {
            const rutError = rutValidator({ value: rut } as any);
            if (rutError) {
                this.invoiceValidationError.set('El RUT ingresado no es válido. Corrígelo o déjalo en blanco para Boleta.');
                return;
            }
        }

        if (this.selectedMethod() === 'CARD' || this.selectedMethod() === 'MIXED') {
            this.status.set('WAITING_TERMINAL');
        } else {
            this.status.set('PROCESSING');
        }

        this.confirm.emit({
            method: this.selectedMethod(),
            cashAmount: this.cashTendered(),
            cardAmount: this.cardTendered(),
            dteType: this.selectedDteType(),
            customerRut: rut,
            customerName: name
        });
    }

    close(): void {
        if (this.status() === 'WAITING_TERMINAL' || this.status() === 'PROCESSING') return;
        this.cancel.emit();
    }
}
