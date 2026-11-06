import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import {
  CafAuthorizationDto,
  CertificateStatusDto,
  ConfigService
} from '../../core/services/config.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
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

  // Estado de Folios CAF SII
  readonly cafList = signal<CafAuthorizationDto[]>([]);
  readonly isLoadingCaf = signal(false);
  readonly isSavingCaf = signal(false);
  readonly selectedCafXmlName = signal<string | null>(null);
  readonly cafStatusMessage = signal<string | null>(null);
  readonly cafHasError = signal(false);

  readonly cafForm = this.fb.group({
    dteType: this.fb.nonNullable.control(39, [Validators.required]),
    startFolio: this.fb.nonNullable.control(1, [Validators.required, Validators.min(1)]),
    endFolio: this.fb.nonNullable.control(500, [Validators.required, Validators.min(1)]),
    xmlContent: this.fb.control<string | null>(null)
  });

  // Estado de carga del Certificado Digital SII
  readonly certificateStatus = signal<CertificateStatusDto | null>(null);
  readonly selectedFile = signal<File | null>(null);
  readonly isUploadingCertificate = signal(false);
  readonly certificatePasswordControl = this.fb.nonNullable.control('', [Validators.required]);

  readonly settingsForm = this.fb.group({
    taxPercentage: this.fb.nonNullable.control(19.0, [Validators.required, Validators.min(0), Validators.max(100)]),
    defaultMarginPercentage: this.fb.nonNullable.control(30.0, [Validators.required, Validators.min(0), Validators.max(500)]),
    currencyCode: this.fb.nonNullable.control('CLP', [Validators.required, Validators.minLength(3), Validators.maxLength(3)]),
    currencySymbol: this.fb.nonNullable.control('$', [Validators.required, Validators.maxLength(10)]),
    timeZoneId: this.fb.nonNullable.control('America/Santiago', [Validators.required, Validators.maxLength(100)]),
    businessActivity: this.fb.control<string | null>('Comercialización y Distribución de Insumos', [Validators.maxLength(200)]),
    companyAddress: this.fb.control<string | null>('Av. Libertador Bernardo O\'Higgins 1234', [Validators.maxLength(200)]),
    companyCommune: this.fb.control<string | null>('Santiago', [Validators.maxLength(100)]),
    receiptFooterText: this.fb.control<string | null>(null, [Validators.maxLength(500)])
  });

  ngOnInit(): void {
    this.loadConfiguration();
    this.loadCafAuthorizations();
    this.loadCertificateStatus();
  }

  loadConfiguration(): void {
    this.isLoading.set(true);
    this.statusMessage.set(null);

    this.configService.getConfiguration().subscribe({
      next: (config) => {
        this.settingsForm.patchValue({
          taxPercentage: config.taxPercentage,
          defaultMarginPercentage: config.defaultMarginPercentage ?? 30.0,
          currencyCode: config.currencyCode,
          currencySymbol: config.currencySymbol,
          timeZoneId: config.timeZoneId,
          businessActivity: config.businessActivity ?? 'Comercialización y Distribución de Insumos',
          companyAddress: config.companyAddress ?? 'Av. Libertador Bernardo O\'Higgins 1234',
          companyCommune: config.companyCommune ?? 'Santiago',
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

  loadCafAuthorizations(): void {
    this.isLoadingCaf.set(true);
    this.configService.getCafAuthorizations().subscribe({
      next: (items) => {
        this.cafList.set(items ?? []);
        this.isLoadingCaf.set(false);
      },
      error: () => {
        this.cafList.set([]);
        this.isLoadingCaf.set(false);
      }
    });
  }

  loadCertificateStatus(): void {
    this.configService.getCertificateStatus().subscribe({
      next: (status) => this.certificateStatus.set(status),
      error: () => this.certificateStatus.set(null)
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
      defaultMarginPercentage: raw.defaultMarginPercentage,
      currencyCode: raw.currencyCode.toUpperCase(),
      currencySymbol: raw.currencySymbol,
      timeZoneId: raw.timeZoneId,
      businessActivity: raw.businessActivity,
      companyAddress: raw.companyAddress,
      companyCommune: raw.companyCommune,
      receiptFooterText: raw.receiptFooterText
    }).subscribe({
      next: (updated) => {
        this.settingsForm.patchValue({
          taxPercentage: updated.taxPercentage,
          defaultMarginPercentage: updated.defaultMarginPercentage ?? raw.defaultMarginPercentage,
          currencyCode: updated.currencyCode,
          currencySymbol: updated.currencySymbol,
          timeZoneId: updated.timeZoneId,
          businessActivity: updated.businessActivity ?? raw.businessActivity,
          companyAddress: updated.companyAddress ?? raw.companyAddress,
          companyCommune: updated.companyCommune ?? raw.companyCommune,
          receiptFooterText: updated.receiptFooterText ?? null
        });
        this.isSaving.set(false);
        this.hasError.set(false);
        this.statusMessage.set('Configuración guardada exitosamente (Precios de Venta actualizados según el nuevo margen).');
      },
      error: () => {
        this.isSaving.set(false);
        this.hasError.set(true);
        this.statusMessage.set('Error al guardar los parámetros de configuración.');
      }
    });
  }

  onCafXmlFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;

    if (!file) {
      this.selectedCafXmlName.set(null);
      return;
    }

    this.selectedCafXmlName.set(file.name);
    const reader = new FileReader();
    reader.onload = () => {
      const xmlText = typeof reader.result === 'string' ? reader.result : '';
      this.cafForm.patchValue({ xmlContent: xmlText });

      // Extraer automáticamente <TD>, <D> y <H> si vienen en el XML CAF del SII
      const tdMatch = xmlText.match(/<TD>\s*(\d+)\s*<\/TD>/i);
      const dMatch = xmlText.match(/<D>\s*(\d+)\s*<\/D>/i);
      const hMatch = xmlText.match(/<H>\s*(\d+)\s*<\/H>/i);

      if (tdMatch) {
        this.cafForm.patchValue({ dteType: Number(tdMatch[1]) });
      }
      if (dMatch) {
        this.cafForm.patchValue({ startFolio: Number(dMatch[1]) });
      }
      if (hMatch) {
        this.cafForm.patchValue({ endFolio: Number(hMatch[1]) });
      }
    };
    reader.readAsText(file, 'ISO-8859-1');
  }

  onSaveCaf(cafFileInput?: HTMLInputElement): void {
    if (this.cafForm.invalid || this.isSavingCaf()) {
      return;
    }

    const raw = this.cafForm.getRawValue();
    if (raw.endFolio < raw.startFolio) {
      this.cafHasError.set(true);
      this.cafStatusMessage.set('El Folio Final debe ser mayor o igual al Folio Inicial.');
      return;
    }

    this.isSavingCaf.set(true);
    this.cafStatusMessage.set(null);
    this.cafHasError.set(false);

    this.configService.registerCafAuthorization({
      dteType: Number(raw.dteType),
      startFolio: Number(raw.startFolio),
      endFolio: Number(raw.endFolio),
      xmlContent: raw.xmlContent
    }).subscribe({
      next: (res) => {
        this.isSavingCaf.set(false);
        this.cafHasError.set(false);
        this.cafStatusMessage.set(res.message || 'Rango CAF autorizado y activado exitosamente.');
        this.selectedCafXmlName.set(null);
        this.cafForm.patchValue({ xmlContent: null });
        if (cafFileInput) {
          cafFileInput.value = '';
        }
        this.loadCafAuthorizations();
      },
      error: (err) => {
        this.isSavingCaf.set(false);
        this.cafHasError.set(true);
        this.cafStatusMessage.set(err?.error?.message ?? 'No se pudo registrar el rango de folios CAF.');
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
        this.loadCertificateStatus();

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
