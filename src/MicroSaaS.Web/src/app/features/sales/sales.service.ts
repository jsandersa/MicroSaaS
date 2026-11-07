import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { BaseApiService } from '../../core/http/base-api.service';
import { ProductDTO } from './models/sales.models';

interface RawProductResponse {
  id: string;
  name: string;
  sku: string;
  price?: number;
  salePrice?: number;
  stock?: number;
  currentStock?: number;
}

export interface CashSessionSummary {
  id: string;
  openingAmount: number;
  expectedClosingAmount: number;
  openedAt: string;
  status: string;
}

export interface CurrentCashSessionResponse {
  isOpen: boolean;
  session: CashSessionSummary | null;
}

export interface CloseCashRegisterResult {
  sessionId: string;
  status: number | string;
  openingAmount: number;
  expectedCashAmount: number;
  actualCashAmount: number;
  cashDifference: number;
  expectedCardAmount: number;
  actualCardAmount: number;
  cardDifference: number;
  expectedClosingAmount: number;
  actualClosingAmount: number;
  totalDifference: number;
  closedAt: string;
}

export interface EmittedDteDto {
  id: string;
  saleId: string;
  dteType: number;
  dteTypeName: string;
  folio: number;
  totalAmount: number;
  status: number;
  statusName: string;
  emissionDate: string;
  xmlContent: string;
}

export interface CreditNoteResultDto {
  creditNoteId: string;
  creditNoteFolio: number;
  annulledDteFolio: number;
  restoredItemsCount: number;
  message: string;
}

export interface DashboardSummaryDto {
  todaySalesTotal: number;
  todaySalesCount: number;
  monthSalesTotal: number;
  monthSalesNet: number;
  monthIvaDebito: number;
  monthSalesCount: number;
  monthPurchasesTotal: number;
  monthPurchasesNet: number;
  monthIvaCredito: number;
  monthPurchasesCount: number;
  ivaAPagar: number;
  remanenteCreditoFiscal: number;
  monthCogsPmp: number;
  monthGrossProfit: number;
  creditNotesCount: number;
  creditNotesTotal: number;
  inventoryValorizedPmp: number;
  inventoryExpectedSaleValue: number;
  totalProductsCount: number;
  criticalStockProducts: Array<{
    id: string;
    sku: string;
    name: string;
    currentStock: number;
    pmpCost: number;
  }>;
  topProducts: Array<{
    sku: string;
    name: string;
    unitsSold: number;
    revenue: number;
    costPmp: number;
    grossProfit: number;
  }>;
  cafSummary: Array<{
    dteType: number;
    dteTypeName: string;
    startFolio: number;
    endFolio: number;
    currentFolio: number;
    availableFolios: number;
    usagePercentage: number;
  }>;
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
    const payload = {
      ...command,
      folio: command.folio || 0,
      rutCliente: command.rutCliente || '66.666.666-6',
      razonSocialCliente: command.razonSocialCliente || 'Cliente POS'
    };

    return this.api.post<{ id: string }>('/api/sales', payload);
  }

  /**
   * Consulta los Documentos Tributarios Electrónicos (DTE) emitidos por el Tenant actual.
   */
  getEmittedDtes(): Observable<EmittedDteDto[]> {
    return this.api.get<EmittedDteDto[]>('/api/sales/dtes');
  }

  /**
   * Emite una Nota de Crédito Electrónica (DTE 61) anulando el DTE seleccionado y reingresando el stock al Kardex.
   */
  emitCreditNote(dteId: string, reason?: string): Observable<CreditNoteResultDto> {
    return this.api.post<CreditNoteResultDto>(`/api/sales/dtes/${dteId}/credit-note`, {
      reason: reason || 'Anulación de venta y devolución de mercadería a inventario'
    });
  }

  /**
   * Obtiene el resumen ejecutivo, rentabilidad PMP y pre-balance IVA F29 del Tenant.
   */
  getDashboardSummary(): Observable<DashboardSummaryDto> {
    return this.api.get<DashboardSummaryDto>('/api/sales/dashboard-summary');
  }

  /**
   * Descarga el Pack Contable Mensual en formato Microsoft Excel (.xlsx) con 4 hojas (F29, Ventas, Compras y Kardex PMP).
   */
  downloadAccountingExcelPack(): Observable<Blob> {
    return this.api.getBlob('/api/sales/accounting-export-excel');
  }

  /**
   * Consulta si el operador actual posee una sesión de caja abierta.
   */
  getCurrentCashSession(): Observable<CurrentCashSessionResponse> {
    return this.api.get<CurrentCashSessionResponse>('/api/cash-register/current');
  }

  /**
   * Abre una nueva sesión diaria de caja con el fondo inicial indicado.
   */
  openCashRegister(openingAmount: number): Observable<{ id: string }> {
    return this.api.post<{ id: string }>('/api/cash-register/open', { openingAmount });
  }

  /**
   * Cierra la sesión de caja actual realizando el arqueo y cuadratura de efectivo y tarjetas.
   */
  closeCashRegister(
    actualCashAmount: number,
    actualCardAmount: number
  ): Observable<CloseCashRegisterResult> {
    return this.api.post<CloseCashRegisterResult>('/api/cash-register/close', {
      actualCashAmount,
      actualCardAmount
    });
  }
}
