import { CommonModule } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { catchError, debounceTime, distinctUntilChanged, finalize, of, startWith, switchMap, tap } from 'rxjs';
import { rutValidator } from '../../shared/validators/rut.validator';
import { CreateSaleCommand, ProductDTO, SaleItem } from './models/sales.models';
import { SalesService } from './sales.service';

@Component({
  selector: 'app-sales',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="pos-wrapper">
      <header class="pos-header">
        <div>
          <h1>Punto de Venta (POS)</h1>
          <p class="subtitle">Búsqueda rápida de inventario y emisión de ventas con IVA (19%)</p>
        </div>
        <div class="pos-stats">
          <span class="badge-items">{{ totalItemsCount() }} unidades en carrito</span>
        </div>
      </header>

      @if (successMessage()) {
        <div class="alert alert-success" role="status">
          <span>{{ successMessage() }}</span>
          <button type="button" class="alert-close" (click)="successMessage.set(null)">×</button>
        </div>
      }

      @if (errorMessage()) {
        <div class="alert alert-error" role="alert">
          <span>{{ errorMessage() }}</span>
          <button type="button" class="alert-close" (click)="errorMessage.set(null)">×</button>
        </div>
      }

      <div class="pos-grid">
        <!-- Columna Izquierda: Buscador y Catálogo de Productos -->
        <section class="catalog-panel card">
          <div class="search-header">
            <h2>Catálogo de Productos</h2>
            <div class="search-box">
              <input
                type="text"
                [formControl]="searchControl"
                placeholder="Buscar por nombre o código SKU..."
                aria-label="Buscar productos"
              />
              @if (isSearching()) {
                <span class="search-spinner" aria-hidden="true"></span>
              }
            </div>
          </div>

          <div class="products-list">
            @if (products().length === 0 && !isSearching()) {
              <div class="empty-state">
                <p>No se encontraron productos para "{{ searchControl.value }}".</p>
              </div>
            } @else {
              @for (product of products(); track product.id) {
                <article class="product-card" [class.out-of-stock]="product.stock <= 0">
                  <div class="product-info">
                    <span class="product-sku">{{ product.sku }}</span>
                    <h3 class="product-name">{{ product.name }}</h3>
                    <span
                      class="product-stock"
                      [class.low-stock]="product.stock > 0 && product.stock <= 5"
                    >
                      Stock disponible: {{ product.stock }}
                    </span>
                  </div>
                  <div class="product-actions">
                    <span class="product-price">\${{ product.price | number: '1.0-0' }}</span>
                    <button
                      type="button"
                      class="btn-add"
                      [disabled]="product.stock <= 0 || getQuantityInCart(product.id) >= product.stock"
                      (click)="addToCart(product)"
                    >
                      @if (product.stock <= 0) {
                        Sin Stock
                      } @else if (getQuantityInCart(product.id) >= product.stock) {
                        Máx. Stock
                      } @else {
                        + Agregar
                      }
                    </button>
                  </div>
                </article>
              }
            }
          </div>
        </section>

        <!-- Columna Derecha: Carrito de Compras y Checkout -->
        <section class="cart-panel card">
          <div class="cart-header">
            <h2>Carrito de Venta</h2>
            @if (cart().length > 0) {
              <button
                type="button"
                class="btn-clear"
                [disabled]="isProcessing()"
                (click)="clearCart()"
              >
                Vaciar carrito
              </button>
            }
          </div>

          <div class="cart-items">
            @if (cart().length === 0) {
              <div class="empty-cart">
                <p>El carrito está vacío.</p>
                <small>Selecciona productos del catálogo izquierdo para comenzar la venta.</small>
              </div>
            } @else {
              @for (item of cart(); track item.product.id) {
                <div class="cart-item">
                  <div class="cart-item-info">
                    <strong>{{ item.product.name }}</strong>
                    <small>
                      {{ item.product.sku }} · \${{ item.product.price | number: '1.0-0' }} c/u
                    </small>
                  </div>

                  <div class="cart-item-controls">
                    <button
                      type="button"
                      class="qty-btn"
                      [disabled]="isProcessing()"
                      (click)="decrementQuantity(item.product.id)"
                      aria-label="Disminuir cantidad"
                    >
                      −
                    </button>
                    <span class="qty-value">{{ item.quantity }}</span>
                    <button
                      type="button"
                      class="qty-btn"
                      [disabled]="isProcessing() || item.quantity >= item.product.stock"
                      (click)="addToCart(item.product)"
                      aria-label="Aumentar cantidad"
                    >
                      +
                    </button>
                  </div>

                  <div class="cart-item-subtotal">
                    <span>\${{ item.subtotal | number: '1.0-0' }}</span>
                    <button
                      type="button"
                      class="btn-remove"
                      [disabled]="isProcessing()"
                      (click)="removeFromCart(item.product.id)"
                      title="Eliminar producto"
                    >
                      ✕
                    </button>
                  </div>
                </div>
              }
            }
          </div>

          <div class="checkout-section">
            <div class="customer-field">
              <label for="customerRut">RUT Cliente</label>
              <input
                id="customerRut"
                type="text"
                [formControl]="customerRutControl"
                placeholder="Ej: 12.345.678-5"
                [class.invalid]="customerRutControl.invalid && customerRutControl.touched"
              />
              @if (customerRutControl.invalid && customerRutControl.touched) {
                <small class="field-error">
                  @if (customerRutControl.errors?.['required']) {
                    El RUT del cliente es obligatorio.
                  } @else if (customerRutControl.errors?.['invalidRut']) {
                    El RUT ingresado no es válido (Módulo 11).
                  }
                </small>
              }
            </div>

            <div class="totals-box">
              <div class="total-row">
                <span>Subtotal Neto</span>
                <span>\${{ subtotalGeneral() | number: '1.0-0' }}</span>
              </div>
              <div class="total-row">
                <span>IVA (19%)</span>
                <span>\${{ totalIva() | number: '1.0-0' }}</span>
              </div>
              <div class="total-row total-final">
                <span>Total a Pagar</span>
                <span>\${{ totalFinal() | number: '1.0-0' }}</span>
              </div>
            </div>

            <button
              type="button"
              class="btn-checkout"
              [disabled]="cart().length === 0 || isProcessing()"
              (click)="onCheckout()"
            >
              @if (isProcessing()) {
                <span class="spinner" aria-hidden="true"></span>
                <span>Procesando venta...</span>
              } @else {
                <span>Confirmar y Emitir Venta (\${{ totalFinal() | number: '1.0-0' }})</span>
              }
            </button>
          </div>
        </section>
      </div>
    </div>
  `,
  styles: [
    `
      .pos-wrapper {
        max-width: 1280px;
        margin: 0 auto;
        padding: 1.5rem;
        background: #f8fafc;
        min-height: 100vh;
        box-sizing: border-box;
      }
      .pos-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 1.25rem;
      }
      .pos-header h1 {
        margin: 0;
        font-size: 1.65rem;
        color: #0f172a;
      }
      .subtitle {
        margin: 0.25rem 0 0;
        color: #64748b;
        font-size: 0.9rem;
      }
      .badge-items {
        background: #e0e7ff;
        color: #1e40af;
        font-weight: 600;
        padding: 0.45rem 0.85rem;
        border-radius: 9999px;
        font-size: 0.875rem;
      }
      .alert {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 0.85rem 1.1rem;
        border-radius: 8px;
        margin-bottom: 1rem;
        font-size: 0.925rem;
        font-weight: 500;
      }
      .alert-success {
        background: #ecfdf5;
        border: 1px solid #6ee7b7;
        color: #065f46;
      }
      .alert-error {
        background: #fef2f2;
        border: 1px solid #fecaca;
        color: #991b1b;
      }
      .alert-close {
        background: transparent;
        border: none;
        font-size: 1.25rem;
        cursor: pointer;
        color: inherit;
        line-height: 1;
      }
      .pos-grid {
        display: grid;
        grid-template-columns: 1.35fr 1fr;
        gap: 1.5rem;
        align-items: start;
      }
      @media (max-width: 960px) {
        .pos-grid {
          grid-template-columns: 1fr;
        }
      }
      .card {
        background: #ffffff;
        border-radius: 12px;
        box-shadow: 0 4px 16px rgba(15, 23, 42, 0.06);
        padding: 1.25rem;
      }
      .search-header h2,
      .cart-header h2 {
        margin: 0 0 0.85rem;
        font-size: 1.15rem;
        color: #1e293b;
      }
      .search-box {
        position: relative;
        margin-bottom: 1rem;
      }
      .search-box input {
        width: 100%;
        padding: 0.7rem 2.5rem 0.7rem 0.9rem;
        border: 1px solid #cbd5e1;
        border-radius: 8px;
        font-size: 0.95rem;
        box-sizing: border-box;
      }
      .search-box input:focus {
        outline: none;
        border-color: #2563eb;
        box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.12);
      }
      .search-spinner {
        position: absolute;
        right: 0.85rem;
        top: 50%;
        transform: translateY(-50%);
        width: 16px;
        height: 16px;
        border: 2px solid #cbd5e1;
        border-top-color: #2563eb;
        border-radius: 50%;
        animation: spin 0.7s linear infinite;
      }
      .products-list {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
        max-height: 540px;
        overflow-y: auto;
      }
      .product-card {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 0.85rem 1rem;
        border: 1px solid #e2e8f0;
        border-radius: 8px;
        transition: border-color 0.15s;
      }
      .product-card:hover {
        border-color: #94a3b8;
      }
      .product-card.out-of-stock {
        opacity: 0.6;
        background: #f8fafc;
      }
      .product-sku {
        font-size: 0.75rem;
        color: #64748b;
        font-family: monospace;
      }
      .product-name {
        margin: 0.15rem 0;
        font-size: 0.98rem;
        color: #0f172a;
      }
      .product-stock {
        font-size: 0.8rem;
        color: #16a34a;
      }
      .product-stock.low-stock {
        color: #d97706;
        font-weight: 600;
      }
      .product-actions {
        display: flex;
        align-items: center;
        gap: 1rem;
      }
      .product-price {
        font-size: 1.05rem;
        font-weight: 700;
        color: #0f172a;
      }
      .btn-add {
        padding: 0.5rem 0.9rem;
        background: #2563eb;
        color: #fff;
        border: none;
        border-radius: 6px;
        font-weight: 600;
        cursor: pointer;
      }
      .btn-add:disabled {
        background: #94a3b8;
        cursor: not-allowed;
      }
      .cart-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 0.85rem;
      }
      .cart-header h2 {
        margin: 0;
      }
      .btn-clear {
        background: transparent;
        border: 1px solid #ef4444;
        color: #ef4444;
        padding: 0.3rem 0.65rem;
        border-radius: 6px;
        font-size: 0.8rem;
        cursor: pointer;
      }
      .cart-items {
        display: flex;
        flex-direction: column;
        gap: 0.65rem;
        min-height: 200px;
        max-height: 340px;
        overflow-y: auto;
        margin-bottom: 1rem;
      }
      .empty-cart,
      .empty-state {
        text-align: center;
        padding: 2.5rem 1rem;
        color: #64748b;
      }
      .cart-item {
        display: grid;
        grid-template-columns: 1fr auto auto;
        gap: 0.75rem;
        align-items: center;
        padding: 0.65rem 0.75rem;
        background: #f8fafc;
        border-radius: 8px;
      }
      .cart-item-info {
        display: flex;
        flex-direction: column;
      }
      .cart-item-info strong {
        font-size: 0.9rem;
        color: #0f172a;
      }
      .cart-item-info small {
        color: #64748b;
        font-size: 0.78rem;
      }
      .cart-item-controls {
        display: flex;
        align-items: center;
        gap: 0.4rem;
      }
      .qty-btn {
        width: 26px;
        height: 26px;
        border: 1px solid #cbd5e1;
        background: #fff;
        border-radius: 4px;
        cursor: pointer;
        font-weight: 700;
      }
      .qty-btn:disabled {
        opacity: 0.45;
        cursor: not-allowed;
      }
      .qty-value {
        min-width: 22px;
        text-align: center;
        font-weight: 600;
        font-size: 0.9rem;
      }
      .cart-item-subtotal {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        font-weight: 700;
        color: #0f172a;
      }
      .btn-remove {
        background: transparent;
        border: none;
        color: #ef4444;
        cursor: pointer;
        font-size: 0.9rem;
      }
      .checkout-section {
        border-top: 1px solid #e2e8f0;
        padding-top: 1rem;
      }
      .customer-field {
        margin-bottom: 1rem;
      }
      .customer-field label {
        display: block;
        font-size: 0.85rem;
        font-weight: 600;
        color: #334155;
        margin-bottom: 0.3rem;
      }
      .customer-field input {
        width: 100%;
        padding: 0.6rem 0.75rem;
        border: 1px solid #cbd5e1;
        border-radius: 6px;
        box-sizing: border-box;
      }
      .customer-field input.invalid {
        border-color: #ef4444;
      }
      .field-error {
        display: block;
        color: #dc2626;
        font-size: 0.78rem;
        margin-top: 0.25rem;
      }
      .totals-box {
        background: #f1f5f9;
        padding: 0.9rem;
        border-radius: 8px;
        margin-bottom: 1rem;
      }
      .total-row {
        display: flex;
        justify-content: space-between;
        font-size: 0.9rem;
        color: #475569;
        margin-bottom: 0.4rem;
      }
      .total-row.total-final {
        margin-top: 0.5rem;
        padding-top: 0.5rem;
        border-top: 1px solid #cbd5e1;
        font-size: 1.15rem;
        font-weight: 700;
        color: #0f172a;
        margin-bottom: 0;
      }
      .btn-checkout {
        width: 100%;
        padding: 0.85rem 1rem;
        background: #16a34a;
        color: #ffffff;
        border: none;
        border-radius: 8px;
        font-size: 1rem;
        font-weight: 600;
        cursor: pointer;
        display: inline-flex;
        justify-content: center;
        align-items: center;
        gap: 0.5rem;
      }
      .btn-checkout:disabled {
        background: #94a3b8;
        cursor: not-allowed;
      }
      .spinner {
        width: 18px;
        height: 18px;
        border: 2.5px solid rgba(255, 255, 255, 0.35);
        border-top-color: #ffffff;
        border-radius: 50%;
        animation: spin 0.75s linear infinite;
      }
      @keyframes spin {
        to {
          transform: rotate(360deg);
        }
      }
    `
  ]
})
export class SalesComponent {
  private readonly salesService = inject(SalesService);
  private readonly destroyRef = inject(DestroyRef);

  // Controles reactivos
  readonly searchControl = new FormControl('', { nonNullable: true });
  readonly customerRutControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, rutValidator]
  });

  // Signals de estado
  readonly products = signal<ProductDTO[]>([]);
  readonly cart = signal<SaleItem[]>([]);
  readonly isSearching = signal<boolean>(false);
  readonly isProcessing = signal<boolean>(false);
  readonly successMessage = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);

  // Signals computados para totales (IVA 19% Chile)
  readonly subtotalGeneral = computed(() =>
    this.cart().reduce((acc, item) => acc + item.subtotal, 0)
  );
  readonly totalIva = computed(() => Math.round(this.subtotalGeneral() * 0.19));
  readonly totalFinal = computed(() => this.subtotalGeneral() + this.totalIva());
  readonly totalItemsCount = computed(() =>
    this.cart().reduce((acc, item) => acc + item.quantity, 0)
  );

  constructor() {
    this.setupProductSearch();
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

  getQuantityInCart(productId: string): number {
    const found = this.cart().find((item) => item.product.id === productId);
    return found ? found.quantity : 0;
  }

  /**
   * Agrega un producto al carrito validando que no supere el stock disponible.
   */
  addToCart(product: ProductDTO): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);

    if (product.stock <= 0) {
      this.errorMessage.set(`El producto "${product.name}" no tiene stock disponible.`);
      return;
    }

    const currentItems = this.cart();
    const existingIndex = currentItems.findIndex((item) => item.product.id === product.id);

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

  /**
   * Disminuye en 1 la cantidad de un ítem o lo remueve si llega a 0.
   */
  decrementQuantity(productId: string): void {
    const currentItems = this.cart();
    const existingIndex = currentItems.findIndex((item) => item.product.id === productId);
    if (existingIndex === -1) {
      return;
    }

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

  /**
   * Remueve un producto del carrito por su ID.
   */
  removeFromCart(productId: string): void {
    this.cart.update((items) => items.filter((item) => item.product.id !== productId));
  }

  /**
   * Vacía completamente el carrito de compras.
   */
  clearCart(): void {
    this.cart.set([]);
  }

  /**
   * Procesa y envía la venta al backend CQRS evitando envíos duplicados.
   */
  onCheckout(): void {
    if (this.isProcessing() || this.cart().length === 0) {
      return;
    }

    if (this.customerRutControl.invalid) {
      this.customerRutControl.markAsTouched();
      this.errorMessage.set('Por favor ingresa un RUT de cliente válido antes de confirmar la venta.');
      return;
    }

    this.isProcessing.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const command: CreateSaleCommand = {
      items: this.cart(),
      totalAmount: this.totalFinal(),
      customerRut: this.customerRutControl.value.trim()
    };

    this.salesService
      .processSale(command)
      .pipe(finalize(() => this.isProcessing.set(false)))
      .subscribe({
        next: () => {
          this.clearCart();
          this.customerRutControl.reset('');
          this.searchControl.setValue(this.searchControl.value);
          this.successMessage.set('¡Venta registrada y emitida exitosamente!');
        },
        error: (err: Error) => {
          this.errorMessage.set(
            err.message || 'Ocurrió un error al procesar la venta. Intenta nuevamente.'
          );
        }
      });
  }
}
