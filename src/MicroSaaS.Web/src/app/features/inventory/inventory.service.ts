import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { BaseApiService } from '../../core/http/base-api.service';
import { CreateProductCommand, ProductInventoryDTO } from './models/inventory.models';

interface RawInventoryItem {
  id: string;
  sku: string;
  name: string;
  currentStock?: number;
  stock?: number;
  minimumStock?: number;
  price?: number;
  salePrice?: number;
  category?: string;
}

@Injectable({
  providedIn: 'root'
})
export class InventoryService {
  private readonly api = inject(BaseApiService);

  /**
   * Consulta el listado de inventario mediante HTTP GET hacia /api/inventory.
   */
  getInventory(): Observable<ProductInventoryDTO[]> {
    return this.api.get<RawInventoryItem[]>('/api/inventory').pipe(
      map((items) =>
        (items ?? []).map((item) => ({
          id: item.id,
          sku: item.sku,
          name: item.name,
          currentStock: item.currentStock ?? item.stock ?? 0,
          minimumStock: item.minimumStock ?? 5,
          price: item.price ?? item.salePrice ?? 0,
          category: item.category ?? 'General'
        }))
      )
    );
  }

  /**
   * Despacha el comando CreateProductCommand mediante HTTP POST hacia /api/inventory.
   */
  createProduct(command: CreateProductCommand): Observable<ProductInventoryDTO | { id: string }> {
    const payload = {
      ...command,
      salePrice: command.price,
      costPrice: Math.round(command.price * 0.7),
      currentStock: command.initialStock
    };

    return this.api.post<ProductInventoryDTO | { id: string }>('/api/inventory', payload);
  }
}

