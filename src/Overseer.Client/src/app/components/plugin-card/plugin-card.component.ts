import { Component, input, output } from '@angular/core';
import { I18NextPipe } from 'angular-i18next';
import { PluginInfo } from '../../models/plugin-info.model';

@Component({
  selector: 'app-plugin-card',
  templateUrl: './plugin-card.component.html',
  imports: [I18NextPipe],
})
export class PluginCardComponent {
  plugin = input.required<PluginInfo>();
  selected = input<boolean>(false);
  selectable = input<boolean>(false);

  selectionChange = output<boolean>();

  protected toggleSelection(): void {
    if (this.selectable()) {
      this.selectionChange.emit(!this.selected());
    }
  }
}
