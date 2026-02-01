import { LOCALE_ID, inject, provideAppInitializer } from '@angular/core';
import { I18NEXT_SERVICE, ITranslationService } from 'angular-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import HttpApi from 'i18next-http-backend';
import { environment } from '../environments/environment';

export function appInit(i18next: ITranslationService) {
  return () =>
    i18next
      .use(HttpApi)
      .use(LanguageDetector)
      .init({
        backend: {
          loadPath: `/i18n/{{lng}}.json?v=${Date.now()}`,
        },
        supportedLngs: ['en'],
        fallbackLng: 'en',
        debug: !environment.production,
        returnEmptyString: false,
        ns: ['translation'],
      });
}

export function localeIdFactory(i18next: ITranslationService) {
  return i18next.language;
}

export const I18N_PROVIDERS = [
  provideAppInitializer(() => {
    const initializerFn = appInit(inject(I18NEXT_SERVICE));
    return initializerFn();
  }),
  {
    provide: LOCALE_ID,
    deps: [I18NEXT_SERVICE],
    useFactory: localeIdFactory,
  },
];
