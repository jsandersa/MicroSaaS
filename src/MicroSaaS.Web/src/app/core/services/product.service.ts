import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ProductItem {
  id: string;
  sku: string;
  name: string;
  price: number;
  stock: number;
  isActive?: boolean;
}

export interface ProductPayload {
  id?: string;
  sku: string;
  name: string;
  price: number;
  stock: number;
}

@Injectable({
  providedIn: 'root'
})
export class ProductService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl.replace(/\/api\/?$/, '');
  private readonly productsUrl = `${this.baseUrl}/api/Products`;

  getProducts(): Observable<ProductItem[]> {
    return this.http.get<ProductItem[]>(this.productsUrl);
  }

  getProductById(id: string): Observable<ProductItem> {
    return this.http.get<ProductItem>(`${this.productsUrl}/${id}`);
  }

  createProduct(payload: ProductPayload): Observable<string> {
    return this.http.post<string>(this.productsUrl, payload);
  }

  updateProduct(id: string, payload: ProductPayload): Observable<void> {
    return this.http.put<void>(`${this.productsUrl}/${id}`, {
      id,
      ...payload
    });
  }

  deleteProduct(id: string): Observable<void> {
    return this.http.delete<void>(`${this.productsUrl}/${id}`);
  }
}

