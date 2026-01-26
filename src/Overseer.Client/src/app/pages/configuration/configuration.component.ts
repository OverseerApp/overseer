import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { I18NextPipe } from 'angular-i18next';

@Component({
  selector: 'app-configuration',
  templateUrl: './configuration.component.html',
  styleUrls: ['./configuration.component.scss'],
  imports: [I18NextPipe, RouterOutlet, RouterLink, RouterLinkActive],
})
export class ConfigurationComponent {}
