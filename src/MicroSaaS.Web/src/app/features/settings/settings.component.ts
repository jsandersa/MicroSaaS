import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ConfigService } from '../../core/services/config.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatSnackBarModule
  ],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.css'
})
export class SettingsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly configService = inject(ConfigService);
  private readonly snackBar = inject(MatSnackBar);

  readonly isLoading = signal(false);
  readonly isSaving = signal(false);
  readonly statusMessage = signal<string | null>(null);
  readonly hasError = signal(false);

  // Estado de carga del Certificado Digital SII
  readonly selectedFile = signal<File | null>(null);
  readonly isUploadingCertificate = signal(false);
  readonly certificatePasswordControl = this.fb.nonNullable.control('', [Validators.required]);

  readonly settingsForm = this.fb.group({
    taxPercentage: this.fb.nonNullable.control(19.0, [Validators.required, Validators.min(0), Validators.max(100)]),
    currencyCode: this.fb.nonNullable.control('CLP', [Validators.required, Validators.minLength(3), Validators.maxLength(3)]),
    currencySymbol: this.fb.nonNullable.control('$', [Validators.required, Validators.maxLength(10)]),
    timeZoneId: this.fb.nonNullable.control('America/Santiago', [Validators.required, Validators.maxLength(100)]),
    receiptFooterText: this.fb.control<string | null>(null, [Validators.maxLength(500)])
  });

  ngOnInit(): void {
    this.loadConfiguration();
  }

  loadConfiguration(): void {
    this.isLoading.set(true);
    this.statusMessage.set(null);

    this.configService.getConfiguration().subscribe({
      next: (config) => {
        this.settingsForm.patchValue({
          taxPercentage: config.taxPercentage,
          currencyCode: config.currencyCode,
          currencySymbol: config.currencySymbol,
          timeZoneId: config.timeZoneId,
          receiptFooterText: config.receiptFooterText ?? null
        });
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.hasError.set(true);
        this.statusMessage.set('No se pudo cargar la configuración actual del Tenant.');
      }
    });
  }

  onSaveSettings(): void {
    if (this.settingsForm.invalid) {
      return;
    }

    const raw = this.settingsForm.getRawValue();
    this.isSaving.set(true);
    this.statusMessage.set(null);
    this.hasError.set(false);

    this.configService.updateConfiguration({
      taxPercentage: raw.taxPercentage,
      currencyCode: raw.currencyCode.toUpperCase(),
      currencySymbol: raw.currencySymbol,
      timeZoneId: raw.timeZoneId,
      receiptFooterText: raw.receiptFooterText
    }).subscribe({
      next: (updated) => {
        this.settingsForm.patchValue({
          taxPercentage: updated.taxPercentage,
          currencyCode: updated.currencyCode,
          currencySymbol: updated.currencySymbol,
          timeZoneId: updated.timeZoneId,
          receiptFooterText: updated.receiptFooterText ?? null
        });
        this.isSaving.set(false);
        this.hasError.set(false);
        this.statusMessage.set('Configuración guardada exitosamente.');
      },
      error: () => {
        this.isSaving.set(false);
        this.hasError.set(true);
        this.statusMessage.set('Error al guardar los parámetros de configuración.');
      }
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;

    if (!file) {
      this.selectedFile.set(null);
      return;
    }

    const lowerName = file.name.toLowerCase();
    if (!lowerName.endsWith('.pfx') && !lowerName.endsWith('.p12')) {
      this.selectedFile.set(null);
      input.value = '';
      this.snackBar.open('Formato no válido. Selecciona un archivo .pfx o .p12.', 'Cerrar', {
        duration: 4500
      });
      return;
    }

    this.selectedFile.set(file);
  }

  onUploadCertificate(fileInput: HTMLInputElement): void {
    const file = this.selectedFile();
    const password = this.certificatePasswordControl.value;

    if (!file || this.certificatePasswordControl.invalid) {
      return;
    }

    this.isUploadingCertificate.set(true);

    this.configService.uploadCertificate(file, password).subscribe({
      next: () => {
        this.isUploadingCertificate.set(false);
        this.selectedFile.set(null);
        fileInput.value = '';
        this.certificatePasswordControl.reset('');

        this.snackBar.open(
          'Certificado Digital SII validado, cifrado y almacenado con éxito.',
          'OK',
          { duration: 5000 }
        );
      },
      error: (err) => {
        this.isUploadingCertificate.set(false);
        const backendMsg = err?.error?.message
          ?? 'Contraseña incorrecta o archivo de certificado (.pfx/.p12) inválido.';

        this.snackBar.open(backendMsg, 'Cerrar', {
          duration: 6000
        });
      }
    });
  }
}
