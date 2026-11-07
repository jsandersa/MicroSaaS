import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { DashboardSummaryDto, SalesService } from '../sales/sales.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="dashboard-wrapper">
      <header class="dashboard-header">
        <div>
          <h1>
            {{ authService.isAccountant() ? '💼 Portal del Contador y Auditoría F29 (SII)' : '📊 Dashboard Ejecutivo y Pre-Balance Tributario (F29 SII)' }}
          </h1>
          <p class="subtitle">
            Control financiero en tiempo real: Ventas, Utilidad Bruta sobre Costo PMP, IVA Débito/Crédito y Exportación Contable
          </p>
        </div>
        <div class="header-actions">
          <button type="button" class="btn-secondary" (click)="loadDashboard()">
            🔄 Actualizar Indicadores
          </button>
          @if (!authService.isAccountant()) {
            <a routerLink="/sales" class="btn-primary">Ir al Punto de Venta (POS)</a>
          }
        </div>
      </header>

      <!-- Tarjeta Especial de Exportación Contable (4 Hojas Excel) -->
      <section class="card" style="margin-bottom:1.25rem;background:linear-gradient(135deg,#f0fdf4 0%,#ecfdf5 100%);border:1px solid #86efac;display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:1rem;">
        <div>
          <strong style="color:#065f46;font-size:1rem;display:block;margin-bottom:0.2rem;">
            💼 Centro de Exportación para el Contador (Libro Excel Multi-Hoja .xlsx)
          </strong>
          <span style="color:#047857;font-size:0.86rem;">
            Descarga en un solo archivo Excel las <strong>4 hojas oficiales</strong> listas para contabilidad:
            <strong>1. Pre-Balance F29</strong> · <strong>2. Libro de Ventas DTE (33, 39, 61)</strong> · <strong>3. Libro de Compras</strong> · <strong>4. Existencias y Kardex Valorizado PMP</strong>.
          </span>
        </div>
        <button
          type="button"
          style="background:#15803d;color:#ffffff;border:none;padding:0.65rem 1.2rem;border-radius:8px;font-weight:700;font-size:0.88rem;cursor:pointer;"
          [disabled]="isExportingExcel()"
          (click)="exportAccountingExcel()"
        >
          {{ isExportingExcel() ? 'Generando Excel...' : '📗 Descargar Pack Contable en Excel (.xlsx)' }}
        </button>
      </section>

      @if (isLoading()) {
        <div class="loading-box card">
          <p>Calculando indicadores financieros, PMP e impuestos del período...</p>
        </div>
      } @else {
        @if (summary(); as data) {
        <!-- Fila 1: KPIs Comerciales y de Rentabilidad Real -->
        <section class="kpi-grid">
          <article class="kpi-card card kpi-blue">
            <span class="kpi-label">Ventas de Hoy ({{ data.todaySalesCount }} docs)</span>
            <strong class="kpi-value">\${{ data.todaySalesTotal | number: '1.0-0' }}</strong>
            <small class="kpi-sub">Total bruto IVA incluido recaudado hoy</small>
          </article>

          <article class="kpi-card card kpi-indigo">
            <span class="kpi-label">Ventas Acumuladas Mes ({{ data.monthSalesCount }} docs)</span>
            <strong class="kpi-value">\${{ data.monthSalesTotal | number: '1.0-0' }}</strong>
            <small class="kpi-sub">Venta Neta sin IVA: \${{ data.monthSalesNet | number: '1.0-0' }}</small>
          </article>

          <article class="kpi-card card kpi-green">
            <span class="kpi-label">Utilidad Bruta Real (Sobre Costo PMP)</span>
            <strong class="kpi-value">\${{ data.monthGrossProfit | number: '1.0-0' }}</strong>
            <small class="kpi-sub">Costo de Ventas PMP: \${{ data.monthCogsPmp | number: '1.0-0' }}</small>
          </article>

          <article class="kpi-card card kpi-amber">
            <span class="kpi-label">Valorización Actual de Bodega ({{ data.totalProductsCount }} SKUs)</span>
            <strong class="kpi-value">\${{ data.inventoryValorizedPmp | number: '1.0-0' }}</strong>
            <small class="kpi-sub">Valor venta proyectado: \${{ data.inventoryExpectedSaleValue | number: '1.0-0' }}</small>
          </article>
        </section>

        <!-- Fila 2: Termómetro Tributario Formulario 29 (SII Chile) -->
        <section class="f29-card card">
          <div class="section-header">
            <div>
              <h2>🏛️ Pre-Balance Mensual de IVA — Formulario 29 (SII)</h2>
              <p class="section-sub">
                Cálculo automático descontando Notas de Crédito (61) emitidas en el período ({{ data.creditNotesCount }} NC por \${{ data.creditNotesTotal | number: '1.0-0' }})
              </p>
            </div>
            @if (data.ivaAPagar > 0) {
              <span class="f29-badge badge-pay">
                IVA Estimado a Pagar: \${{ data.ivaAPagar | number: '1.0-0' }}
              </span>
            } @else {
              <span class="f29-badge badge-credit">
                ✓ Remanente a Favor: \${{ data.remanenteCreditoFiscal | number: '1.0-0' }}
              </span>
            }
          </div>

          <div class="f29-grid">
            <div class="f29-box">
              <span class="f29-title">➕ IVA Débito Fiscal (Ventas Vigentes)</span>
              <strong class="f29-amount text-danger">\${{ data.monthIvaDebito | number: '1.0-0' }}</strong>
              <small>Generado por {{ data.monthSalesCount }} ventas afectas del mes</small>
            </div>

            <div class="f29-box">
              <span class="f29-title">➖ IVA Crédito Fiscal (Compras con Factura)</span>
              <strong class="f29-amount text-success">\${{ data.monthIvaCredito | number: '1.0-0' }}</strong>
              <small>Respaldado por {{ data.monthPurchasesCount }} compras registradas (\${{ data.monthPurchasesTotal | number: '1.0-0' }})</small>
            </div>

            <div class="f29-box f29-result">
              @if (data.ivaAPagar > 0) {
                <span class="f29-title">⚖️ Saldo Determinado F29 (Impuesto a Pagar)</span>
                <strong class="f29-amount text-danger">\${{ data.ivaAPagar | number: '1.0-0' }}</strong>
                <small>Débito Fiscal supera al Crédito Fiscal del período</small>
              } @else {
                <span class="f29-title">⚖️ Remanente de Crédito Fiscal para Mes Siguiente</span>
                <strong class="f29-amount text-success">\${{ data.remanenteCreditoFiscal | number: '1.0-0' }}</strong>
                <small>Crédito por compras absorbe el 100% del IVA Débito</small>
              }
            </div>
          </div>
        </section>

        <!-- Fila 3: Top Productos Rentables + Estado de Folios CAF y Stock Crítico -->
        <div class="bottom-grid">
          <!-- Tabla Top 5 Productos -->
          <section class="card">
            <div class="section-header">
              <h2>🏆 Top Productos Vendidos y Margen PMP</h2>
            </div>
            @if (data.topProducts.length === 0) {
              <p class="empty-msg">Aún no se registran ventas activas en el mes actual.</p>
            } @else {
              <div class="table-responsive">
                <table class="dash-table">
                  <thead>
                    <tr>
                      <th>SKU</th>
                      <th>Producto</th>
                      <th class="text-center">Unidades</th>
                      <th class="text-right">Venta Neta</th>
                      <th class="text-right">Costo PMP</th>
                      <th class="text-right">Utilidad Real</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (prod of data.topProducts; track prod.sku) {
                      <tr>
                        <td class="mono">{{ prod.sku }}</td>
                        <td><strong>{{ prod.name }}</strong></td>
                        <td class="text-center"><strong>{{ prod.unitsSold }}</strong></td>
                        <td class="text-right">\${{ prod.revenue | number: '1.0-0' }}</td>
                        <td class="text-right muted">\${{ prod.costPmp | number: '1.0-0' }}</td>
                        <td class="text-right text-success"><strong>\${{ prod.grossProfit | number: '1.0-0' }}</strong></td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }
          </section>

          <!-- Panel de Estado CAF SII y Alertas de Stock -->
          <section class="card">
            <div class="section-header">
              <h2>🔐 Estado de Folios CAF SII y Alertas</h2>
              <a routerLink="/settings" class="link-small">Administrar CAF →</a>
            </div>

            <div class="caf-mini-list">
              @for (caf of data.cafSummary; track caf.dteType) {
                <div class="caf-mini-item">
                  <div class="caf-mini-top">
                    <strong>{{ caf.dteTypeName }}</strong>
                    <span class="caf-pill" [class.pill-warn]="caf.availableFolios <= 15">
                      {{ caf.availableFolios }} folios libres (Último: {{ caf.currentFolio > 0 ? 'N° ' + caf.currentFolio : 'Ninguno' }})
                    </span>
                  </div>
                  <div class="mini-progress-track">
                    <div
                      class="mini-progress-fill"
                      [style.width.%]="caf.usagePercentage > 100 ? 100 : (caf.usagePercentage < 3 && caf.currentFolio > 0 ? 4 : caf.usagePercentage)"
                    ></div>
                  </div>
                </div>
              }
            </div>

            <h3 style="margin:1.25rem 0 0.6rem;font-size:0.95rem;color:#0f172a;">
              ⚠ Productos en Stock Crítico ({{ data.criticalStockProducts.length }})
            </h3>
            @if (data.criticalStockProducts.length === 0) {
              <p class="empty-msg" style="padding:0.75rem;background:#ecfdf5;color:#065f46;border-radius:8px;">
                ✓ Todos los productos del catálogo cuentan con stock óptimo.
              </p>
            } @else {
              <div class="critical-list">
                @for (item of data.criticalStockProducts; track item.id) {
                  <div class="critical-item">
                    <span><strong>{{ item.name }}</strong> ({{ item.sku }})</span>
                    <span class="badge-crit">Quedan {{ item.currentStock }} u.</span>
                  </div>
                }
              </div>
            }
          </section>
        </div>
        }
      }
    </div>
  `,
  styles: [
    `
      .dashboard-wrapper {
        max-width: 1260px;
        margin: 0 auto;
        padding: 1.5rem;
        background: #f8fafc;
        min-height: calc(100vh - 64px);
        box-sizing: border-box;
      }
      .dashboard-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        flex-wrap: wrap;
        gap: 1rem;
        margin-bottom: 1.5rem;
      }
      .dashboard-header h1 {
        margin: 0;
        font-size: 1.6rem;
        color: #0f172a;
      }
      .subtitle {
        margin: 0.25rem 0 0;
        color: #64748b;
        font-size: 0.92rem;
      }
      .header-actions {
        display: flex;
        gap: 0.75rem;
        align-items: center;
      }
      .btn-primary {
        background: #2563eb;
        color: #fff;
        text-decoration: none;
        padding: 0.6rem 1.15rem;
        border-radius: 8px;
        font-weight: 600;
        font-size: 0.9rem;
      }
      .btn-secondary {
        background: #fff;
        color: #334155;
        border: 1px solid #cbd5e1;
        padding: 0.6rem 1rem;
        border-radius: 8px;
        font-weight: 600;
        font-size: 0.9rem;
        cursor: pointer;
      }
      .card {
        background: #fff;
        border-radius: 12px;
        box-shadow: 0 4px 16px rgba(15, 23, 42, 0.05);
        padding: 1.35rem;
      }
      .loading-box {
        text-align: center;
        padding: 3rem;
        color: #64748b;
      }
      .kpi-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
        gap: 1.1rem;
        margin-bottom: 1.5rem;
      }
      .kpi-card {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
        border-top: 4px solid #2563eb;
      }
      .kpi-blue { border-top-color: #2563eb; }
      .kpi-indigo { border-top-color: #4f46e5; }
      .kpi-green { border-top-color: #16a34a; }
      .kpi-amber { border-top-color: #d97706; }
      .kpi-label {
        font-size: 0.84rem;
        font-weight: 600;
        color: #64748b;
      }
      .kpi-value {
        font-size: 1.75rem;
        font-weight: 800;
        color: #0f172a;
      }
      .kpi-sub {
        font-size: 0.8rem;
        color: #475569;
      }
      .f29-card {
        margin-bottom: 1.5rem;
        border-left: 5px solid #1e40af;
      }
      .section-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        flex-wrap: wrap;
        gap: 0.75rem;
        margin-bottom: 1rem;
      }
      .section-header h2 {
        margin: 0;
        font-size: 1.15rem;
        color: #0f172a;
      }
      .section-sub {
        margin: 0.2rem 0 0;
        font-size: 0.84rem;
        color: #64748b;
      }
      .f29-badge {
        padding: 0.45rem 0.95rem;
        border-radius: 999px;
        font-weight: 700;
        font-size: 0.88rem;
      }
      .badge-pay {
        background: #fef2f2;
        color: #b91c1c;
        border: 1px solid #fecaca;
      }
      .badge-credit {
        background: #ecfdf5;
        color: #065f46;
        border: 1px solid #6ee7b7;
      }
      .f29-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
        gap: 1rem;
      }
      .f29-box {
        background: #f8fafc;
        border: 1px solid #e2e8f0;
        border-radius: 10px;
        padding: 1rem;
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
      }
      .f29-result {
        background: #eff6ff;
        border-color: #bfdbfe;
      }
      .f29-title {
        font-size: 0.84rem;
        font-weight: 700;
        color: #334155;
      }
      .f29-amount {
        font-size: 1.45rem;
        font-weight: 800;
      }
      .text-danger { color: #dc2626; }
      .text-success { color: #16a34a; }
      .muted { color: #64748b; }
      .bottom-grid {
        display: grid;
        grid-template-columns: 1.3fr 1fr;
        gap: 1.25rem;
      }
      @media (max-width: 920px) {
        .bottom-grid {
          grid-template-columns: 1fr;
        }
      }
      .dash-table {
        width: 100%;
        border-collapse: collapse;
        font-size: 0.88rem;
      }
      .dash-table th {
        text-align: left;
        padding: 0.65rem;
        background: #f8fafc;
        color: #475569;
        border-bottom: 2px solid #e2e8f0;
      }
      .dash-table td {
        padding: 0.65rem;
        border-bottom: 1px solid #e2e8f0;
      }
      .text-right { text-align: right; }
      .text-center { text-align: center; }
      .mono { font-family: monospace; font-weight: 600; color: #475569; }
      .link-small {
        font-size: 0.84rem;
        font-weight: 600;
        color: #2563eb;
        text-decoration: none;
      }
      .caf-mini-list {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
      }
      .caf-mini-item {
        background: #f8fafc;
        border: 1px solid #e2e8f0;
        border-radius: 8px;
        padding: 0.7rem 0.85rem;
      }
      .caf-mini-top {
        display: flex;
        justify-content: space-between;
        font-size: 0.84rem;
        margin-bottom: 0.4rem;
      }
      .caf-pill {
        font-size: 0.78rem;
        font-weight: 600;
        color: #166534;
      }
      .pill-warn { color: #dc2626; }
      .mini-progress-track {
        height: 6px;
        background: #e2e8f0;
        border-radius: 999px;
        overflow: hidden;
      }
      .mini-progress-fill {
        height: 100%;
        background: #16a34a;
      }
      .critical-list {
        display: flex;
        flex-direction: column;
        gap: 0.45rem;
      }
      .critical-item {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 0.5rem 0.75rem;
        background: #fef2f2;
        border: 1px solid #fecaca;
        border-radius: 6px;
        font-size: 0.84rem;
      }
      .badge-crit {
        font-weight: 700;
        color: #b91c1c;
      }
      .empty-msg {
        color: #64748b;
        font-size: 0.88rem;
      }
    `
  ]
})
export class DashboardComponent implements OnInit {
  private readonly salesService = inject(SalesService);
  readonly authService = inject(AuthService);

  readonly summary = signal<DashboardSummaryDto | null>(null);
  readonly isLoading = signal<boolean>(true);
  readonly isExportingExcel = signal<boolean>(false);

  ngOnInit(): void {
    this.loadDashboard();
  }

  loadDashboard(): void {
    this.isLoading.set(true);
    this.salesService
      .getDashboardSummary()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (data) => this.summary.set(data),
        error: () => this.summary.set(null)
      });
  }

  exportAccountingExcel(): void {
    if (this.isExportingExcel()) return;
    this.isExportingExcel.set(true);

    this.salesService
      .downloadAccountingExcelPack()
      .pipe(finalize(() => this.isExportingExcel.set(false)))
      .subscribe({
        next: (blob) => {
          const now = new Date();
          const period = `${now.getFullYear()}_${String(now.getMonth() + 1).padStart(2, '0')}`;
          const url = URL.createObjectURL(blob);
          const anchor = document.createElement('a');
          anchor.href = url;
          anchor.download = `Pack_Contable_SII_F29_${period}.xlsx`;
          anchor.click();
          URL.revokeObjectURL(url);
        }
      });
  }
}
