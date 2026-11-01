import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { BaseApiService } from '../../core/http/base-api.service';
import {
  CreatePurchaseCommand,
  PurchaseProductOption,
  SupplierDTO
} from './models/purchases.models';

interface RawProductItem {
  id: string;
  sku: string;
  name: string;
  costPrice?: number;
  price?: number;
  salePrice?: number;
  currentStock?: number;
  stock?: number;
}

interface RawPurchaseSummary {
  id: string;
  rutProveedor?: string;
  razonSocialProveedor?: string;
}

@Injectable({
  providedIn: 'root'
})
export class PurchasesService {
  private readonly api = inject(BaseApiService);

  /**
   * Busca proveedores por RUT o razón social mediante HTTP GET.
   */
  searchSuppliers(term: string): Observable<SupplierDTO[]> {
    const cleanTerm = term.trim();
    return this.api.get<SupplierDTO[]>('/api/suppliers', { search: cleanTerm }).pipe(
      catchError(() =>
        this.api.get<RawPurchaseSummary[]>('/api/purchases').pipe(
          map((purchases) => {
            const uniqueByRut = new Map<string, SupplierDTO>();
            for (const p of purchases ?? []) {
              const rut = p.rutProveedor ?? '';
              if (rut && !uniqueByRut.has(rut)) {
                uniqueByRut.set(rut, {
                  id: p.id,
                  rut,
                  name: p.razonSocialProveedor ?? 'Proveedor Comercial'
                });
              }
            }
            const list = Array.from(uniqueByRut.values());
            if (!cleanTerm) {
              return list;
            }
            const lower = cleanTerm.toLowerCase();
            return list.filter(
              (s) =>
                s.rut.toLowerCase().includes(lower) ||
                s.name.toLowerCase().includes(lower)
            );
          }),
          catchError(() => of([] as SupplierDTO[]))
        )
      )
    );
  }

  /**
   * Consulta el catálogo de productos mediante HTTP GET para ingreso de compras.
   */
  searchProducts(term: string): Observable<PurchaseProductOption[]> {
    const cleanTerm = term.trim();
    return this.api.get<RawProductItem[]>('/api/products', { search: cleanTerm }).pipe(
      map((items) => {
        const normalized: PurchaseProductOption[] = (items ?? []).map((item) => ({
          id: item.id,
          sku: item.sku,
          name: item.name,
          unitCost:
            item.costPrice ??
            Math.round((item.price ?? item.salePrice ?? 1000) * 0.7),
          currentStock: item.currentStock ?? item.stock ?? 0
        }));

        if (!cleanTerm) {
          return normalized;
        }

        const lower = cleanTerm.toLowerCase();
        return normalized.filter(
          (p) =>
            p.name.toLowerCase().includes(lower) ||
            p.sku.toLowerCase().includes(lower)
        );
      })
    );
  }

  /**
   * Envía el comando CreatePurchaseCommand hacia /api/purchases mediante HTTP POST.
   */
  registerPurchase(command: CreatePurchaseCommand): Observable<{ id: string }> {
    const subtotalNeto = command.items.reduce((acc, item) => acc + item.subtotal, 0);
    const iva = Math.round(subtotalNeto * 0.19);
    const parsedFolio = parseInt(command.documentNumber.replace(/\D/g, ''), 10);

    const payload = {
      ...command,
      folio: Number.isNaN(parsedFolio) || parsedFolio <= 0 ? 1 : parsedFolio,
      tipoDte: 33,
      fechaEmision: new Date().toISOString(),
      rutProveedor: command.supplierRut.trim(),
      razonSocialProveedor: `Proveedor (${command.supplierRut.trim()})`,
      montoNeto: subtotalNeto,
      iva,
      montoTotal: command.totalAmount,
      items: command.items.map((item) => ({
        ...item,
        sku: item.sku,
        nombreItem: item.name,
        cantidad: item.quantity,
        precioUnitario: item.unitCost,
        montoItem: item.subtotal,
        esExento: false
      }))
    };

    return this.api.post<{ id: string }>('/api/purchases', payload);
  }
}

