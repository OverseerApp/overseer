import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideI18Next } from 'angular-i18next';
import { progressInterceptor } from 'ngx-progressbar/http';
import { overseerInterceptor } from './app.interceptor';
import { routes } from './app.routes';
import { I18N_PROVIDERS } from './app.translations';
import { LocalStorageService } from './services/local-storage.service';
import { ThemeService } from './services/theme.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideHttpClient(withInterceptors([progressInterceptor, overseerInterceptor])),
    I18N_PROVIDERS,
    ThemeService,
    LocalStorageService,
    provideRouter(routes),
    provideZoneChangeDetection({ eventCoalescing: true }),
    // { provide: ErrorHandler, useClass: OverseerErrorHandler },
    provideI18Next(),
  ],
};
