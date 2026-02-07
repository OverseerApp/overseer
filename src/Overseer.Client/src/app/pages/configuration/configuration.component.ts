import { CommonModule } from '@angular/common';
import { Component, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { I18NextPipe } from 'angular-i18next';
import { FooterComponent } from '../../components/footer/footer.component';

interface NavItem {
  routerLink: string;
  icon: string;
  label: string;
  exact?: boolean;
}

@Component({
  selector: 'app-configuration',
  templateUrl: './configuration.component.html',
  styleUrls: ['./configuration.component.scss'],
  imports: [CommonModule, I18NextPipe, RouterOutlet, RouterLink, RouterLinkActive, FooterComponent],
  host: { class: 'd-flex flex-1 flex-column' },
})
export class ConfigurationComponent {
  currentYear = signal(new Date().getFullYear());

  navItems: NavItem[] = [
    { routerLink: './', icon: 'bi-gear-fill', label: 'generalSettings', exact: true },
    { routerLink: 'machines', icon: 'bi-cpu-fill', label: 'machines' },
    { routerLink: 'users', icon: 'bi-people-fill', label: 'users' },
    { routerLink: 'plugins', icon: 'bi-puzzle-fill', label: 'plugins' },
    { routerLink: 'about', icon: 'bi-info-circle-fill', label: 'aboutSettings' },
  ];
}
