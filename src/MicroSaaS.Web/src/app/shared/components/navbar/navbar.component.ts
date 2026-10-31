import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [MatToolbarModule, MatButtonModule, RouterLink, RouterLinkActive],
  template: `
    <mat-toolbar color="primary" class="navbar">
      <span class="brand" routerLink="/">MicroSaaS ERP</span>
      <span class="spacer"></span>
      @if (authService.isAuthenticated()) {
        <a mat-button routerLink="/inventory" routerLinkActive="active-link">Inventario</a>
        <a mat-button routerLink="/settings" routerLinkActive="active-link">Configuración</a>
        <button mat-stroked-button (click)="onLogout()">Cerrar sesión</button>
      } @else {
        <a mat-flat-button routerLink="/login">Iniciar sesión</a>
      }
    </mat-toolbar>
  `,
  styles: [`
    .navbar {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 0 24px;
    }
    .brand {
      font-weight: 600;
      cursor: pointer;
    }
    .spacer {
      flex: 1 1 auto;
    }
    .active-link {
      background: rgba(255, 255, 255, 0.15);
    }
  `]
})
export class NavbarComponent {
  readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  onLogout(): void {
    this.authService.logout();
    void this.router.navigate(['/login']);
  }
}
