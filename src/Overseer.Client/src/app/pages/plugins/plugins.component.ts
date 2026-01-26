import { Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { I18NextPipe } from 'angular-i18next';
import { Observable } from 'rxjs';
import { PluginInfo } from '../../models/plugin-info.model';
import { PluginsService } from '../../services/plugins.service';

@Component({
  selector: 'app-plugins',
  templateUrl: './plugins.component.html',
  imports: [I18NextPipe],
})
export class PluginsComponent {
  private pluginsService = inject(PluginsService);

  protected plugins = rxResource({ stream: () => this.pluginsService.getPlugins() });

  protected installPlugin(pluginInfo: PluginInfo): Observable<boolean> {
    return this.pluginsService.installPlugin(pluginInfo);
    // TODO: restart the app, if supported
  }

  protected uninstallPlugin(pluginInfo: PluginInfo): Observable<boolean> {
    return this.pluginsService.uninstallPlugin(pluginInfo);
    // TODO: restart the app, if supported
  }
}
