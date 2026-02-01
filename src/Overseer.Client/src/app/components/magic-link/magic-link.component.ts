import { Component, effect, inject, input, signal } from '@angular/core';
import { I18NextPipe } from 'angular-i18next';
import { User } from '../../models/user.model';
import { AuthenticationService } from '../../services/authentication.service';
import { ToastsService } from '../../services/toast.service';
import { CardSectionComponent } from '../card-section/card-section.component';

@Component({
  selector: 'app-magic-link',
  templateUrl: './magic-link.component.html',
  imports: [CardSectionComponent, I18NextPipe],
})
export class MagicLinkComponent {
  user = input<User>();
  autoGenerate = input<boolean>(false);

  private authenticationService = inject(AuthenticationService);
  private toastsService = inject(ToastsService);

  protected generatedUrl = signal<string>('');

  constructor() {
    const initEffect = effect(() => {
      if (this.autoGenerate() && this.user()) {
        this.generatePreAuthentication();
        initEffect.destroy();
      }
    });
  }

  generatePreAuthentication() {
    const user = this.user();
    if (user?.id === null || user?.id === undefined) return;

    this.authenticationService.getPreauthenticatedToken(user.id).subscribe((token) => {
      this.generatedUrl.set(`${window.location.origin}/sso?token=${encodeURIComponent(token)}`);
    });
  }

  copyToClipboard(input: HTMLInputElement) {
    if (!input.value) return;

    try {
      if (!navigator.clipboard?.writeText) throw new Error('Clipboard API not available');
      navigator.clipboard.writeText(input.value);
    } catch (error) {
      input.select();
      try {
        document.execCommand('copy');
      } catch {
        return;
      }
      input.setSelectionRange(0, 0);
    }
    setTimeout(() => this.generatedUrl.set(''), 5000);
    this.toastsService.show({ message: 'copiedToClipboard', type: 'success', delay: 3000 });
  }
}
