import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="landing-page">
      <!-- HERO SECTION -->
      <section class="hero-section">
        <div class="hero-container">
          <div class="hero-text">
            <span class="hero-badge">🏛️ ERP Cloud Multi-Tenant · Normativa SII Chile</span>
            <h1>
              Control Total de tu Pyme:
              <span class="highlight">Punto de Venta, Kardex PMP y Facturación Electrónica SII</span>
            </h1>
            <p class="hero-description">
              La plataforma integral diseñada para comercios y empresas chilenas. Carga tu inventario desde
              <strong>Microsoft Excel (.xlsx)</strong>, vende en segundos emitiendo
              <strong>Boletas (39)</strong> y <strong>Facturas Electrónicas (33)</strong>, gestiona devoluciones con
              <strong>Notas de Crédito (61)</strong> y conoce tu <strong>Pre-Balance de IVA (Formulario 29)</strong> en tiempo real.
            </p>

            <!-- LOS 2 BOTONES PRINCIPALES SOLICITADOS -->
            <div class="cta-buttons-group">
              <a routerLink="/onboarding" class="btn-cta btn-subscribe">
                <div class="btn-icon-circle">🚀</div>
                <div class="btn-text-block">
                  <strong>1. Suscripción</strong>
                  <small>Registro y suscripción para clientes nuevos</small>
                </div>
              </a>

              <a routerLink="/login" class="btn-cta btn-login">
                <div class="btn-icon-circle">🔐</div>
                <div class="btn-text-block">
                  <strong>2. Acceso a Usuarios</strong>
                  <small>Ingreso para clientes suscritos al sistema</small>
                </div>
              </a>
            </div>

            @if (authService.isAuthenticated()) {
              <div class="active-session-banner">
                <span>🟢 Tienes una sesión corporativa activa.</span>
                <a routerLink="/dashboard" class="link-dashboard">Ir directamente al Dashboard F29 →</a>
              </div>
            }

            <div class="trust-metrics">
              <div class="metric-item">
                <strong>DTE 33 · 39 · 61</strong>
                <span>Folios CAF y Firma XML SII</span>
              </div>
              <div class="metric-divider"></div>
              <div class="metric-item">
                <strong>Kardex PMP</strong>
                <span>Valorización Contable Exacta</span>
              </div>
              <div class="metric-divider"></div>
              <div class="metric-item">
                <strong>Pre-Balance F29</strong>
                <span>IVA Débito vs Crédito Fiscal</span>
              </div>
            </div>
          </div>

          <!-- ILUSTRACIÓN GRÁFICA INTERACTIVA DEL SISTEMA -->
          <div class="hero-illustration" aria-hidden="true">
            <div class="mockup-window">
              <div class="mockup-topbar">
                <span class="dot dot-red"></span>
                <span class="dot dot-amber"></span>
                <span class="dot dot-green"></span>
                <span class="mockup-title">MicroSaaS ERP — Vista General de Procesos</span>
              </div>

              <div class="mockup-body">
                <!-- Ilustración SVG del flujo completo ERP/POS -->
                <svg viewBox="0 0 520 340" class="hero-svg" xmlns="http://www.w3.org/2000/svg">
                  <defs>
                    <linearGradient id="bgGrad" x1="0" y1="0" x2="1" y2="1">
                      <stop offset="0%" stop-color="#eff6ff" />
                      <stop offset="100%" stop-color="#f8fafc" />
                    </linearGradient>
                    <linearGradient id="blueGrad" x1="0" y1="0" x2="1" y2="0">
                      <stop offset="0%" stop-color="#1d4ed8" />
                      <stop offset="100%" stop-color="#2563eb" />
                    </linearGradient>
                    <linearGradient id="greenGrad" x1="0" y1="0" x2="1" y2="0">
                      <stop offset="0%" stop-color="#059669" />
                      <stop offset="100%" stop-color="#10b981" />
                    </linearGradient>
                  </defs>

                  <rect x="0" y="0" width="520" height="340" rx="12" fill="url(#bgGrad)" />

                  <!-- Tarjeta 1: Inventario Excel + Kardex PMP -->
                  <rect x="18" y="18" width="230" height="135" rx="10" fill="#ffffff" stroke="#cbd5e1" stroke-width="1.5" />
                  <rect x="30" y="30" width="38" height="38" rx="8" fill="#dcfce7" />
                  <text x="49" y="55" font-size="20" text-anchor="middle">📗</text>
                  <text x="78" y="45" font-family="sans-serif" font-size="12" font-weight="bold" fill="#0f172a">1. Inventario &amp; Excel</text>
                  <text x="78" y="61" font-family="sans-serif" font-size="10" fill="#64748b">Carga Masiva .xlsx y PMP</text>
                  <rect x="30" y="80" width="206" height="24" rx="5" fill="#f1f5f9" />
                  <text x="38" y="96" font-family="monospace" font-size="10" fill="#1e293b">ABAR-001 | Stock: 100 | PMP $950</text>
                  <rect x="30" y="112" width="145" height="24" rx="5" fill="#ecfdf5" />
                  <text x="38" y="128" font-family="sans-serif" font-size="10" font-weight="bold" fill="#047857">✓ Margen Sugerido +30%</text>

                  <!-- Tarjeta 2: Punto de Venta POS -->
                  <rect x="272" y="18" width="230" height="135" rx="10" fill="#ffffff" stroke="#cbd5e1" stroke-width="1.5" />
                  <rect x="284" y="30" width="38" height="38" rx="8" fill="#dbeafe" />
                  <text x="303" y="55" font-size="20" text-anchor="middle">🛒</text>
                  <text x="332" y="45" font-family="sans-serif" font-size="12" font-weight="bold" fill="#0f172a">2. Punto de Venta POS</text>
                  <text x="332" y="61" font-family="sans-serif" font-size="10" fill="#64748b">Efectivo · Tarjeta · Mixto</text>
                  <rect x="284" y="80" width="98" height="26" rx="6" fill="url(#blueGrad)" />
                  <text x="333" y="97" font-family="sans-serif" font-size="10" font-weight="bold" fill="#ffffff" text-anchor="middle">🧾 Boleta (39)</text>
                  <rect x="390" y="80" width="98" height="26" rx="6" fill="#eff6ff" stroke="#93c5fd" />
                  <text x="439" y="97" font-family="sans-serif" font-size="10" font-weight="bold" fill="#1d4ed8" text-anchor="middle">🏢 Factura (33)</text>
                  <rect x="284" y="114" width="204" height="24" rx="6" fill="url(#greenGrad)" />
                  <text x="386" y="130" font-family="sans-serif" font-size="10.5" font-weight="bold" fill="#ffffff" text-anchor="middle">Confirmar Cobro · $34.990</text>

                  <!-- Tarjeta 3: Facturación SII & F29 (Ancho Completo Inferior) -->
                  <rect x="18" y="172" width="484" height="150" rx="10" fill="#ffffff" stroke="#cbd5e1" stroke-width="1.5" />
                  <rect x="32" y="186" width="40" height="40" rx="8" fill="#ede9fe" />
                  <text x="52" y="212" font-size="20" text-anchor="middle">📊</text>
                  <text x="84" y="202" font-family="sans-serif" font-size="13" font-weight="bold" fill="#0f172a">3. Pre-Balance Tributario Formulario 29 (SII) y Utilidad Real</text>
                  <text x="84" y="219" font-family="sans-serif" font-size="10.5" fill="#64748b">Cálculo automático de IVA Débito, IVA Crédito por Compras y Notas de Crédito (61)</text>

                  <!-- 3 Cajas de KPI dentro del SVG -->
                  <rect x="32" y="238" width="142" height="68" rx="8" fill="#fef2f2" stroke="#fecaca" />
                  <text x="44" y="257" font-family="sans-serif" font-size="9.5" font-weight="bold" fill="#991b1b">➕ IVA DÉBITO VENTAS</text>
                  <text x="44" y="280" font-family="sans-serif" font-size="16" font-weight="bold" fill="#dc2626">$418.000</text>
                  <text x="44" y="296" font-family="sans-serif" font-size="8.5" fill="#7f1d1d">Neto de Notas de Crédito</text>

                  <rect x="189" y="238" width="142" height="68" rx="8" fill="#ecfdf5" stroke="#a7f3d0" />
                  <text x="201" y="257" font-family="sans-serif" font-size="9.5" font-weight="bold" fill="#065f46">➖ IVA CRÉDITO COMPRAS</text>
                  <text x="201" y="280" font-family="sans-serif" font-size="16" font-weight="bold" fill="#059669">$295.000</text>
                  <text x="201" y="296" font-family="sans-serif" font-size="8.5" fill="#064e3b">Facturas Proveedores (33)</text>

                  <rect x="346" y="238" width="142" height="68" rx="8" fill="#eff6ff" stroke="#bfdbfe" />
                  <text x="358" y="257" font-family="sans-serif" font-size="9.5" font-weight="bold" fill="#1e40af">⚖️ UTILIDAD BRUTA PMP</text>
                  <text x="358" y="280" font-family="sans-serif" font-size="16" font-weight="bold" fill="#1d4ed8">$840.500</text>
                  <text x="358" y="296" font-family="sans-serif" font-size="8.5" fill="#1e3a8a">Rentabilidad real del mes</text>
                </svg>
              </div>
            </div>
          </div>
        </div>
      </section>

      <!-- SECCIÓN DE PROCESOS ILUSTRADOS -->
      <section class="processes-section">
        <div class="section-container">
          <div class="section-heading">
            <span class="section-tag">Flujo Operativo Intuitivo</span>
            <h2>¿Cómo funciona MicroSaaS ERP en tu negocio?</h2>
            <p>
              Todo el ciclo comercial, contable y tributario conectado automáticamente en 4 etapas simples:
            </p>
          </div>

          <div class="process-grid">
            <!-- Etapa 1 -->
            <article class="process-card">
              <div class="process-illustration">
                <svg viewBox="0 0 260 130" xmlns="http://www.w3.org/2000/svg">
                  <rect width="260" height="130" rx="10" fill="#ecfdf5" />
                  <!-- Icono Planilla Excel -->
                  <rect x="28" y="25" width="76" height="80" rx="8" fill="#16a34a" />
                  <text x="66" y="58" font-family="sans-serif" font-size="16" font-weight="bold" fill="#ffffff" text-anchor="middle">XLSX</text>
                  <line x1="40" y1="70" x2="92" y2="70" stroke="#bbf7d0" stroke-width="3" />
                  <line x1="40" y1="80" x2="92" y2="80" stroke="#bbf7d0" stroke-width="3" />
                  <line x1="40" y1="90" x2="78" y2="90" stroke="#bbf7d0" stroke-width="3" />
                  <!-- Flecha -->
                  <path d="M118 65 L144 65 M138 58 L146 65 L138 72" stroke="#059669" stroke-width="3" fill="none" stroke-linecap="round" />
                  <!-- Tarjeta Kardex -->
                  <rect x="156" y="25" width="76" height="80" rx="8" fill="#ffffff" stroke="#10b981" stroke-width="2" />
                  <text x="194" y="50" font-size="22" text-anchor="middle">📦</text>
                  <text x="194" y="72" font-family="sans-serif" font-size="10" font-weight="bold" fill="#065f46" text-anchor="middle">Kardex PMP</text>
                  <text x="194" y="88" font-family="sans-serif" font-size="9" fill="#047857" text-anchor="middle">Stock Activo</text>
                </svg>
              </div>
              <div class="process-step-badge">Paso 1</div>
              <h3>Carga Inicial Excel y Kardex PMP</h3>
              <p>
                Descarga la planilla oficial de <strong>Microsoft Excel (.xlsx)</strong> en blanco, escribe tus productos y súbela en un clic. El sistema valoriza tu bodega con <strong>Precio Medio Ponderado (PMP)</strong> y sugiere precios según tu margen.
              </p>
            </article>

            <!-- Etapa 2 -->
            <article class="process-card">
              <div class="process-illustration">
                <svg viewBox="0 0 260 130" xmlns="http://www.w3.org/2000/svg">
                  <rect width="260" height="130" rx="10" fill="#eff6ff" />
                  <rect x="26" y="22" width="110" height="86" rx="8" fill="#ffffff" stroke="#3b82f6" stroke-width="2" />
                  <text x="81" y="48" font-size="20" text-anchor="middle">🖥️</text>
                  <text x="81" y="68" font-family="sans-serif" font-size="10" font-weight="bold" fill="#1e3a8a" text-anchor="middle">Terminal POS</text>
                  <rect x="38" y="78" width="86" height="18" rx="4" fill="#2563eb" />
                  <text x="81" y="90" font-family="sans-serif" font-size="8.5" font-weight="bold" fill="#ffffff" text-anchor="middle">Cobrar Venta</text>
                  <!-- Flecha -->
                  <path d="M144 65 L164 65 M158 58 L166 65 L158 72" stroke="#2563eb" stroke-width="3" fill="none" stroke-linecap="round" />
                  <!-- Arqueo Caja -->
                  <rect x="172" y="28" width="64" height="74" rx="8" fill="#dbeafe" />
                  <text x="204" y="56" font-size="20" text-anchor="middle">💵</text>
                  <text x="204" y="75" font-family="sans-serif" font-size="9.5" font-weight="bold" fill="#1d4ed8" text-anchor="middle">Cuadratura</text>
                  <text x="204" y="88" font-family="sans-serif" font-size="8.5" fill="#1e40af" text-anchor="middle">de Caja</text>
                </svg>
              </div>
              <div class="process-step-badge">Paso 2</div>
              <h3>Punto de Venta (POS) y Control de Caja</h3>
              <p>
                Abre tu turno de caja con fondo inicial, procesa pagos en <strong>Efectivo, Tarjeta o Mixto</strong> con cálculo automático de vuelto y realiza el arqueo ciego de cierre al terminar el día.
              </p>
            </article>

            <!-- Etapa 3 -->
            <article class="process-card">
              <div class="process-illustration">
                <svg viewBox="0 0 260 130" xmlns="http://www.w3.org/2000/svg">
                  <rect width="260" height="130" rx="10" fill="#f5f3ff" />
                  <rect x="28" y="20" width="90" height="90" rx="8" fill="#ffffff" stroke="#7c3aed" stroke-width="2" />
                  <text x="73" y="45" font-size="18" text-anchor="middle">🧾</text>
                  <text x="73" y="62" font-family="sans-serif" font-size="9.5" font-weight="bold" fill="#4c1d95" text-anchor="middle">Boleta (39)</text>
                  <text x="73" y="76" font-family="sans-serif" font-size="9.5" font-weight="bold" fill="#4c1d95" text-anchor="middle">Factura (33)</text>
                  <rect x="40" y="84" width="66" height="16" rx="4" fill="#ede9fe" />
                  <text x="73" y="95" font-family="monospace" font-size="8" fill="#5b21b6" text-anchor="middle">&lt;TED Firma/&gt;</text>
                  <!-- Sello SII -->
                  <circle cx="190" cy="65" r="38" fill="#7c3aed" />
                  <text x="190" y="61" font-family="sans-serif" font-size="11" font-weight="bold" fill="#ffffff" text-anchor="middle">DTE SII</text>
                  <text x="190" y="76" font-family="sans-serif" font-size="9" fill="#ddd6fe" text-anchor="middle">CAF + NC 61</text>
                </svg>
              </div>
              <div class="process-step-badge">Paso 3</div>
              <h3>Facturación SII y Notas de Crédito (61)</h3>
              <p>
                Elige en el mismo cobro entre <strong>Boleta (39)</strong> o <strong>Factura Electrónica (33)</strong> validando RUT. Si un cliente devuelve productos, emite una <strong>Nota de Crédito (61)</strong> que reingresa el stock a bodega.
              </p>
            </article>

            <!-- Etapa 4 -->
            <article class="process-card">
              <div class="process-illustration">
                <svg viewBox="0 0 260 130" xmlns="http://www.w3.org/2000/svg">
                  <rect width="260" height="130" rx="10" fill="#fffbeb" />
                  <rect x="24" y="22" width="212" height="86" rx="8" fill="#ffffff" stroke="#f59e0b" stroke-width="2" />
                  <!-- Barras de gráfico -->
                  <rect x="44" y="68" width="22" height="26" rx="3" fill="#93c5fd" />
                  <rect x="74" y="50" width="22" height="44" rx="3" fill="#3b82f6" />
                  <rect x="104" y="36" width="22" height="58" rx="3" fill="#10b981" />
                  <!-- Texto F29 -->
                  <text x="142" y="52" font-family="sans-serif" font-size="11" font-weight="bold" fill="#0f172a">Balance F29</text>
                  <text x="142" y="68" font-family="sans-serif" font-size="9.5" fill="#059669" font-weight="bold">✓ Utilidad PMP</text>
                  <text x="142" y="84" font-family="sans-serif" font-size="9.5" fill="#d97706" font-weight="bold">⚖️ IVA al Día</text>
                </svg>
              </div>
              <div class="process-step-badge">Paso 4</div>
              <h3>Dashboard Ejecutivo y Formulario 29</h3>
              <p>
                Monitorea tus ventas del día y del mes, la <strong>Utilidad Bruta Real</strong> descontando el costo PMP, las alertas de stock crítico y el pre-balance mensual de <strong>IVA Débito vs IVA Crédito</strong>.
              </p>
            </article>
          </div>
        </div>
      </section>

      <!-- BANNER INFERIOR DE ACCESO RÁPIDO -->
      <section class="bottom-cta-section">
        <div class="bottom-cta-card">
          <div>
            <h2>¿Listo para comenzar a operar con tu Pyme?</h2>
            <p>Selecciona tu perfil de ingreso para continuar:</p>
          </div>
          <div class="bottom-cta-actions">
            <a routerLink="/onboarding" class="btn-bottom-subscribe">
              🚀 1. Suscripción (Clientes Nuevos)
            </a>
            <a routerLink="/login" class="btn-bottom-login">
              🔐 2. Acceso a Usuarios
            </a>
          </div>
        </div>
      </section>
    </div>
  `,
  styles: [
    `
      .landing-page {
        min-height: calc(100vh - 64px);
        background: #f8fafc;
        color: #0f172a;
      }

      /* HERO */
      .hero-section {
        background: linear-gradient(180deg, #ffffff 0%, #f1f5f9 100%);
        padding: 3.25rem 1.5rem 3.5rem;
        border-bottom: 1px solid #e2e8f0;
      }
      .hero-container {
        max-width: 1240px;
        margin: 0 auto;
        display: grid;
        grid-template-columns: 1.08fr 0.92fr;
        gap: 2.5rem;
        align-items: center;
      }
      @media (max-width: 992px) {
        .hero-container {
          grid-template-columns: 1fr;
        }
      }
      .hero-badge {
        display: inline-block;
        background: #eff6ff;
        color: #1d4ed8;
        border: 1px solid #bfdbfe;
        padding: 0.35rem 0.85rem;
        border-radius: 9999px;
        font-size: 0.82rem;
        font-weight: 700;
        margin-bottom: 1rem;
      }
      .hero-text h1 {
        margin: 0 0 1rem;
        font-size: 2.25rem;
        line-height: 1.2;
        font-weight: 800;
        color: #0f172a;
      }
      .hero-text h1 .highlight {
        color: #2563eb;
      }
      .hero-description {
        margin: 0 0 1.75rem;
        font-size: 1.02rem;
        line-height: 1.6;
        color: #475569;
      }

      /* BOTONES PRINCIPALES (SUSCRIPCIÓN Y ACCESO A USUARIOS) */
      .cta-buttons-group {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
        gap: 1rem;
        margin-bottom: 1.5rem;
      }
      .btn-cta {
        display: flex;
        align-items: center;
        gap: 0.9rem;
        padding: 1rem 1.25rem;
        border-radius: 12px;
        text-decoration: none;
        transition: transform 0.15s ease, box-shadow 0.15s ease;
      }
      .btn-cta:hover {
        transform: translateY(-2px);
      }
      .btn-subscribe {
        background: linear-gradient(135deg, #16a34a 0%, #15803d 100%);
        color: #ffffff;
        box-shadow: 0 10px 20px -5px rgba(22, 163, 74, 0.35);
      }
      .btn-login {
        background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%);
        color: #ffffff;
        box-shadow: 0 10px 20px -5px rgba(37, 99, 235, 0.35);
      }
      .btn-icon-circle {
        width: 44px;
        height: 44px;
        border-radius: 10px;
        background: rgba(255, 255, 255, 0.2);
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 1.35rem;
        flex-shrink: 0;
      }
      .btn-text-block {
        display: flex;
        flex-direction: column;
      }
      .btn-text-block strong {
        font-size: 1.05rem;
        font-weight: 800;
        letter-spacing: 0.01em;
      }
      .btn-text-block small {
        font-size: 0.8rem;
        opacity: 0.92;
        margin-top: 0.15rem;
      }

      .active-session-banner {
        display: flex;
        align-items: center;
        justify-content: space-between;
        flex-wrap: wrap;
        gap: 0.5rem;
        background: #ecfdf5;
        border: 1px solid #6ee7b7;
        color: #065f46;
        padding: 0.65rem 1rem;
        border-radius: 8px;
        font-size: 0.88rem;
        font-weight: 600;
        margin-bottom: 1.25rem;
      }
      .link-dashboard {
        color: #047857;
        font-weight: 800;
        text-decoration: none;
      }
      .link-dashboard:hover {
        text-decoration: underline;
      }

      .trust-metrics {
        display: flex;
        align-items: center;
        flex-wrap: wrap;
        gap: 1.25rem;
        padding-top: 1rem;
        border-top: 1px solid #e2e8f0;
      }
      .metric-item {
        display: flex;
        flex-direction: column;
      }
      .metric-item strong {
        font-size: 0.95rem;
        color: #0f172a;
      }
      .metric-item span {
        font-size: 0.78rem;
        color: #64748b;
      }
      .metric-divider {
        width: 1px;
        height: 28px;
        background: #cbd5e1;
      }

      /* MOCKUP ILUSTRADO */
      .mockup-window {
        background: #ffffff;
        border-radius: 14px;
        border: 1px solid #cbd5e1;
        box-shadow: 0 20px 35px -10px rgba(15, 23, 42, 0.12);
        overflow: hidden;
      }
      .mockup-topbar {
        background: #0f172a;
        padding: 0.65rem 1rem;
        display: flex;
        align-items: center;
        gap: 0.45rem;
      }
      .dot {
        width: 10px;
        height: 10px;
        border-radius: 50%;
      }
      .dot-red {
        background: #ef4444;
      }
      .dot-amber {
        background: #f59e0b;
      }
      .dot-green {
        background: #10b981;
      }
      .mockup-title {
        color: #cbd5e1;
        font-size: 0.78rem;
        font-weight: 600;
        margin-left: 0.5rem;
      }
      .mockup-body {
        padding: 0.75rem;
        background: #f8fafc;
      }
      .hero-svg {
        width: 100%;
        height: auto;
        display: block;
      }

      /* SECCIÓN PROCESOS */
      .processes-section {
        padding: 3.5rem 1.5rem;
      }
      .section-container {
        max-width: 1240px;
        margin: 0 auto;
      }
      .section-heading {
        text-align: center;
        max-width: 680px;
        margin: 0 auto 2.5rem;
      }
      .section-tag {
        display: inline-block;
        background: #dbeafe;
        color: #1e40af;
        font-size: 0.78rem;
        font-weight: 700;
        padding: 0.25rem 0.75rem;
        border-radius: 9999px;
        text-transform: uppercase;
        letter-spacing: 0.04em;
        margin-bottom: 0.5rem;
      }
      .section-heading h2 {
        margin: 0 0 0.5rem;
        font-size: 1.75rem;
        color: #0f172a;
      }
      .section-heading p {
        margin: 0;
        color: #64748b;
        font-size: 0.98rem;
      }

      .process-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(265px, 1fr));
        gap: 1.35rem;
      }
      .process-card {
        background: #ffffff;
        border-radius: 12px;
        border: 1px solid #e2e8f0;
        padding: 1.25rem;
        box-shadow: 0 4px 14px rgba(15, 23, 42, 0.04);
        display: flex;
        flex-direction: column;
      }
      .process-illustration {
        margin-bottom: 1rem;
        border-radius: 8px;
        overflow: hidden;
      }
      .process-illustration svg {
        width: 100%;
        height: auto;
        display: block;
      }
      .process-step-badge {
        align-self: flex-start;
        background: #f1f5f9;
        color: #334155;
        font-size: 0.75rem;
        font-weight: 800;
        padding: 0.2rem 0.65rem;
        border-radius: 6px;
        text-transform: uppercase;
        margin-bottom: 0.5rem;
      }
      .process-card h3 {
        margin: 0 0 0.5rem;
        font-size: 1.08rem;
        color: #0f172a;
      }
      .process-card p {
        margin: 0;
        font-size: 0.88rem;
        line-height: 1.55;
        color: #475569;
      }

      /* BANNER INFERIOR */
      .bottom-cta-section {
        padding: 0 1.5rem 3.5rem;
      }
      .bottom-cta-card {
        max-width: 1240px;
        margin: 0 auto;
        background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%);
        color: #ffffff;
        border-radius: 16px;
        padding: 2rem 2.25rem;
        display: flex;
        justify-content: space-between;
        align-items: center;
        flex-wrap: wrap;
        gap: 1.5rem;
      }
      .bottom-cta-card h2 {
        margin: 0 0 0.35rem;
        font-size: 1.45rem;
      }
      .bottom-cta-card p {
        margin: 0;
        color: #cbd5e1;
        font-size: 0.95rem;
      }
      .bottom-cta-actions {
        display: flex;
        flex-wrap: wrap;
        gap: 0.9rem;
      }
      .btn-bottom-subscribe {
        background: #16a34a;
        color: #ffffff;
        text-decoration: none;
        padding: 0.8rem 1.35rem;
        border-radius: 10px;
        font-weight: 700;
        font-size: 0.95rem;
      }
      .btn-bottom-subscribe:hover {
        background: #15803d;
      }
      .btn-bottom-login {
        background: #2563eb;
        color: #ffffff;
        text-decoration: none;
        padding: 0.8rem 1.35rem;
        border-radius: 10px;
        font-weight: 700;
        font-size: 0.95rem;
      }
      .btn-bottom-login:hover {
        background: #1d4ed8;
      }
    `
  ]
})
export class LandingComponent {
  readonly authService = inject(AuthService);
}

