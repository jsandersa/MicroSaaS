import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { BaseApiService } from '../../core/http/base-api.service';
import { CreateSaleCommand, ProductDTO } from './models/sales.models';

interface RawProductResponse {
  id: string;
  name: string;
  sku: string;
  price?: number;
  salePrice?: number;
  stock?: number;
  currentStock?: number;
}

@Injectable({
  providedIn: 'root'
})
export class SalesService {
  private readonly api = inject(BaseApiService);

  /**
   * Consulta el inventario filtrando por término de búsqueda (nombre o SKU).
   */
  searchProducts(term: string): Observable<ProductDTO[]> {
    const cleanTerm = term.trim();
    return this.api.get<RawProductResponse[]>('/api/products', { search: cleanTerm }).pipe(
      map((items) => {
        const normalized: ProductDTO[] = (items ?? []).map((item) => ({
          id: item.id,
          name: item.name,
          sku: item.sku,
          price: item.price ?? item.salePrice ?? 0,
          stock: item.stock ?? item.currentStock ?? 0
        }));

        if (!cleanTerm) {
          return normalized;
        }

        const lowerTerm = cleanTerm.toLowerCase();
        return normalized.filter(
          (p) =>
            p.name.toLowerCase().includes(lowerTerm) ||
            p.sku.toLowerCase().includes(lowerTerm)
        );
      })
    );
  }

  /**
   * Envía el comando de creación de venta hacia el backend CQRS.
   */
  processSale(command: CreateSaleCommand): Observable<{ id: string }> {
    const subtotalNeto = command.items.reduce((acc, item) => acc + item.subtotal, 0);
    const iva = Math.round(subtotalNeto * 0.19);
    const payload = {
      ...command,
      folio: Math.floor(Date.now() / 1000) % 1000000,
      tipoDte: 39,
      fechaEmision: new Date().toISOString(),
      rutCliente: command.customerRut || '66.666.666-6',
      razonSocialCliente: 'Cliente POS',
      montoNeto: subtotalNeto,
      iva,
      montoTotal: command.totalAmount,
      items: command.items.map((item) => ({
        ...item,
        sku: item.product.sku,
        nombreItem: item.product.name,
        cantidad: item.quantity,
        precioUnitario: item.product.price,
        montoItem: item.subtotal,
        esExento: false
      }))
    };

    return this.api.post<{ id: string }>('/api/sales', payload);
  }
}

