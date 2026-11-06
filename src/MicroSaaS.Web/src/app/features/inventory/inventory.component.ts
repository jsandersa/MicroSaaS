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
import { ConfigService } from '../../core/services/config.service';
import { InventoryService } from './inventory.service';
import {
  CreateProductCommand,
  KardexMovementDTO,
  ProductInventoryDTO
} from './models/inventory.models';

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
          <h1>Gestión de Inventario y Kardex</h1>
          <p class="subtitle">Control de catálogo, existencias, Precio Medio Ponderado (PMP) y libro Kardex</p>
        </div>

        <div class="header-actions">
          @if (criticalStockCount() > 0) {
            <span class="critical-summary-badge">
              ⚠ {{ criticalStockCount() }} en Stock Crítico
            </span>
          }
          <button
            type="button"
            class="btn-secondary"
            (click)="toggleKardexView()"
          >
            {{ isKardexVisible() ? 'Ocultar Libro Kardex' : '📊 Ver Libro Kardex' }}
          </button>
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

      <!-- Panel Libro Kardex -->
      @if (isKardexVisible()) {
        <section class="form-panel card" aria-label="Libro Mayor Kardex">
          <div class="form-panel-header">
            <h2>📊 Libro Kardex Transaccional (Entradas, Salidas y PMP)</h2>
            <button type="button" class="btn-icon" (click)="isKardexVisible.set(false)" aria-label="Cerrar">✕</button>
          </div>

          @if (isLoadingKardex()) {
            <div class="loading-state">
              <p>Cargando movimientos del Kardex...</p>
            </div>
          } @else if (kardexList().length === 0) {
            <div class="empty-state">
              <p>Aún no se registran movimientos en el libro Kardex.</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="inventory-table">
                <thead>
                  <tr>
                    <th>Fecha / Hora</th>
                    <th>SKU</th>
                    <th>Producto</th>
                    <th class="text-center">Tipo Movimiento</th>
                    <th>Justificativo / Glosa Auditoría</th>
                    <th class="text-center">Cantidad</th>
                    <th class="text-right">Costo Unit. Mov.</th>
                    <th class="text-right">Valor Total</th>
                    <th class="text-right">Costo PMP Actual</th>
                  </tr>
                </thead>
                <tbody>
                  @for (mov of kardexList(); track mov.id) {
                    <tr>
                      <td class="muted-cell">{{ mov.transactionDate | date: 'dd/MM/yyyy HH:mm:ss' }}</td>
                      <td class="sku-cell">{{ mov.productSku }}</td>
                      <td class="name-cell">{{ mov.productName }}</td>
                      <td class="text-center">
                        @if (mov.movementType === 1) {
                          <span class="badge badge-optimal">🟢 {{ mov.movementTypeName }}</span>
                        } @else if (mov.movementType === 2) {
                          <span class="badge badge-critical">🔴 {{ mov.movementTypeName }}</span>
                        } @else {
                          <span class="category-pill">🔵 {{ mov.movementTypeName }}</span>
                        }
                      </td>
                      <td class="muted-cell">{{ mov.reason || '-' }}</td>
                      <td class="text-center stock-number">{{ mov.quantity }}</td>
                      <td class="text-right">\${{ mov.unitCost | number: '1.0-0' }}</td>
                      <td class="text-right price-cell">\${{ mov.totalValue | number: '1.0-0' }}</td>
                      <td class="text-right">\${{ mov.currentPmpCost | number: '1.0-0' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </section>
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
                  (focus)="$any($event.target).select()"
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
                  (focus)="$any($event.target).select()"
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
                  (focus)="$any($event.target).select()"
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

      <!-- Panel de Edición Rápida de Precio Venta y Ajuste Auditado de Stock (Justo encima de la tabla) -->
      @if (editingProduct()) {
        <section id="edit-product-panel" class="form-panel card" aria-label="Editar Precio y Ajuste Auditado de Stock">
          <div class="form-panel-header">
            <h2>
              {{ isStockAdjustmentMode() ? '⚖️ Ajuste Auditado de Stock (Kardex):' : '✏️ Editar Precio de Venta POS:' }}
              {{ editingProduct()!.name }} ({{ editingProduct()!.sku }})
            </h2>
            <button type="button" class="btn-icon" (click)="cancelEditProduct()" aria-label="Cerrar">✕</button>
          </div>

          <div class="form-grid">
            <div class="form-field">
              <label for="editPmpCost">🔒 Costo PMP Contable (Solo Lectura)</label>
              <input
                id="editPmpCost"
                type="text"
                readonly
                style="background:#f1f5f9;color:#475569;cursor:not-allowed;font-weight:700;"
                [value]="'$' + (editingProduct()!.pmpCost | number: '1.0-0') + ' (Calculado por Kardex)'"
              />
            </div>

            <div class="form-field">
              <label for="editSalePrice">Precio de Venta POS (CLP)</label>
              <input
                id="editSalePrice"
                type="number"
                min="1"
                (focus)="$any($event.target).select()"
                [formControl]="editSalePriceControl"
              />
              @if (suggestedPriceHint()) {
                <small style="color:#16a34a;font-weight:600;margin-top:0.25rem;">{{ suggestedPriceHint() }}</small>
              }
            </div>

            <div class="form-field">
              <label for="editCurrentStock">
                {{ isStockAdjustmentMode() ? 'Nuevo Stock Físico Contado (Actual: ' + editingProduct()!.currentStock + ' u.)' : '🔒 Stock Actual en Kardex (Protegido)' }}
              </label>
              @if (isStockAdjustmentMode()) {
                <input
                  id="editCurrentStock"
                  type="number"
                  min="0"
                  (focus)="$any($event.target).select()"
                  [formControl]="editStockControl"
                />
              } @else {
                <div style="display:flex;gap:0.5rem;align-items:center;">
                  <input
                    type="text"
                    readonly
                    style="background:#f1f5f9;color:#475569;cursor:not-allowed;font-weight:700;flex:1;"
                    [value]="editingProduct()!.currentStock + ' unidades'"
                  />
                  <button
                    type="button"
                    class="btn-secondary"
                    style="white-space:nowrap;padding:0.6rem 0.85rem;font-size:0.82rem;"
                    (click)="enableStockAdjustmentMode()"
                  >
                    ⚖️ Ajustar por Merma / Conteo
                  </button>
                </div>
              }
            </div>
          </div>

          <!-- Bloque obligatorio de Justificativo Kardex cuando se activa el modo de ajuste de stock -->
          @if (isStockAdjustmentMode()) {
            <div style="margin-top:1rem;padding:1rem;background:#fffbeb;border:1px solid #fde68a;border-radius:8px;">
              <p style="margin:0 0 0.75rem;font-size:0.88rem;font-weight:700;color:#92400e;">
                ⚠ Ajuste Auditado de Inventario ({{ editingProduct()!.currentStock }} u. → {{ editStockControl.value }} u., diferencia: {{ editStockControl.value - editingProduct()!.currentStock > 0 ? '+' : '' }}{{ editStockControl.value - editingProduct()!.currentStock }} u.). Ingresa la causal y glosa obligatoria para el Libro Kardex:
              </p>
              <div class="form-grid">
                <div class="form-field">
                  <label for="editReasonCategory">Causal Tributaria / Contable del Ajuste *</label>
                  <select
                    id="editReasonCategory"
                    [formControl]="editReasonCategoryControl"
                    style="padding:0.65rem 0.8rem;border:1px solid #cbd5e1;border-radius:6px;font-size:0.925rem;background:#fff;"
                  >
                    <option value="Diferencia en Conteo Físico (Toma de Inventario)">Diferencia en Conteo Físico (Toma de Inventario)</option>
                    <option value="Merma / Daño o Rotura en Bodega">Merma / Daño o Rotura en Bodega</option>
                    <option value="Vencimiento / Caducidad de Producto">Vencimiento / Caducidad de Producto</option>
                    <option value="Extravío / Hurto">Extravío / Hurto</option>
                    <option value="Muestra Comercial / Consumo Interno">Muestra Comercial / Consumo Interno</option>
                  </select>
                </div>

                <div class="form-field">
                  <label for="editReasonDetail">Glosa / Documento o Motivo de Respaldo (Mín. 5 caracteres) *</label>
                  <input
                    id="editReasonDetail"
                    type="text"
                    placeholder="Ej: Acta de conteo físico mensual #04 / Envase roto en recepción"
                    [formControl]="editReasonDetailControl"
                  />
                </div>
              </div>
            </div>
          }

          <div class="form-actions">
            <button
              type="button"
              class="btn-secondary"
              (click)="applySuggestedMargin()"
            >
              ⚡ Sugerir Precio Venta (+{{ defaultMarginPercentage() }}% sobre PMP)
            </button>
            <button
              type="button"
              class="btn-secondary"
              [disabled]="isSubmitting()"
              (click)="cancelEditProduct()"
            >
              Cancelar
            </button>
            <button
              type="button"
              class="btn-submit"
              [disabled]="isSubmitting() || !canSaveEditProduct()"
              (click)="saveEditProduct()"
            >
              {{ isSubmitting() ? 'Guardando...' : 'Guardar Cambios' }}
            </button>
          </div>
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
                  <th class="text-right">Costo PMP</th>
                  <th class="text-right">Precio Venta</th>
                  <th class="text-center">Stock Actual</th>
                  <th class="text-center">Estado de Stock</th>
                  <th class="text-center">Acción</th>
                </tr>
              </thead>
              <tbody>
                @for (item of inventoryList(); track item.id) {
                  <tr [class.row-critical]="isCriticalStock(item) || item.price <= item.pmpCost">
                    <td class="sku-cell">{{ item.sku }}</td>
                    <td class="name-cell">{{ item.name }}</td>
                    <td>
                      <span class="category-pill">{{ item.category }}</span>
                    </td>
                    <td class="text-right muted-cell">\${{ item.pmpCost | number: '1.0-0' }}</td>
                    <td class="text-right price-cell" [class.stock-critical-text]="item.price <= item.pmpCost">
                      \${{ item.price | number: '1.0-0' }}
                      @if (item.price <= item.pmpCost) {
                        <small style="display:block;font-size:0.72rem;color:#dc2626;">⚠ Bajo PMP</small>
                      }
                    </td>
                    <td class="text-center">
                      <span
                        class="stock-number"
                        [class.stock-critical-text]="isCriticalStock(item)"
                      >
                        {{ item.currentStock }}
                      </span>
                    </td>
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
                    <td class="text-center" style="display:flex;gap:0.4rem;justify-content:center;flex-wrap:wrap;">
                      <button
                        type="button"
                        class="btn-secondary"
                        style="padding:0.35rem 0.7rem;font-size:0.8rem;"
                        (click)="startEditProduct(item, false)"
                      >
                        ✏️ Editar Precio
                      </button>
                      <button
                        type="button"
                        class="btn-secondary"
                        style="padding:0.35rem 0.7rem;font-size:0.8rem;"
                        (click)="startEditProduct(item, true)"
                      >
                        ⚖️ Ajustar Stock
                      </button>
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
  private readonly configService = inject(ConfigService);
  private readonly signalRService = inject(SignalRService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(NonNullableFormBuilder);

  // Signals de gestión de estado
  readonly inventoryList = signal<ProductInventoryDTO[]>([]);
  readonly defaultMarginPercentage = signal<number>(30);
  readonly isLoading = signal<boolean>(true);
  readonly isFormVisible = signal<boolean>(false);
  readonly isSubmitting = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  // Signals de Edición Rápida de Precio y Ajuste Auditado de Stock
  readonly editingProduct = signal<ProductInventoryDTO | null>(null);
  readonly isStockAdjustmentMode = signal<boolean>(false);
  readonly suggestedPriceHint = signal<string | null>(null);
  readonly editCostControl = new FormControl<number>(0, { nonNullable: true });
  readonly editSalePriceControl = new FormControl<number>(0, { nonNullable: true });
  readonly editStockControl = new FormControl<number>(0, { nonNullable: true });
  readonly editReasonCategoryControl = new FormControl<string>(
    'Diferencia en Conteo Físico (Toma de Inventario)',
    { nonNullable: true }
  );
  readonly editReasonDetailControl = new FormControl<string>('', { nonNullable: true });

  // Signals de Libro Kardex
  readonly isKardexVisible = signal<boolean>(false);
  readonly isLoadingKardex = signal<boolean>(false);
  readonly kardexList = signal<KardexMovementDTO[]>([]);

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
    this.loadTenantMargin();
    this.loadInventory();
    this.setupRealtimeStockUpdates();
  }

  private loadTenantMargin(): void {
    this.configService.getConfiguration().subscribe({
      next: (config) => {
        if (config && typeof config.defaultMarginPercentage === 'number' && config.defaultMarginPercentage >= 0) {
          this.defaultMarginPercentage.set(config.defaultMarginPercentage);
        }
      },
      error: () => {
        // Fallback silencioso a 30%
      }
    });
  }

  startEditProduct(product: ProductInventoryDTO, adjustStockMode = false): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.suggestedPriceHint.set(null);
    this.editingProduct.set(product);
    this.isStockAdjustmentMode.set(adjustStockMode);
    this.editCostControl.setValue(product.pmpCost);
    const factor = 1 + this.defaultMarginPercentage() / 100;
    this.editSalePriceControl.setValue(
      product.price <= product.pmpCost ? Math.round(product.pmpCost * factor) : product.price
    );
    this.editStockControl.setValue(product.currentStock);
    this.editReasonCategoryControl.setValue('Diferencia en Conteo Físico (Toma de Inventario)');
    this.editReasonDetailControl.setValue('');

    setTimeout(() => {
      const panel = document.getElementById('edit-product-panel');
      panel?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      const inputId = adjustStockMode ? 'editCurrentStock' : 'editSalePrice';
      const inputEl = document.getElementById(inputId) as HTMLInputElement | null;
      inputEl?.focus();
      inputEl?.select();
    }, 50);
  }

  enableStockAdjustmentMode(): void {
    this.isStockAdjustmentMode.set(true);
    setTimeout(() => {
      const stockInput = document.getElementById('editCurrentStock') as HTMLInputElement | null;
      stockInput?.focus();
      stockInput?.select();
    }, 50);
  }

  canSaveEditProduct(): boolean {
    const target = this.editingProduct();
    if (!target) return false;
    const newStock = Number(this.editStockControl.value);
    if (newStock !== target.currentStock) {
      return (this.editReasonDetailControl.value ?? '').trim().length >= 5;
    }
    return true;
  }

  applySuggestedMargin(): void {
    const target = this.editingProduct();
    const pmp = target ? target.pmpCost : Number(this.editCostControl.value) || 0;
    const factor = 1 + this.defaultMarginPercentage() / 100;
    const suggested = Math.round(pmp * factor);
    this.editSalePriceControl.setValue(suggested);
    this.suggestedPriceHint.set(
      `✓ Precio sugerido aplicado: $${suggested.toLocaleString('es-CL')} (PMP $${Math.round(pmp).toLocaleString('es-CL')} + ${this.defaultMarginPercentage()}%)`
    );
  }

  cancelEditProduct(): void {
    this.editingProduct.set(null);
  }

  saveEditProduct(): void {
    const target = this.editingProduct();
    if (!target || this.isSubmitting()) return;

    const newSalePrice = Math.max(1, Number(this.editSalePriceControl.value) || 1);
    const newStock = Math.max(0, Number(this.editStockControl.value) || 0);
    const stockChanged = newStock !== target.currentStock;

    if (stockChanged && !this.canSaveEditProduct()) {
      this.errorMessage.set(
        'Debes ingresar una glosa o motivo de respaldo (mínimo 5 caracteres) para justificar el ajuste de stock en el Libro Kardex.'
      );
      return;
    }

    const fullReason = stockChanged
      ? `${this.editReasonCategoryControl.value}: ${this.editReasonDetailControl.value.trim()}`
      : null;

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    this.inventoryService
      .updateProduct(target.id, {
        sku: target.sku,
        name: target.name,
        salePrice: newSalePrice,
        costPrice: target.pmpCost,
        currentStock: newStock,
        adjustmentReason: fullReason
      })
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: () => {
          this.inventoryList.update((list) =>
            list.map((p) =>
              p.id === target.id
                ? { ...p, price: newSalePrice, currentStock: newStock }
                : p
            )
          );
          this.editingProduct.set(null);
          this.successMessage.set(
            stockChanged
              ? `Precio actualizado y ajuste de stock (${target.currentStock} → ${newStock} u.) registrado con su justificativo en el Libro Kardex.`
              : `Precio de venta de "${target.name}" actualizado a $${newSalePrice.toLocaleString('es-CL')}.`
          );
          if (this.isKardexVisible() || stockChanged) {
            this.isKardexVisible.set(true);
            this.loadKardex();
          }
        },
        error: (err: Error) => {
          this.errorMessage.set(
            err.message || 'No se pudo actualizar el producto.'
          );
        }
      });
  }

  toggleKardexView(): void {
    const nextState = !this.isKardexVisible();
    this.isKardexVisible.set(nextState);
    if (nextState) {
      this.loadKardex();
    }
  }

  loadKardex(): void {
    this.isLoadingKardex.set(true);
    this.inventoryService
      .getKardexMovements()
      .pipe(finalize(() => this.isLoadingKardex.set(false)))
      .subscribe({
        next: (movements) => this.kardexList.set(movements ?? []),
        error: () => this.kardexList.set([])
      });
  }

  private setupRealtimeStockUpdates(): void {
    void this.signalRService.startConnection();

    this.signalRService.onStockUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        if (!event || !event.productId) return;

        const targetId = event.productId.toString().trim().toLowerCase();

        this.inventoryList.update((current) =>
          current.map((p) =>
            p.id && p.id.toString().trim().toLowerCase() === targetId
              ? { ...p, currentStock: event.newStock }
              : p
          )
        );

        if (this.isKardexVisible()) {
          this.loadKardex();
        }
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
            pmpCost: Math.round(command.price * 0.7),
            category: command.category
          };

          this.inventoryList.update((current) => [newProduct, ...current]);
          this.isFormVisible.set(false);
          this.resetForm();
          this.successMessage.set(`Producto "${newProduct.name}" agregado exitosamente al catálogo.`);
          if (this.isKardexVisible()) {
            this.loadKardex();
          }
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
