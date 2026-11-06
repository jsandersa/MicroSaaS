import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { BaseApiService } from '../../core/http/base-api.service';
import {
  CreateProductCommand,
  KardexMovementDTO,
  ProductInventoryDTO
} from './models/inventory.models';

interface RawInventoryItem {
  id: string;
  sku: string;
  name: string;
  currentStock?: number;
  stock?: number;
  minimumStock?: number;
  price?: number;
  salePrice?: number;
  costPrice?: number;
  pmpCost?: number;
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
          pmpCost: item.pmpCost ?? item.costPrice ?? 0,
          category: item.category ?? 'General'
        }))
      )
    );
  }

  /**
   * Consulta el historial del Libro Kardex mediante HTTP GET hacia /api/inventory/kardex.
   */
  getKardexMovements(): Observable<KardexMovementDTO[]> {
    return this.api.get<KardexMovementDTO[]>('/api/inventory/kardex');
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

  /**
   * Actualiza un producto existente (Precio de Venta, Costo PMP y Stock) mediante HTTP PUT hacia /api/inventory/{id}.
   */
  updateProduct(
    id: string,
    payload: {
      sku: string;
      name: string;
      salePrice: number;
      costPrice: number;
      currentStock: number;
      adjustmentReason?: string | null;
    }
  ): Observable<void> {
    return this.api.put<void>(`/api/inventory/${id}`, {
      id,
      ...payload
    });
  }
}
