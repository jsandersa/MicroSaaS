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
        const normalized: ProductDTO[] = (items ?? []).map((item: any) => ({
          id: item.id,
          name: item.name,
          sku: item.sku,
          price: item.salePrice ?? item.price ?? 0,
          stock: item.currentStock ?? item.stock ?? 0,
          customMarginPercentage: item.customMarginPercentage ?? null
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
  processSale(command: any): Observable<{ id: string }> {
    // El componente ya aplanó los items y calculó los totales (montoNeto, iva, montoTotal).
    // Solo agregamos los valores por defecto para folio y cliente si vienen vacíos.
    const payload = {
      ...command,
      folio: command.folio || (Math.floor(Date.now() / 1000) % 1000000),
      rutCliente: command.rutCliente || '66.666.666-6',
      razonSocialCliente: command.razonSocialCliente || 'Cliente POS'
    };

    return this.api.post<{ id: string }>('/api/sales', payload);
  }
}