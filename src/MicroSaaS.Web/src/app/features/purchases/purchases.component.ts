import { Component, inject, signal, computed } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, FormControl } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, distinctUntilChanged, filter, switchMap } from 'rxjs/operators';
import { CommonModule } from '@angular/common';
import { PurchasesService } from './purchases.service';
import { rutValidator } from '../../shared/validators/rut.validator';
import { PurchaseItem, CreatePurchaseCommand } from './models/purchases.models';
import { ProductDTO } from '../sales/models/sales.models';

@Component({
  selector: 'app-purchases',
  standalone: true,
  imports: [ReactiveFormsModule, CommonModule],
  templateUrl: './purchases.component.html',
  styleUrl: './purchases.component.css'
})
export class PurchasesComponent {
  private fb = inject(FormBuilder);
  private purchasesService = inject(PurchasesService);

  // Gestión de Estado (Signals)
  cart = signal<PurchaseItem[]>([]);
  isProcessing = signal(false);
  successMessage = signal('');
  errorMessage = signal('');

  // Controles de Búsqueda
  searchControl = new FormControl('');
  searchResults = signal<ProductDTO[]>([]);

  // Formulario reactivo para la cabecera del documento
  headerForm: FormGroup = this.fb.group({
    supplierRut: ['', [Validators.required, rutValidator]],
    documentNumber: ['', Validators.required]
  });

  // Cálculos dinámicos (19% IVA Chile)
  subtotalNeto = computed(() => this.cart().reduce((sum, item) => sum + item.subtotal, 0));
  totalIva = computed(() => Math.round(this.subtotalNeto() * 0.19));
  totalCompra = computed(() => this.subtotalNeto() + this.totalIva());

  constructor() {
    // Escuchar cambios en el input con RxJS
    this.searchControl.valueChanges
      .pipe(
        takeUntilDestroyed(),
        debounceTime(300),
        distinctUntilChanged(),
        filter(term => typeof term === 'string' && term.length >= 2),
        switchMap(term => this.purchasesService.searchProducts(term!))
      )
      .subscribe({
        next: (products) => this.searchResults.set(products as unknown as ProductDTO[]),
        error: (err) => console.error('Error al buscar productos', err)
      });
  }

  selectProduct(product: ProductDTO) {
    const defaultQty = 1;
    const defaultCost = product.price || 0;

    this.addPurchaseItem(product, defaultQty, defaultCost);

    this.searchControl.reset('', { emitEvent: false });
    this.searchResults.set([]);
  }

  addPurchaseItem(product: ProductDTO, quantity: number, unitCost: number) {
    if (this.cart().some(item => item.productId === product.id)) {
      this.errorMessage.set('El producto ya está en el detalle de ingreso.');
      return;
    }

    // Usar el margen específico o caer en el 30% global por defecto
    const margin = product.customMarginPercentage ?? 30;

    const item = {
      productId: product.id,
      name: product.name,
      sku: product.sku,
      quantity,
      unitCost,
      subtotal: quantity * unitCost,
      customMarginPercentage: product.customMarginPercentage,
      // Calculamos el precio de venta inicial sugerido
      price: Math.round(unitCost * (1 + (margin / 100)))
    } as PurchaseItem;

    this.cart.update(current => [...current, item]);
    this.errorMessage.set('');
  }

  updatePurchaseItem(productId: string, field: 'quantity' | 'unitCost', event: Event) {
    const input = event.target as HTMLInputElement;
    const value = Number(input.value);

    if (value < 0) return;

    this.cart.update(current =>
      current.map(item => {
        if (item.productId === productId) {
          const updatedItem = { ...item, [field]: value };
          updatedItem.subtotal = updatedItem.quantity * updatedItem.unitCost;

          // Si el usuario edita el costo en la grilla, recalcula el precio de venta en vivo
          if (field === 'unitCost') {
            const margin = updatedItem.customMarginPercentage ?? 30;
            updatedItem.price = Math.round(updatedItem.unitCost * (1 + (margin / 100)));
          }

          return updatedItem;
        }
        return item;
      })
    );
  }

  removePurchaseItem(productId: string) {
    this.cart.update(current => current.filter(item => item.productId !== productId));
  }

  onSubmit() {
    if (this.headerForm.invalid || this.cart().length === 0) {
      this.headerForm.markAllAsTouched();
      return;
    }

    this.isProcessing.set(true);
    const command: CreatePurchaseCommand = {
      supplierRut: this.headerForm.value.supplierRut,
      documentNumber: this.headerForm.value.documentNumber,
      items: this.cart(),
      totalAmount: this.totalCompra()
    };

    this.purchasesService.registerPurchase(command).subscribe({
      next: () => {
        this.cart.set([]);
        this.headerForm.reset();
        this.successMessage.set('¡Ingreso de mercadería registrado con éxito!');
        this.isProcessing.set(false);
        setTimeout(() => this.successMessage.set(''), 4000);
      },
      error: (err) => {
        console.error(err);
        this.errorMessage.set('Ocurrió un error al procesar la compra.');
        this.isProcessing.set(false);
      }
    });
  }
}