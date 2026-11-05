import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { finalize } from 'rxjs';
import { SignalRService } from '../../core/realtime/signalr.service';
import { InventoryService } from './inventory.service';
import { CreateProductCommand, ProductInventoryDTO } from './models/inventory.models';

type CreateProductFormControls = {
  [K in keyof CreateProductCommand]: FormControl<CreateProductCommand[K]>;
};

@Component({
  selector: 'app-inventory',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="inventory-wrapper">
      <!-- Cabecera Principal -->
      <header class="inventory-header">
        <div>
          <h1>Gestión de Inventario</h1>
          <p class="subtitle">Control de catálogo, existencias y alertas de stock crítico</p>
        </div>

        <div class="header-actions">
          @if (criticalStockCount() > 0) {
            <span class="critical-summary-badge">
              ⚠ {{ criticalStockCount() }} en Stock Crítico
            </span>
          }
          <button
            type="button"
            class="btn-primary"
            (click)="toggleForm()"
          >
            {{ isFormVisible() ? 'Cerrar Formulario' : '+ Nuevo Producto' }}
          </button>
        </div>
      </header>

      @if (errorMessage()) {
        <div class="alert alert-error" role="alert">
          <span>{{ errorMessage() }}</span>
          <button type="button" class="alert-close" (click)="errorMessage.set(null)">×</button>
        </div>
      }

      @if (successMessage()) {
        <div class="alert alert-success" role="status">
          <span>{{ successMessage() }}</span>
          <button type="button" class="alert-close" (click)="successMessage.set(null)">×</button>
        </div>
      }

      <!-- Panel Expandible / Formulario Reactivo -->
      @if (isFormVisible()) {
        <section class="form-panel card" aria-label="Formulario de Nuevo Producto">
          <div class="form-panel-header">
            <h2>Registrar Nuevo Producto</h2>
            <button type="button" class="btn-icon" (click)="closeForm()" aria-label="Cerrar">✕</button>
          </div>

          <form [formGroup]="productForm" (ngSubmit)="onSubmit()" novalidate class="product-form">
            <div class="form-grid">
              <div class="form-field">
                <label for="sku">Código SKU</label>
                <input
                  id="sku"
                  type="text"
                  formControlName="sku"
                  placeholder="Ej: SKU-1001"
                  [class.invalid]="isInvalid('sku')"
                />
                @if (isInvalid('sku')) {
                  <small class="error-text">El código SKU es obligatorio (máx. 50 caracteres).</small>
                }
              </div>

              <div class="form-field">
                <label for="name">Nombre del Producto</label>
                <input
                  id="name"
                  type="text"
                  formControlName="name"
                  placeholder="Ej: Monitor LED 27 pulgadas"
                  [class.invalid]="isInvalid('name')"
                />
                @if (isInvalid('name')) {
                  <small class="error-text">El nombre del producto es obligatorio.</small>
                }
              </div>

              <div class="form-field">
                <label for="category">Categoría</label>
                <input
                  id="category"
                  type="text"
                  formControlName="category"
                  placeholder="Ej: Electrónica, Insumos, Servicios"
                  [class.invalid]="isInvalid('category')"
                />
                @if (isInvalid('category')) {
                  <small class="error-text">La categoría es obligatoria.</small>
                }
              </div>

              <div class="form-field">
                <label for="price">Precio de Venta (CLP)</label>
                <input
                  id="price"
                  type="number"
                  formControlName="price"
                  min="1"
                  [class.invalid]="isInvalid('price')"
                />
                @if (isInvalid('price')) {
                  <small class="error-text">Ingresa un precio mayor a 0.</small>
                }
              </div>

              <div class="form-field">
                <label for="initialStock">Stock Inicial</label>
                <input
                  id="initialStock"
                  type="number"
                  formControlName="initialStock"
                  min="0"
                  [class.invalid]="isInvalid('initialStock')"
                />
                @if (isInvalid('initialStock')) {
                  <small class="error-text">El stock inicial no puede ser negativo.</small>
                }
              </div>

              <div class="form-field">
                <label for="minimumStock">Stock Mínimo (Alerta)</label>
                <input
                  id="minimumStock"
                  type="number"
                  formControlName="minimumStock"
                  min="0"
                  [class.invalid]="isInvalid('minimumStock')"
                />
                @if (isInvalid('minimumStock')) {
                  <small class="error-text">El stock mínimo no puede ser negativo.</small>
                }
              </div>
            </div>

            <div class="form-actions">
              <button
                type="button"
                class="btn-secondary"
                [disabled]="isSubmitting()"
                (click)="closeForm()"
              >
                Cancelar
              </button>
              <button
                type="submit"
                class="btn-submit"
                [disabled]="productForm.invalid || isSubmitting()"
              >
                @if (isSubmitting()) {
                  <span class="spinner" aria-hidden="true"></span>
                  <span>Guardando...</span>
                } @else {
                  <span>Guardar Producto</span>
                }
              </button>
            </div>
          </form>
        </section>
      }

      <!-- Tabla de Catálogo de Inventario -->
      <section class="table-card card">
        @if (isLoading()) {
          <div class="loading-state">
            <span class="spinner spinner-dark" aria-hidden="true"></span>
            <p>Cargando catálogo de inventario...</p>
          </div>
        } @else if (inventoryList().length === 0) {
          <div class="empty-state">
            <p>Aún no hay productos registrados en el inventario.</p>
            <button type="button" class="btn-primary" (click)="openForm()">
              Registrar primer producto
            </button>
          </div>
        } @else {
          <div class="table-responsive">
            <table class="inventory-table">
              <thead>
                <tr>
                  <th>SKU</th>
                  <th>Producto</th>
                  <th>Categoría</th>
                  <th class="text-right">Precio Venta</th>
                  <th class="text-center">Stock Actual</th>
                  <th class="text-center">Stock Mínimo</th>
                  <th class="text-center">Estado de Stock</th>
                </tr>
              </thead>
              <tbody>
                @for (item of inventoryList(); track item.id) {
                  <tr [class.row-critical]="isCriticalStock(item)">
                    <td class="sku-cell">{{ item.sku }}</td>
                    <td class="name-cell">{{ item.name }}</td>
                    <td>
                      <span class="category-pill">{{ item.category }}</span>
                    </td>
                    <td class="text-right price-cell">\${{ item.price | number: '1.0-0' }}</td>
                    <td class="text-center">
                      <span
                        class="stock-number"
                        [class.stock-critical-text]="isCriticalStock(item)"
                      >
                        {{ item.currentStock }}
                      </span>
                    </td>
                    <td class="text-center muted-cell">{{ item.minimumStock }}</td>
                    <td class="text-center">
                      @if (isCriticalStock(item)) {
                        <span class="badge badge-critical" role="status">
                          ⚠ Stock Crítico
                        </span>
                      } @else {
                        <span class="badge badge-optimal">
                          ✓ Stock Óptimo
                        </span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </section>
    </div>
  `,
  styles: [
    `
      .inventory-wrapper {
        max-width: 1240px;
        margin: 0 auto;
        padding: 1.5rem;
        background: #f8fafc;
        min-height: 100vh;
        box-sizing: border-box;
      }
      .inventory-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        flex-wrap: wrap;
        gap: 1rem;
        margin-bottom: 1.5rem;
      }
      .inventory-header h1 {
        margin: 0;
        font-size: 1.65rem;
        color: #0f172a;
      }
      .subtitle {
        margin: 0.25rem 0 0;
        color: #64748b;
        font-size: 0.92rem;
      }
      .header-actions {
        display: flex;
        align-items: center;
        gap: 0.85rem;
      }
      .critical-summary-badge {
        background: #fef2f2;
        color: #dc2626;
        border: 1px solid #fecaca;
        padding: 0.45rem 0.85rem;
        border-radius: 9999px;
        font-size: 0.85rem;
        font-weight: 600;
      }
      .btn-primary {
        background: #2563eb;
        color: #ffffff;
        border: none;
        padding: 0.65rem 1.15rem;
        border-radius: 8px;
        font-size: 0.925rem;
        font-weight: 600;
        cursor: pointer;
        transition: background 0.15s;
      }
      .btn-primary:hover {
        background: #1d4ed8;
      }
      .alert {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 0.85rem 1.1rem;
        border-radius: 8px;
        margin-bottom: 1rem;
        font-size: 0.92rem;
        font-weight: 500;
      }
      .alert-error {
        background: #fef2f2;
        border: 1px solid #fecaca;
        color: #991b1b;
      }
      .alert-success {
        background: #ecfdf5;
        border: 1px solid #6ee7b7;
        color: #065f46;
      }
      .alert-close {
        background: transparent;
        border: none;
        font-size: 1.25rem;
        cursor: pointer;
        color: inherit;
      }
      .card {
        background: #ffffff;
        border-radius: 12px;
        box-shadow: 0 4px 16px rgba(15, 23, 42, 0.06);
        padding: 1.35rem;
      }
      .form-panel {
        margin-bottom: 1.5rem;
        border-left: 4px solid #2563eb;
      }
      .form-panel-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 1.1rem;
      }
      .form-panel-header h2 {
        margin: 0;
        font-size: 1.15rem;
        color: #1e293b;
      }
      .btn-icon {
        background: transparent;
        border: none;
        font-size: 1.1rem;
        color: #64748b;
        cursor: pointer;
      }
      .form-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
        gap: 1rem;
      }
      .form-field {
        display: flex;
        flex-direction: column;
      }
      .form-field label {
        font-size: 0.85rem;
        font-weight: 600;
        color: #334155;
        margin-bottom: 0.35rem;
      }
      .form-field input {
        padding: 0.65rem 0.8rem;
        border: 1px solid #cbd5e1;
        border-radius: 6px;
        font-size: 0.925rem;
      }
      .form-field input.invalid {
        border-color: #dc2626;
        background: #fef2f2;
      }
      .error-text {
        color: #dc2626;
        font-size: 0.78rem;
        margin-top: 0.25rem;
      }
      .form-actions {
        display: flex;
        justify-content: flex-end;
        gap: 0.75rem;
        margin-top: 1.25rem;
      }
      .btn-secondary {
        background: #f1f5f9;
        color: #334155;
        border: 1px solid #cbd5e1;
        padding: 0.6rem 1.1rem;
        border-radius: 6px;
        font-weight: 600;
        cursor: pointer;
      }
      .btn-submit {
        background: #16a34a;
        color: #ffffff;
        border: none;
        padding: 0.6rem 1.25rem;
        border-radius: 6px;
        font-weight: 600;
        cursor: pointer;
        display: inline-flex;
        align-items: center;
        gap: 0.5rem;
      }
      .btn-submit:disabled {
        background: #94a3b8;
        cursor: not-allowed;
      }
      .loading-state,
      .empty-state {
        text-align: center;
        padding: 3rem 1rem;
        color: #64748b;
      }
      .table-responsive {
        overflow-x: auto;
      }
      .inventory-table {
        width: 100%;
        border-collapse: collapse;
        font-size: 0.925rem;
      }
      .inventory-table th {
        text-align: left;
        padding: 0.85rem 1rem;
        background: #f8fafc;
        color: #475569;
        font-weight: 600;
        border-bottom: 2px solid #e2e8f0;
      }
      .inventory-table td {
        padding: 0.85rem 1rem;
        border-bottom: 1px solid #e2e8f0;
        color: #1e293b;
      }
      .inventory-table tr.row-critical {
        background: #fff5f5;
      }
      .text-right {
        text-align: right;
      }
      .text-center {
        text-align: center;
      }
      .sku-cell {
        font-family: monospace;
        color: #475569;
        font-weight: 600;
      }
      .name-cell {
        font-weight: 600;
        color: #0f172a;
      }
      .category-pill {
        background: #f1f5f9;
        color: #334155;
        padding: 0.25rem 0.6rem;
        border-radius: 6px;
        font-size: 0.8rem;
      }
      .price-cell {
        font-weight: 700;
      }
      .stock-number {
        font-weight: 700;
        font-size: 1rem;
        color: #15803d;
      }
      .stock-critical-text {
        color: #dc2626;
      }
      .muted-cell {
        color: #64748b;
      }
      .badge {
        display: inline-block;
        padding: 0.3rem 0.7rem;
        border-radius: 9999px;
        font-size: 0.78rem;
        font-weight: 700;
      }
      .badge-critical {
        background: #fee2e2;
        color: #b91c1c;
        border: 1px solid #fca5a5;
      }
      .badge-optimal {
        background: #dcfce7;
        color: #166534;
      }
      .spinner {
        width: 16px;
        height: 16px;
        border: 2.5px solid rgba(255, 255, 255, 0.35);
        border-top-color: #ffffff;
        border-radius: 50%;
        display: inline-block;
        animation: spin 0.75s linear infinite;
      }
      .spinner-dark {
        width: 28px;
        height: 28px;
        border-color: #cbd5e1;
        border-top-color: #2563eb;
        margin-bottom: 0.5rem;
      }
      @keyframes spin {
        to {
          transform: rotate(360deg);
        }
      }
    `
  ]
})
export class InventoryComponent implements OnInit {
  private readonly inventoryService = inject(InventoryService);
  private readonly signalRService = inject(SignalRService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(NonNullableFormBuilder);

  // Signals de gestión de estado
  readonly inventoryList = signal<ProductInventoryDTO[]>([]);
  readonly isLoading = signal<boolean>(true);
  readonly isFormVisible = signal<boolean>(false);
  readonly isSubmitting = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  // Signal computado para conteo de productos en stock crítico
  readonly criticalStockCount = computed(
    () => this.inventoryList().filter((p) => p.currentStock <= p.minimumStock).length
  );

  // Formulario Reactivo fuertemente tipado para CreateProductCommand
  readonly productForm: FormGroup<CreateProductFormControls> = this.fb.group({
    sku: ['', [Validators.required, Validators.maxLength(50)]],
    name: ['', [Validators.required, Validators.maxLength(150)]],
    initialStock: [0, [Validators.required, Validators.min(0)]],
    minimumStock: [5, [Validators.required, Validators.min(0)]],
    price: [1000, [Validators.required, Validators.min(1)]],
    category: ['General', [Validators.required, Validators.maxLength(80)]]
  });

  ngOnInit(): void {
    this.loadInventory();
    this.setupRealtimeStockUpdates();
  }

  private setupRealtimeStockUpdates(): void {
    void this.signalRService.startConnection();

    this.signalRService.onStockUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        if (!event || !event.productId) return;

        const targetId = event.productId.toString().trim().toLowerCase();
        console.log('📦 [InventoryComponent] Recibido update de stock para ID:', targetId, 'Nuevo stock:', event.newStock);

        this.inventoryList.update((current) =>
          current.map((p) =>
            (p.id && p.id.toString().trim().toLowerCase() === targetId)
              ? { ...p, currentStock: event.newStock }
              : p
          )
        );
      });
  }

  loadInventory(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.inventoryService
      .getInventory()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (products) => {
          this.inventoryList.set(products);
        },
        error: (err: Error) => {
          this.errorMessage.set(
            err.message || 'No se pudo cargar el inventario desde el servidor.'
          );
        }
      });
  }

  /**
   * Determina si un producto se encuentra en nivel de Stock Crítico.
   */
  isCriticalStock(product: ProductInventoryDTO): boolean {
    return product.currentStock <= product.minimumStock;
  }

  toggleForm(): void {
    this.isFormVisible.update((visible) => !visible);
    this.errorMessage.set(null);
  }

  openForm(): void {
    this.isFormVisible.set(true);
    this.errorMessage.set(null);
  }

  closeForm(): void {
    this.isFormVisible.set(false);
    this.resetForm();
  }

  isInvalid(controlName: keyof CreateProductCommand): boolean {
    const control = this.productForm.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  onSubmit(): void {
    if (this.productForm.invalid || this.isSubmitting()) {
      this.productForm.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const command: CreateProductCommand = this.productForm.getRawValue();

    this.inventoryService
      .createProduct(command)
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (response) => {
          const createdId =
            response && 'id' in response && response.id
              ? response.id
              : crypto.randomUUID();

          const newProduct: ProductInventoryDTO = {
            id: createdId,
            sku: command.sku,
            name: command.name,
            currentStock: command.initialStock,
            minimumStock: command.minimumStock,
            price: command.price,
            category: command.category
          };

          // Actualización reactiva inmediata del signal inventoryList sin recargar la página
          this.inventoryList.update((current) => [newProduct, ...current]);
          this.isFormVisible.set(false);
          this.resetForm();
          this.successMessage.set(`Producto "${newProduct.name}" agregado exitosamente al catálogo.`);
        },
        error: (err: Error) => {
          this.errorMessage.set(
            err.message || 'Ocurrió un error al registrar el producto en el inventario.'
          );
        }
      });
  }

  private resetForm(): void {
    this.productForm.reset({
      sku: '',
      name: '',
      initialStock: 0,
      minimumStock: 5,
      price: 1000,
      category: 'General'
    });
  }
}