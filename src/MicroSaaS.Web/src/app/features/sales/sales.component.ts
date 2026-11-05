import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { catchError, debounceTime, distinctUntilChanged, finalize, of, startWith, switchMap, tap } from 'rxjs';

import { rutValidator } from '../../shared/validators/rut.validator';
import { ProductDTO, SaleItem } from './models/sales.models';
import { SalesService } from './sales.service';
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

  // Controles reactivos
  readonly searchControl = new FormControl('', { nonNullable: true });
  readonly customerRutControl = new FormControl('', {
    nonNullable: true,
    validators: [rutValidator]
  });

  // Signals de estado
  readonly products = signal<ProductDTO[]>([]);
  readonly cart = signal<SaleItem[]>([]);
  readonly isSearching = signal<boolean>(false);
  readonly isProcessing = signal<boolean>(false);
  readonly showCheckoutModal = signal<boolean>(false);
  readonly successMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);

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
    console.log('🚀 SalesComponent inicializado - versión Payload Actualizado (Safe Map)');
    void this.signalRService.startConnection();
    this.setupProductSearch();
    this.setupRealTimeInventory();
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
            (p?.id && p.id.toString().trim().toLowerCase() === targetId)
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

      // Filtro de seguridad y mapeo seguro de propiedades
      items: this.cart()
        .filter(item => item && item.product)
        .map(item => ({
          nombreItem: item.product.name,
          cantidad: item.quantity,
          precioUnitario: item.product.price,
          montoItem: item.subtotal,
          esExento: false,
          sku: item.product.sku || '' // Fallback por si el SKU viene indefinido
        }))
    };

    this.salesService
      .processSale(command as any)
      .pipe(finalize(() => {
        this.isProcessing.set(false);
        this.showCheckoutModal.set(false);
      }))
      .subscribe({
        next: () => {
          this.clearCart();
          this.customerRutControl.reset('');
          this.successMessage.set('¡Venta registrada y emitida exitosamente!');
          setTimeout(() => this.successMessage.set(null), 4000);
        },
        error: (err: Error) => {
          this.errorMessage.set(
            err.message || 'Ocurrió un error al procesar la venta. Intenta nuevamente.'
          );
        }
      });
  }
}