import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule
  ],
  template: `
    <div class="login-container">
      <mat-card class="login-card">
        <mat-card-header>
          <mat-card-title>🔐 Acceso a Usuarios</mat-card-title>
          <mat-card-subtitle>Ingresa con tu cuenta corporativa suscrita</mat-card-subtitle>
        </mat-card-header>

        <mat-card-content>
          <div style="display:grid;grid-template-columns:repeat(3,1fr);gap:0.45rem;margin:0.5rem 0 1.1rem;">
            <button
              type="button"
              style="background:#eff6ff;color:#1d4ed8;border:1px solid #bfdbfe;padding:0.5rem 0.35rem;border-radius:8px;font-size:0.74rem;font-weight:700;cursor:pointer;"
              (click)="fillDemoCredentials('admin@comercializadora.cl')"
            >
              👤 Administrador
            </button>
            <button
              type="button"
              style="background:#fef3c7;color:#92400e;border:1px solid #fcd34d;padding:0.5rem 0.35rem;border-radius:8px;font-size:0.74rem;font-weight:700;cursor:pointer;"
              (click)="fillDemoCredentials('cajero@comercializadora.cl')"
            >
              🛒 Cajero POS
            </button>
            <button
              type="button"
              style="background:#f0fdf4;color:#15803d;border:1px solid #86efac;padding:0.5rem 0.35rem;border-radius:8px;font-size:0.74rem;font-weight:700;cursor:pointer;"
              (click)="fillDemoCredentials('contador@comercializadora.cl')"
            >
              💼 Contador
            </button>
          </div>

          <form [formGroup]="loginForm" (ngSubmit)="onSubmit()">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Correo electrónico</mat-label>
              <input matInput type="email" formControlName="email" placeholder="admin@pyme.cl" />
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width" style="margin-bottom:4px;">
              <mat-label>Contraseña</mat-label>
              <input
                matInput
                [type]="showPassword() ? 'text' : 'password'"
                formControlName="password"
              />
              <button
                type="button"
                matSuffix
                (click)="showPassword.set(!showPassword())"
                style="background:transparent;border:none;cursor:pointer;padding:0 0.65rem;font-size:0.82rem;color:#475569;font-weight:600;"
                [attr.aria-label]="showPassword() ? 'Ocultar contraseña' : 'Mostrar contraseña'"
              >
                {{ showPassword() ? '🙈 Ocultar' : '👁️ Ver' }}
              </button>
            </mat-form-field>

            <div style="display:flex;justify-content:flex-end;margin-bottom:12px;">
              <button
                type="button"
                (click)="toggleForgotPassword()"
                style="background:transparent;border:none;color:#2563eb;font-size:0.82rem;font-weight:600;cursor:pointer;padding:0;"
              >
                🔑 ¿Olvidaste tu contraseña?
              </button>
            </div>

            @if (errorMessage()) {
              <p class="error-text">{{ errorMessage() }}</p>
            }

            @if (successMessage()) {
              <p style="color:#15803d;background:#f0fdf4;border:1px solid #86efac;padding:0.55rem 0.75rem;border-radius:8px;font-size:0.84rem;font-weight:600;margin:0 0 12px;">
                {{ successMessage() }}
              </p>
            }

            <button
              mat-flat-button
              color="primary"
              type="submit"
              class="full-width"
              [disabled]="loginForm.invalid || isLoading()">
              {{ isLoading() ? 'Autenticando...' : 'Ingresar al Sistema' }}
            </button>
          </form>

          @if (showForgotModal()) {
            <div style="margin-top:1rem;padding:0.95rem;background:#f8fafc;border:1px solid #cbd5e1;border-radius:10px;">
              <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:0.5rem;">
                <strong style="font-size:0.88rem;color:#0f172a;">🔑 Restablecer Contraseña Corporativa</strong>
                <button
                  type="button"
                  (click)="showForgotModal.set(false)"
                  style="background:transparent;border:none;cursor:pointer;color:#64748b;font-weight:700;"
                >
                  ✕
                </button>
              </div>
              <p style="font-size:0.78rem;color:#475569;margin:0 0 0.75rem;">
                Ingresa tu correo registrado y define tu nueva contraseña de acceso:
              </p>
              <form [formGroup]="resetForm" (ngSubmit)="onResetPassword()">
                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Correo de la cuenta</mat-label>
                  <input matInput type="email" formControlName="email" placeholder="usuario@empresa.cl" />
                </mat-form-field>
                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Nueva contraseña (mín. 6 caracteres)</mat-label>
                  <input matInput [type]="showPassword() ? 'text' : 'password'" formControlName="newPassword" />
                </mat-form-field>
                <button
                  mat-flat-button
                  color="primary"
                  type="submit"
                  class="full-width"
                  [disabled]="resetForm.invalid || isResetting()"
                >
                  {{ isResetting() ? 'Actualizando clave...' : 'Guardar Nueva Contraseña' }}
                </button>
              </form>
            </div>
          }

          <div class="login-footer-links">
            <a routerLink="/onboarding" class="link-subscribe">
              🚀 ¿Eres cliente nuevo? Suscríbete y registra tu empresa aquí
            </a>
            <a routerLink="/" class="link-home">
              ← Volver a la Portada Principal
            </a>
          </div>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .login-container {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: calc(100vh - 64px);
      padding: 24px;
      background: #f8fafc;
    }
    .login-card {
      width: 100%;
      max-width: 440px;
      padding: 20px;
      border-radius: 12px;
    }
    .full-width {
      width: 100%;
      margin-bottom: 12px;
    }
    .error-text {
      color: #d32f2f;
      font-size: 0.875rem;
      margin: 0 0 12px;
    }
    .login-footer-links {
      margin-top: 1.25rem;
      padding-top: 1rem;
      border-top: 1px solid #e2e8f0;
      display: flex;
      flex-direction: column;
      gap: 0.6rem;
      text-align: center;
    }
    .link-subscribe {
      color: #15803d;
      font-weight: 700;
      font-size: 0.88rem;
      text-decoration: none;
    }
    .link-subscribe:hover {
      text-decoration: underline;
    }
    .link-home {
      color: #64748b;
      font-size: 0.84rem;
      text-decoration: none;
    }
    .link-home:hover {
      color: #0f172a;
      text-decoration: underline;
    }
  `]
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly isLoading = signal(false);
  readonly isResetting = signal(false);
  readonly showPassword = signal(false);
  readonly showForgotModal = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  readonly loginForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
  });

  readonly resetForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    newPassword: ['', [Validators.required, Validators.minLength(6)]]
  });

  fillDemoCredentials(email: string): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.loginForm.setValue({
      email,
      password: 'Password123!'
    });
  }

  toggleForgotPassword(): void {
    this.errorMessage.set(null);
    this.successMessage.set(null);
    const currentEmail = this.loginForm.controls.email.value;
    if (currentEmail) {
      this.resetForm.patchValue({ email: currentEmail });
    }
    this.showForgotModal.set(!this.showForgotModal());
  }

  onResetPassword(): void {
    if (this.resetForm.invalid || this.isResetting()) {
      return;
    }

    const { email, newPassword } = this.resetForm.getRawValue();
    this.isResetting.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    this.authService.resetPassword(email, newPassword).subscribe({
      next: (res) => {
        this.isResetting.set(false);
        this.showForgotModal.set(false);
        this.loginForm.setValue({ email, password: newPassword });
        this.successMessage.set(res.message);
      },
      error: (err) => {
        this.isResetting.set(false);
        this.errorMessage.set(err?.error?.message || 'No se pudo restablecer la contraseña.');
      }
    });
  }

  onSubmit(): void {
    if (this.loginForm.invalid) {
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    this.authService.login(this.loginForm.getRawValue()).subscribe({
      next: () => {
        this.isLoading.set(false);
        const targetRoute = this.authService.isCashier() ? '/sales' : '/dashboard';
        void this.router.navigateByUrl(targetRoute);
      },
      error: (err) => {
        this.isLoading.set(false);
        if (err?.status === 402) {
          void this.router.navigateByUrl('/billing');
          return;
        }
        this.errorMessage.set('Credenciales inválidas o servicio no disponible.');
      }
    });
  }
}

