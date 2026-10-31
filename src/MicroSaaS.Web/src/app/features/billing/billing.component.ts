import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-billing',
  standalone: true,
  imports: [MatCardModule, MatButtonModule, MatIconModule],
  template: `
    <div class="billing-container">
      <mat-card class="billing-card">
        <mat-card-header>
          <mat-icon mat-card-avatar color="warn" class="warning-icon">warning</mat-icon>
          <mat-card-title>Suscripción Inactiva o Vencida</mat-card-title>
          <mat-card-subtitle>Acceso temporalmente restringido a tu esquema privado</mat-card-subtitle>
        </mat-card-header>

        <mat-card-content class="billing-content">
          <p>
            Tu suscripción mensual se encuentra vencida, morosa o suspendida.
            Para restablecer el acceso inmediato al módulo de inventario de tu Pyme,
            procesa el pago de tu mensualidad.
          </p>

          @if (errorMessage()) {
            <p class="error-text">{{ errorMessage() }}</p>
          }
        </mat-card-content>

        <mat-card-actions align="end">
          <button
            mat-flat-button
            color="primary"
            type="button"
            [disabled]="isProcessing()"
            (click)="onPaySubscription()">
            {{ isProcessing() ? 'Procesando pago...' : 'Pagar Suscripción y Reactivar Cuenta' }}
          </button>
        </mat-card-actions>
      </mat-card>
    </div>
  `,
  styles: [`
    .billing-container {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: calc(100vh - 64px);
      padding: 24px;
    }
    .billing-card {
      width: 100%;
      max-width: 520px;
      padding: 16px;
    }
    .warning-icon {
      font-size: 36px;
      width: 36px;
      height: 36px;
    }
    .billing-content {
      margin-top: 16px;
      line-height: 1.5;
    }
    .error-text {
      color: #d32f2f;
      font-size: 0.875rem;
      margin-top: 12px;
    }
  `]
})
export class BillingComponent {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly baseUrl = environment.apiUrl.replace(/\/api\/?$/, '');
  private readonly payUrl = `${this.baseUrl}/api/Subscriptions/pay`;

  readonly isProcessing = signal(false);
  readonly errorMessage = signal<string | null>(null);

  onPaySubscription(): void {
    this.isProcessing.set(true);
    this.errorMessage.set(null);

    this.http.post(this.payUrl, {}).subscribe({
      next: () => {
        this.isProcessing.set(false);
        this.router.navigate(['/inventory']);
      },
      error: () => {
        this.isProcessing.set(false);
        this.errorMessage.set('No fue posible procesar el pago. Verifica tu sesión e inténtalo nuevamente.');
      }
    });
  }
}

