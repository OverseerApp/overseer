import { CommonModule } from '@angular/common';
import { Component, signal } from '@angular/core';
import { I18NextPipe } from 'angular-i18next';

@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [CommonModule, I18NextPipe],
  templateUrl: './footer.component.html',
  host: { class: 'd-block text-center' },
})
export class FooterComponent {
  currentYear = signal(new Date().getFullYear());
}
