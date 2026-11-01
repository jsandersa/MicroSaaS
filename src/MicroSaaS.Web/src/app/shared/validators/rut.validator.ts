import { AbstractControl, ValidationErrors } from '@angular/forms';

/**
 * Validador personalizado de Angular que verifica matemáticamente el dígito verificador (Módulo 11)
 * de un RUT chileno (ej. 77.654.321-K o 77654321-K).
 */
export function rutValidator(control: AbstractControl): ValidationErrors | null {
  const rawValue = (control.value ?? '').toString().trim();

  if (!rawValue) {
    return null;
  }

  // Limpiar puntos, espacios y guiones
  const cleanRut = rawValue.replace(/[.\-\s]/g, '').toUpperCase();

  if (!/^\d{7,8}[0-9K]$/.test(cleanRut)) {
    return { invalidRut: true };
  }

  const body = cleanRut.slice(0, -1);
  const providedDv = cleanRut.slice(-1);

  let sum = 0;
  let multiplier = 2;

  for (let i = body.length - 1; i >= 0; i--) {
    sum += parseInt(body.charAt(i), 10) * multiplier;
    multiplier = multiplier === 7 ? 2 : multiplier + 1;
  }

  const remainder = 11 - (sum % 11);
  const expectedDv =
    remainder === 11 ? '0' : remainder === 10 ? 'K' : remainder.toString();

  return providedDv === expectedDv ? null : { invalidRut: true };
}

