import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { I18NextPipe } from 'angular-i18next';
import { forkJoin } from 'rxjs';
import { MachineMonitorComponent } from '../../components/machine-monitor/machine-monitor.component';
import { isIdle, MachineStatus } from '../../models/machine-status.model';
import { Machine } from '../../models/machine.model';
import { ApplicationSettings } from '../../models/settings.model';
import { AuthenticationService } from '../../services/authentication.service';
import { MachinesService } from '../../services/machines.service';
import { MonitoringService } from '../../services/monitoring.service';
import { SettingsService } from '../../services/settings.service';

@Component({
  selector: 'app-monitoring',
  templateUrl: './monitoring.component.html',
  styleUrls: ['./monitoring.component.scss'],
  imports: [MachineMonitorComponent, I18NextPipe, RouterLink],
})
export class MonitoringComponent {
  private destroyRef = inject(DestroyRef);
  private machineService = inject(MachinesService);
  private settingsService = inject(SettingsService);
  private monitoringService = inject(MonitoringService);
  private authenticationService = inject(AuthenticationService);

  protected isAdmin = computed(() => this.authenticationService.activeUser()?.accessLevel === 'Administrator');

  loading = signal(true);
  machines = signal<Machine[] | undefined>(undefined);
  settings = signal<ApplicationSettings | undefined>(undefined);
  statuses = signal<Record<number, MachineStatus | undefined>>({});

  displayedMachines = computed(() => {
    const machines = this.machines();
    const settings = this.settings();
    const statuses = this.statuses();

    if (!machines) return;
    if (!settings) return;

    if (!settings.sortByTimeRemaining) {
      machines.sort((a, b) => a.sortIndex - b.sortIndex);
    } else {
      machines.sort((a, b) => {
        const defaultSort = a.sortIndex - b.sortIndex;
        const aStatus = statuses[a.id];
        const bStatus = statuses[b.id];

        if (!aStatus && !bStatus) return defaultSort;
        if (!aStatus) return 1;
        if (!bStatus) return -1;

        if (isIdle(aStatus.state) && isIdle(bStatus.state)) return defaultSort;
        if (isIdle(aStatus.state)) return 1;
        if (isIdle(bStatus.state)) return -1;

        var remainingA = aStatus?.estimatedTimeRemaining || Infinity;
        var remainingB = bStatus?.estimatedTimeRemaining || Infinity;

        return remainingA - remainingB || defaultSort;
      });
    }

    return machines.filter((machine) => this.isMachineVisible(machine, settings));
  });

  constructor() {
    forkJoin([this.machineService.getMachines(), this.settingsService.getSettings()]).subscribe(([machines, settings]) => {
      this.machines.set(machines);
      this.settings.set(settings);
      // if the machines load too fast there is just an unpleasant flicker of the loading spinner
      setTimeout(() => this.loading.set(false), 250);
    });

    this.monitoringService
      .enableMonitoring()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((status) => {
        this.statuses.update((statuses) => ({ ...statuses, [status.machineId]: status }));
      });
  }

  private isMachineVisible(machine: Machine, settings: ApplicationSettings): boolean {
    if (settings.hideIdleMachines && isIdle(this.statuses()[machine.id]?.state)) return false;
    if (settings.hideDisabledMachines && machine.disabled) return false;
    return true;
  }
}
