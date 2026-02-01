import { Component, computed, effect, inject, Renderer2 } from '@angular/core';
import { isActive, IsActiveMatchOptions, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { I18NextPipe } from 'angular-i18next';
import { NgProgressbar } from 'ngx-progressbar';
import { NgProgressHttp } from 'ngx-progressbar/http';
import { NotificationsComponent } from './components/notifications/notifications.component';
import { SvgComponent } from './components/svg/svg.component';
import { ToastsComponent } from './components/toasts/toasts.component';
import { AuthenticationService } from './services/authentication.service';
import { ThemeService } from './services/theme.service';

@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet,
    NgProgressbar,
    NgProgressHttp,
    SvgComponent,
    I18NextPipe,
    RouterLink,
    RouterLinkActive,
    ToastsComponent,
    NotificationsComponent,
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {
  private router = inject(Router);
  private renderer = inject(Renderer2);
  private themeService = inject(ThemeService);
  private authenticationService = inject(AuthenticationService);

  isLoggedIn = computed(() => !!this.authenticationService.activeUser());
  isAdmin = computed(() => this.authenticationService.activeUser()?.accessLevel === 'Administrator');
  isUser = computed(() => this.authenticationService.activeUser()?.accessLevel === 'User');
  isHeaderHidden = computed(() => {
    const options: IsActiveMatchOptions = { paths: 'exact', queryParams: 'ignored', fragment: 'ignored', matrixParams: 'ignored' };
    const isLoginActive = isActive('/login', this.router, options);
    const isSetupActive = isActive('/setup', this.router, options);
    const isSsoActive = isActive('/sso', this.router, options);

    return isLoginActive() || isSetupActive() || isSsoActive();
  });

  constructor() {
    effect(() => {
      const theme = this.themeService.theme();
      let scheme = this.themeService.scheme();

      if (scheme === 'auto') {
        scheme = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
      }
      this.renderer.setAttribute(window.document.body, 'data-bs-theme', scheme);

      if (theme) {
        this.renderer.removeClass(window.document.body, theme);
      }
    });
  }

  goHome(isSettingsRoute: boolean): void {
    if (isSettingsRoute) {
      this.router.navigate(['/']);
    }
  }

  logout(): void {
    this.authenticationService.logout().subscribe(() => this.router.navigate(['/login']));
  }
}
