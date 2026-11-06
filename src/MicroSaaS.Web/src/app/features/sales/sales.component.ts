import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { catchError, debounceTime, distinctUntilChanged, finalize, of, startWith, switchMap, tap } from 'rxjs';

import { rutValidator } from '../../shared/validators/rut.validator';
import { ProductDTO, SaleItem } from './models/sales.models';
import {
  CashSessionSummary,
  CloseCashRegisterResult,
  EmittedDteDto,
  SalesService
} from './sales.service';
import { SignalRService } from '../../core/realtime/signalr.service';
import { CheckoutModalComponent, PaymentDetails } from './checkout-modal.component';

@Component({
  selector: 'app-sales',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, CheckoutModalComponent],
  templateUrl: './sales.component.html',
  styleUrl: './sales.component.css'
})
export class SalesComponent implements OnInit {
  private readonly salesService = inject(SalesService);
  private readonly signalRService = inject(SignalRService);
  private readonly destroyRef = inject(DestroyRef);

  // Controles reactivos de POS
  readonly searchControl = new FormControl('', { nonNullable: true });
  readonly customerRutControl = new FormControl('', {
    nonNullable: true,
    validators: [rutValidator]
  });

  // Controles reactivos de Apertura y Cierre de Caja
  readonly openingAmountControl = new FormControl<number>(50000, {
    nonNullable: true,
    validators: [Validators.required, Validators.min(0)]
  });
  readonly actualCashAmountControl = new FormControl<number>(0, {
    nonNullable: true,
    validators: [Validators.required, Validators.min(0)]
  });
  readonly actualCardAmountControl = new FormControl<number>(0, {
    nonNullable: true,
    validators: [Validators.required, Validators.min(0)]
  });

