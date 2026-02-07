import { Component, inject } from '@angular/core';
import { I18NextPipe } from 'angular-i18next';
import { CardSectionComponent } from '../../components/card-section/card-section.component';
import { ChangePasswordComponent } from '../../components/change-password/change-password.component';
import { FooterComponent } from '../../components/footer/footer.component';
import { HelpComponent } from '../../components/help/help.component';
import { AuthenticationService } from '../../services/authentication.service';
import { ThemeService } from '../../services/theme.service';

@Component({
  selector: 'app-user-settings',
  templateUrl: './user-settings.component.html',
  imports: [CardSectionComponent, ChangePasswordComponent, I18NextPipe, FooterComponent, HelpComponent],
  host: { class: 'd-flex flex-1 flex-column' },
})
export class UserSettingsComponent {
  private authenticationService = inject(AuthenticationService);
  protected themeService = inject(ThemeService);
  protected user = this.authenticationService.activeUser;

  handleSchemeChange(scheme: string): void {
    this.themeService.scheme.set(scheme as 'auto' | 'light' | 'dark');
  }
}
