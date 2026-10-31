import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ProductItem, ProductService } from '../../core/services/product.service';

@Component({
  selector: 'app-product-list',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule
  ],
  template: `
    <div class="inventory-layout">
      <mat-card class="form-card">
        <mat-card-header>
          <mat-card-title>{{ editingProductId() ? 'Editar Producto' : 'Nuevo Producto' }}</mat-card-title>
          <mat-card-subtitle>Registro en el esquema privado de tu Pyme</mat-card-subtitle>
        </mat-card-header>

        <mat-card-content>
          <form [formGroup]="productForm" (ngSubmit)="onSaveProduct()" class="product-form">
            <mat-form-field appearance="outline">
              <mat-label>SKU</mat-label>
              <input matInput formControlName="sku" placeholder="PROD-001" />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Nombre</mat-label>
              <input matInput formControlName="name" placeholder="Teclado Mecánico" />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Precio</mat-label>
              <input matInput type="number" formControlName="price" />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Stock</mat-label>
              <input matInput type="number" formControlName="stock" />
            </mat-form-field>

            <div class="form-actions">
              <button mat-flat-button color="primary" type="submit" [disabled]="productForm.invalid || isSaving()">
                {{ isSaving() ? 'Guardando...' : (editingProductId() ? 'Actualizar Producto' : 'Registrar Producto') }}
              </button>

              @if (editingProductId()) {
                <button mat-stroked-button type="button" (click)="onCancelEdit()" [disabled]="isSaving()">
                  Cancelar
                </button>
              }
            </div>
          </form>
        </mat-card-content>
      </mat-card>

      <mat-card class="table-card">
        <mat-card-header>
          <mat-card-title>Inventario Actual</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <table mat-table [dataSource]="products()" class="full-width-table">
            <ng-container matColumnDef="sku">
              <th mat-header-cell *matHeaderCellDef>SKU</th>
              <td mat-cell *matCellDef="let product">{{ product.sku }}</td>
            </ng-container>

            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>Nombre</th>
              <td mat-cell *matCellDef="let product">{{ product.name }}</td>
            </ng-container>

            <ng-container matColumnDef="price">
              <th mat-header-cell *matHeaderCellDef>Precio</th>
              <td mat-cell *matCellDef="let product">{{ product.price | currency:'CLP':'symbol-narrow':'1.0-0' }}</td>
            </ng-container>

            <ng-container matColumnDef="stock">
              <th mat-header-cell *matHeaderCellDef>Stock</th>
              <td mat-cell *matCellDef="let product">{{ product.stock }}</td>
            </ng-container>

            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef class="actions-header">Acciones</th>
              <td mat-cell *matCellDef="let product" class="actions-cell">
                <button
                  mat-icon-button
                  color="primary"
                  type="button"
                  title="Editar producto"
                  (click)="onEditProduct(product)">
                  <mat-icon>edit</mat-icon>
                </button>
                <button
                  mat-icon-button
                  color="warn"
                  type="button"
                  title="Eliminar producto"
                  (click)="onDeleteProduct(product)">
                  <mat-icon>delete</mat-icon>
                </button>
              </td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
          </table>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .inventory-layout {
      max-width: 1100px;
      margin: 24px auto;
      padding: 0 24px;
      display: grid;
      gap: 24px;
    }
    .product-form {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 16px;
      align-items: baseline;
      margin-top: 16px;
    }
    .form-actions {
      display: flex;
      gap: 8px;
      align-items: center;
    }
    .full-width-table {
      width: 100%;
    }
    .actions-header,
    .actions-cell {
      text-align: right;
      white-space: nowrap;
    }
  `]
})
export class ProductListComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly productService = inject(ProductService);

  readonly displayedColumns = ['sku', 'name', 'price', 'stock', 'actions'];
  readonly products = signal<ProductItem[]>([]);
  readonly isSaving = signal(false);
  readonly editingProductId = signal<string | null>(null);

  readonly productForm = this.fb.nonNullable.group({
    sku: ['', [Validators.required, Validators.maxLength(50)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    price: [0, [Validators.required, Validators.min(0)]],
    stock: [0, [Validators.required, Validators.min(0)]]
  });

  ngOnInit(): void {
    this.loadProducts();
  }

  loadProducts(): void {
    this.productService.getProducts().subscribe({
      next: (items) => this.products.set(items),
      error: (err) => console.error('Error al cargar productos del inventario:', err)
    });
  }

  onSaveProduct(): void {
    if (this.productForm.invalid) {
      return;
    }

    const payload = this.productForm.getRawValue();
    const currentEditId = this.editingProductId();
    this.isSaving.set(true);

    if (currentEditId) {
      this.productService.updateProduct(currentEditId, payload).subscribe({
        next: () => {
          this.isSaving.set(false);
          this.onCancelEdit();
          this.loadProducts();
        },
        error: () => {
          this.isSaving.set(false);
        }
      });
      return;
    }

    this.productService.createProduct(payload).subscribe({
      next: () => {
        this.productForm.reset({ sku: '', name: '', price: 0, stock: 0 });
        this.isSaving.set(false);
        this.loadProducts();
      },
      error: () => {
        this.isSaving.set(false);
      }
    });
  }

  onEditProduct(product: ProductItem): void {
    this.editingProductId.set(product.id);
    this.productForm.setValue({
      sku: product.sku,
      name: product.name,
      price: product.price,
      stock: product.stock
    });
  }

  onCancelEdit(): void {
    this.editingProductId.set(null);
    this.productForm.reset({ sku: '', name: '', price: 0, stock: 0 });
  }

  onDeleteProduct(product: ProductItem): void {
    this.productService.deleteProduct(product.id).subscribe({
      next: () => {
        if (this.editingProductId() === product.id) {
          this.onCancelEdit();
        }
        this.loadProducts();
      }
    });
  }
}
