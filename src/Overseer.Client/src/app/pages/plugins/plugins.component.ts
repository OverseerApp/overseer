import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { I18NextPipe } from 'angular-i18next';
import { forkJoin, tap } from 'rxjs';
import { CardSectionComponent } from '../../components/card-section/card-section.component';
import { PluginCardComponent } from '../../components/plugin-card/plugin-card.component';
import { RestartDialogComponent } from '../../components/restart-dialog/restart-dialog.component';
import { DialogService } from '../../services/dialog.service';
import { PluginsService } from '../../services/plugins.service';

@Component({
  selector: 'app-plugins',
  templateUrl: './plugins.component.html',
  imports: [CardSectionComponent, I18NextPipe, PluginCardComponent],
  providers: [DialogService],
})
export class PluginsComponent {
  private pluginsService = inject(PluginsService);
  private dialogService = inject(DialogService);
  private refresh = signal(false);

  protected busy = signal(false);

  protected plugins = rxResource({
    stream: () => this.pluginsService.getPlugins(this.refresh()).pipe(tap(() => this.refresh.set(false))),
  });

  protected availablePlugins = computed(() => {
    const allPlugins = this.plugins.value() ?? [];
    return allPlugins.filter((p) => !p.isInstalled || p.isUpdateAvailable);
  });

  protected installedPlugins = computed(() => {
    const allPlugins = this.plugins.value() ?? [];
    return allPlugins.filter((p) => p.isInstalled);
  });

  protected selectedAvailable = signal<Set<string>>(new Set());
  protected selectedInstalled = signal<Set<string>>(new Set());

  protected toggleAvailableSelection(pluginName: string): void {
    const current = new Set(this.selectedAvailable());
    if (current.has(pluginName)) {
      current.delete(pluginName);
    } else {
      current.add(pluginName);
    }
    this.selectedAvailable.set(current);
  }

  protected toggleInstalledSelection(pluginName: string): void {
    const current = new Set(this.selectedInstalled());
    if (current.has(pluginName)) {
      current.delete(pluginName);
    } else {
      current.add(pluginName);
    }
    this.selectedInstalled.set(current);
  }

  protected selectAllAvailable(): void {
    const allNames = this.availablePlugins().map((p) => p.name);
    this.selectedAvailable.set(new Set(allNames));
  }

  protected deselectAllAvailable(): void {
    this.selectedAvailable.set(new Set());
  }

  protected selectAllInstalled(): void {
    const allNames = this.installedPlugins().map((p) => p.name);
    this.selectedInstalled.set(new Set(allNames));
  }

  protected deselectAllInstalled(): void {
    this.selectedInstalled.set(new Set());
  }

  protected installSelectedPlugins(): void {
    const selected = this.availablePlugins().filter((p) => this.selectedAvailable().has(p.name));
    if (selected.length === 0) return;

    this.busy.set(true);
    forkJoin(selected.map((plugin) => this.pluginsService.installPlugin(plugin))).subscribe({
      next: () => this.restart(),
    });
  }

  protected uninstallSelectedPlugins(): void {
    const selected = this.installedPlugins().filter((p) => this.selectedInstalled().has(p.name));
    if (selected.length === 0) return;

    this.busy.set(true);
    forkJoin(selected.map((plugin) => this.pluginsService.uninstallPlugin(plugin))).subscribe({
      next: () => this.restart(),
    });
  }

  private restart(): void {
    this.busy.set(false);
    this.plugins.reload();
    this.selectedAvailable.set(new Set());
    this.dialogService.show(RestartDialogComponent, { beforeDismiss: () => false });
  }
}
