import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { OnboardingService, RegisterTenantCommand } from './onboarding.service';
import { rutValidator } from '../../shared/validators/rut.validator';

type RegisterTenantFormGroup = {
  [K in keyof RegisterTenantCommand]: FormControl<RegisterTenantCommand[K]>;
};

@Component({
  selector: 'app-onboarding',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatIconModule
  ],
  template: `
    <div class="onboarding-wrapper">
      <mat-card class="onboarding-card">
        <mat-card-header>
          <mat-card-title>Registro de Empresa (Onboarding)</mat-card-title>
          <mat-card-subtitle>
            Crea la cuenta de tu Pyme y aprovisiona tu entorno aislado
          </mat-card-subtitle>
        </mat-card-header>

        <mat-card-content>
          <form [formGroup]="onboardingForm" (ngSubmit)="onSubmit()" class="onboarding-form">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Razón Social / Nombre de la Empresa</mat-label>
              <input
                matInput
                formControlName="companyName"
                placeholder="Ej. Comercializadora Sur SpA"
              />
              @if (onboardingForm.controls.companyName.touched && onboardingForm.controls.companyName.hasError('required')) {
                <mat-error>El nombre de la empresa es obligatorio.</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>RUT Empresa</mat-label>
              <input
                matInput
                formControlName="rutEmpresa"
                placeholder="Ej. 77.654.321-K"
              />
              @if (onboardingForm.controls.rutEmpresa.touched && onboardingForm.controls.rutEmpresa.hasError('required')) {
                <mat-error>El RUT de la empresa es obligatorio.</mat-error>
              } @else if (onboardingForm.controls.rutEmpresa.touched && onboardingForm.controls.rutEmpresa.hasError('invalidRut')) {
                <mat-error>El RUT ingresado no es válido (dígito verificador incorrecto).</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Correo del Administrador</mat-label>
              <input
                matInput
                type="email"
                formControlName="adminEmail"
                placeholder="admin@tuempresa.cl"
              />
              @if (onboardingForm.controls.adminEmail.touched && onboardingForm.controls.adminEmail.hasError('required')) {
                <mat-error>El correo electrónico es obligatorio.</mat-error>
              } @else if (onboardingForm.controls.adminEmail.touched && onboardingForm.controls.adminEmail.hasError('email')) {
                <mat-error>Ingresa un formato de correo electrónico válido.</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Contraseña</mat-label>
              <input
                matInput
                type="password"
                formControlName="password"
                placeholder="Mínimo 6 caracteres"
              />
              @if (onboardingForm.controls.password.touched && onboardingForm.controls.password.hasError('required')) {
                <mat-error>La contraseña es obligatoria.</mat-error>
              } @else if (onboardingForm.controls.password.touched && onboardingForm.controls.password.hasError('minlength')) {
                <mat-error>La contraseña debe tener al menos 6 caracteres.</mat-error>
              }
            </mat-form-field>

            @if (errorMessage()) {
              <div class="error-banner">
                <mat-icon>error_outline</mat-icon>
                <span>{{ errorMessage() }}</span>
              </div>
            }

            <div class="actions">
              <button
                mat-flat-button
                color="primary"
                type="submit"
                class="full-width submit-btn"
                [disabled]="onboardingForm.invalid || isLoading()"
              >
                @if (isLoading()) {
                  <mat-spinner diameter="22"></mat-spinner>
                  <span>Aprovisionando entorno...</span>
                } @else {
                  <span>Registrar Pyme</span>
                }
              </button>
            </div>
          </form>
        </mat-card-content>

        <mat-card-actions align="end">
          <a mat-button routerLink="/login">¿Ya tienes cuenta? Inicia sesión</a>
        </mat-card-actions>
      </mat-card>
    </div>
  `,
  styles: [
    `
      .onboarding-wrapper {
        display: flex;
        justify-content: center;
        align-items: center;
        min-height: calc(100vh - 64px);
        padding: 24px;
        background-color: #f5f7fa;
      }

      .onboarding-card {
        width: 100%;
        max-width: 480px;
        padding: 16px;
      }

      .onboarding-form {
        display: flex;
        flex-direction: column;
        gap: 12px;
        margin-top: 16px;
      }

      .full-width {
        width: 100%;
      }

      .error-banner {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 12px;
        border-radius: 6px;
        background-color: #fdecea;
        color: #b71c1c;
        font-size: 14px;
      }

      .submit-btn {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        gap: 10px;
        height: 44px;
      }
    `
  ]
})
export class OnboardingComponent {
  private readonly onboardingService = inject(OnboardingService);
  private readonly router = inject(Router);

  readonly isLoading = signal<boolean>(false);
  readonly errorMessage = signal<string>('');

  readonly onboardingForm = new FormGroup<RegisterTenantFormGroup>({
    companyName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required]
    }),
    rutEmpresa: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, rutValidator]
    }),
    adminEmail: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email]
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(6)]
    })
  });

  onSubmit(): void {
    if (this.onboardingForm.invalid) {
      this.onboardingForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set('');

    const command: RegisterTenantCommand = this.onboardingForm.getRawValue();

    this.onboardingService.registerTenant(command).subscribe({
      next: () => {
        this.isLoading.set(false);
        void this.router.navigateByUrl('/login');
      },
      error: (err) => {
        this.isLoading.set(false);
        const backendMsg =
          err?.error?.message ||
          err?.error?.title ||
          err?.message ||
          'No fue posible completar el registro de la empresa.';
        this.errorMessage.set(backendMsg);
      }
    });
  }
}