  // Signals de estado del POS
  readonly products = signal<ProductDTO[]>([]);
  readonly cart = signal<SaleItem[]>([]);
  readonly isSearching = signal<boolean>(false);
  readonly isProcessing = signal<boolean>(false);
  readonly showCheckoutModal = signal<boolean>(false);
  readonly successMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);

  // Signals de Control de Caja
  readonly isCashOpen = signal<boolean>(false);
  readonly currentCashSession = signal<CashSessionSummary | null>(null);
  readonly showOpenCashModal = signal<boolean>(false);
  readonly showCloseCashModal = signal<boolean>(false);
  readonly isCashActionLoading = signal<boolean>(false);
  readonly closeCashResult = signal<CloseCashRegisterResult | null>(null);

  // Signals de Visor de DTEs Emitidos
  readonly showDtesModal = signal<boolean>(false);
  readonly isLoadingDtes = signal<boolean>(false);
  readonly emittedDtes = signal<EmittedDteDto[]>([]);
  readonly selectedDte = signal<EmittedDteDto | null>(null);

  // Signals computados para totales
  readonly subtotalGeneral = computed(() =>
    this.cart().reduce((acc, item) => acc + (item?.subtotal || 0), 0)
  );
  readonly totalIva = computed(() => Math.round(this.subtotalGeneral() * 0.19));
  readonly totalFinal = computed(() => this.subtotalGeneral() + this.totalIva());
  readonly totalItemsCount = computed(() =>
    this.cart().reduce((acc, item) => acc + (item?.quantity || 0), 0)
  );

  ngOnInit(): void {
    void this.signalRService.startConnection();
    this.loadCurrentCashSession();
    this.setupProductSearch();
    this.setupRealTimeInventory();
  }

  loadCurrentCashSession(): void {
    this.salesService
      .getCurrentCashSession()
      .pipe(
        catchError(() => of({ isOpen: false, session: null })),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((res) => {
        this.isCashOpen.set(res.isOpen);
        this.currentCashSession.set(res.session);
      });
  }

  openDtesModal(): void {
    this.showDtesModal.set(true);
    this.isLoadingDtes.set(true);

    this.salesService
      .getEmittedDtes()
      .pipe(finalize(() => this.isLoadingDtes.set(false)))
      .subscribe({
        next: (list) => {
          this.emittedDtes.set(list ?? []);
          this.selectedDte.set(list && list.length > 0 ? list[0] : null);
        },
        error: () => {
          this.emittedDtes.set([]);
          this.selectedDte.set(null);
        }
      });
  }

  selectDte(dte: EmittedDteDto): void {
    this.selectedDte.set(dte);
  }

  closeDtesModal(): void {
    this.showDtesModal.set(false);
  }

  closeOpenCashModal(): void {
    this.showOpenCashModal.set(false);
  }

  closeCloseCashModal(): void {
    this.showCloseCashModal.set(false);
  }

  downloadDteXml(dte: EmittedDteDto): void {
    if (!dte || !dte.xmlContent) return;
    const blob = new Blob([dte.xmlContent], { type: 'application/xml;charset=iso-8859-1' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `DTE_T${dte.dteType}_F${dte.folio}.xml`;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  openCashModal(): void {
    this.errorMessage.set(null);
    this.openingAmountControl.setValue(50000);
    this.showOpenCashModal.set(true);
  }

  confirmOpenCashRegister(): void {
    if (this.openingAmountControl.invalid || this.isCashActionLoading()) {
      return;
    }

    this.isCashActionLoading.set(true);
    this.errorMessage.set(null);

    const openingAmount = Number(this.openingAmountControl.value) || 0;

    this.salesService
      .openCashRegister(openingAmount)
      .pipe(finalize(() => this.isCashActionLoading.set(false)))
      .subscribe({
        next: () => {
          this.showOpenCashModal.set(false);
          this.loadCurrentCashSession();
          this.successMessage.set(
            `Sesión de caja abierta exitosamente con fondo inicial de $${openingAmount.toLocaleString('es-CL')}.`
          );
        },
        error: (err) => {
          this.errorMessage.set(
            err?.error?.message || err?.message || 'No fue posible abrir la sesión de caja.'
          );
        }
      });
  }

  openCloseCashModal(): void {
    this.errorMessage.set(null);
    this.closeCashResult.set(null);
    const defaultCash = this.currentCashSession()?.openingAmount ?? 0;
    this.actualCashAmountControl.setValue(defaultCash);
    this.actualCardAmountControl.setValue(0);
    this.showCloseCashModal.set(true);
  }

  confirmCloseCashRegister(): void {
    if (
      this.actualCashAmountControl.invalid ||
      this.actualCardAmountControl.invalid ||
      this.isCashActionLoading()
    ) {
      return;
    }

    this.isCashActionLoading.set(true);
    this.errorMessage.set(null);

    const actualCash = Number(this.actualCashAmountControl.value) || 0;
    const actualCard = Number(this.actualCardAmountControl.value) || 0;

    this.salesService
      .closeCashRegister(actualCash, actualCard)
      .pipe(finalize(() => this.isCashActionLoading.set(false)))
      .subscribe({
        next: (result) => {
          this.closeCashResult.set(result);
          this.isCashOpen.set(false);
          this.currentCashSession.set(null);
          this.successMessage.set('Cierre y arqueo de caja registrado exitosamente.');
        },
        error: (err) => {
          this.errorMessage.set(
            err?.error?.message || err?.message || 'No fue posible cerrar la sesión de caja.'
          );
        }
      });
  }

  private setupProductSearch(): void {
    this.searchControl.valueChanges
      .pipe(
        startWith(this.searchControl.value),
        debounceTime(300),
        distinctUntilChanged(),
        tap(() => this.isSearching.set(true)),
        switchMap((term) =>
          this.salesService.searchProducts(term).pipe(
            catchError(() => {
              this.errorMessage.set('No fue posible consultar el inventario de productos.');
              return of([] as ProductDTO[]);
            }),
            finalize(() => this.isSearching.set(false))
          )
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((results) => {
        this.products.set(results);
      });
  }

  private setupRealTimeInventory(): void {
    this.signalRService.onStockUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((update) => {
        if (!update || !update.productId) return;

        const targetId = update.productId.toString().trim().toLowerCase();

        this.products.update((current) =>
          current.map((p) =>
            p?.id && p.id.toString().trim().toLowerCase() === targetId
              ? { ...p, stock: update.newStock }
              : p
          )
        );

        this.cart.update((currentCart) =>
          currentCart
            .map((item) => {
              if (item?.product?.id && item.product.id.toString().trim().toLowerCase() === targetId) {
                const updatedProduct = { ...item.product, stock: update.newStock };
                const newQuantity = Math.min(item.quantity, update.newStock);
                return {
                  ...item,
                  product: updatedProduct,
                  quantity: newQuantity,
                  subtotal: newQuantity * updatedProduct.price
                };
              }
              return item;
            })
            .filter((item) => item && item.quantity > 0)
        );
      });
  }

  getQuantityInCart(productId: string): number {
    const found = this.cart().find((item) => item?.product?.id === productId);
    return found ? found.quantity : 0;
  }

  addToCart(product: ProductDTO): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);

    if (!product || product.stock <= 0) {
      this.errorMessage.set(`El producto "${product?.name || 'seleccionado'}" no tiene stock disponible.`);
      return;
    }

    const currentItems = this.cart();
    const existingIndex = currentItems.findIndex((item) => item?.product?.id === product.id);

    if (existingIndex > -1) {
      const existingItem = currentItems[existingIndex];
      if (existingItem.quantity + 1 > product.stock) {
        this.errorMessage.set(
          `Stock insuficiente para "${product.name}". Máximo disponible: ${product.stock}.`
        );
        return;
      }

      const updatedQuantity = existingItem.quantity + 1;
      const updatedItems = [...currentItems];
      updatedItems[existingIndex] = {
        ...existingItem,
        quantity: updatedQuantity,
        subtotal: updatedQuantity * product.price
      };
      this.cart.set(updatedItems);
    } else {
      const newItem: SaleItem = {
        product,
        quantity: 1,
        subtotal: product.price
      };
      this.cart.set([...currentItems, newItem]);
    }
  }

  decrementQuantity(productId: string): void {
    const currentItems = this.cart();
    const existingIndex = currentItems.findIndex((item) => item?.product?.id === productId);
    if (existingIndex === -1) return;

    const existingItem = currentItems[existingIndex];
    if (existingItem.quantity <= 1) {
      this.removeFromCart(productId);
      return;
    }

    const updatedQuantity = existingItem.quantity - 1;
    const updatedItems = [...currentItems];
    updatedItems[existingIndex] = {
      ...existingItem,
      quantity: updatedQuantity,
      subtotal: updatedQuantity * existingItem.product.price
    };
    this.cart.set(updatedItems);
  }

  removeFromCart(productId: string): void {
    this.cart.update((items) => items.filter((item) => item?.product?.id !== productId));
  }

  clearCart(): void {
    this.cart.set([]);
  }

  onCheckout(): void {
    if (this.isProcessing() || this.cart().length === 0) return;

    if (!this.isCashOpen()) {
      this.errorMessage.set('Debes abrir una sesión de caja antes de procesar ventas.');
      this.openCashModal();
      return;
    }

    if (this.customerRutControl.value && this.customerRutControl.invalid) {
      this.customerRutControl.markAsTouched();
      this.errorMessage.set('El RUT ingresado no es válido. Corrígelo o déjalo en blanco.');
      return;
    }

    this.showCheckoutModal.set(true);
  }

  processFinalSale(payment: PaymentDetails): void {
    this.isProcessing.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const command = {
      folio: 0,
      tipoDte: 39,
      fechaEmision: new Date().toISOString(),
      montoNeto: this.subtotalGeneral(),
      iva: this.totalIva(),
      montoTotal: this.totalFinal(),

      rutCliente: this.customerRutControl.value.trim() || null,
      razonSocialCliente: null,

      paymentMethod: payment.method,
      cashAmount: payment.cashAmount,
      cardAmount: payment.cardAmount,

      items: this.cart()
        .filter((item) => item && item.product)
        .map((item) => ({
          productId: item.product.id,
          quantity: item.quantity,
          unitPrice: item.product.price,
          nombreItem: item.product.name,
          cantidad: item.quantity,
          precioUnitario: item.product.price,
          montoItem: item.subtotal,
          esExento: false,
          sku: item.product.sku || ''
        }))
    };

    this.salesService
      .processSale(command)
      .pipe(
        finalize(() => {
          this.isProcessing.set(false);
          this.showCheckoutModal.set(false);
        })
      )
      .subscribe({
        next: () => {
          this.clearCart();
          this.customerRutControl.reset('');
          this.loadCurrentCashSession();
          this.successMessage.set('¡Venta registrada, stock descontado en Kardex y DTE emitido!');
          setTimeout(() => this.successMessage.set(null), 4000);
        },
        error: (err) => {
          this.errorMessage.set(
            err?.error?.message || err?.message || 'Ocurrió un error al procesar la venta. Intenta nuevamente.'
          );
        }
      });
  }
}
