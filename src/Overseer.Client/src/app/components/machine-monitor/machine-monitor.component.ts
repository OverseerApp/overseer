import { CommonModule } from '@angular/common';
import { Component, computed, DestroyRef, effect, ElementRef, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { NgbProgressbarModule } from '@ng-bootstrap/ng-bootstrap';
import { I18NextPipe } from 'angular-i18next';
import { filter, Observable } from 'rxjs';
import { isIdle, MachineStatus } from '../../models/machine-status.model';
import { Machine } from '../../models/machine.model';
import { DurationPipe } from '../../pipes/duration.pipe';
import { AuthenticationService } from '../../services/authentication.service';
import { ControlService } from '../../services/control.service';
import { DialogService } from '../../services/dialog.service';
import { MonitoringService } from '../../services/monitoring.service';
import { WebCamPanelComponent } from '../web-cam-panel/web-cam-panel.component';

@Component({
  selector: 'app-machine-monitor',
  templateUrl: './machine-monitor.component.html',
  styleUrls: ['./machine-monitor.component.scss'],
  imports: [CommonModule, I18NextPipe, DurationPipe, NgbProgressbarModule, RouterLink, WebCamPanelComponent],
  providers: [DialogService],
})
export class MachineMonitorComponent {
  private host = inject(ElementRef);
  private destroy = inject(DestroyRef);
  private dialogService = inject(DialogService);
  private controlService = inject(ControlService);
  private monitoringService = inject(MonitoringService);
  private authenticationService = inject(AuthenticationService);

  supportsFullscreen = document.fullscreenEnabled;
  machine = input<Machine>();
  busy = signal(false);
  fullScreen = signal(false);
  status = signal<MachineStatus | undefined>(undefined);
  idle = computed(() => isIdle(this.status()?.state));
  paused = computed(() => this.status()?.state === 'Paused');
  operational = computed(() => this.status()?.state === 'Operational');
  heaters = computed(() => this.machine()?.tools.filter((tool) => tool.toolType === 'Heater'));
  extruders = computed(() => this.machine()?.tools.filter((tool) => tool.toolType === 'Extruder'));
  isAdmin = computed(() => this.authenticationService.activeUser()?.accessLevel === 'Administrator');

  constructor() {
    effect(() => {
      const machine = this.machine();
      if (!machine) return;

      untracked(() => {
        this.monitoringService
          .enableMonitoring()
          .pipe(takeUntilDestroyed(this.destroy))
          .subscribe((status: MachineStatus) => {
            if (status.machineId !== machine.id) return;
            this.status.set(status);
          });
      });
    });

    effect(() => {
      const isFullscreen = this.fullScreen();
      if (isFullscreen && !document.fullscreenElement) {
        this.host.nativeElement.requestFullscreen();
      } else if (!!document.fullscreenElement) {
        document.exitFullscreen();
      }
    });
  }

  pause(): void {
    const machine = this.machine();
    if (!machine) return;

    this.disableUi(this.controlService.pauseJob(machine.id));
  }

  resume(): void {
    const machine = this.machine();
    if (!machine) return;

    this.disableUi(this.controlService.resumeJob(machine.id));
  }

  cancel(): void {
    const machine = this.machine();
    if (!machine) return;

    this.dialogService
      .prompt({
        titleKey: 'cancelJobTitle',
        messageKey: 'cancelJobMessage',
      })
      .closed.pipe(filter((confirmed) => confirmed))
      .subscribe(() => this.disableUi(this.controlService.cancelJob(machine.id)));
  }

  private disableUi(observable: Observable<void>): void {
    this.busy.set(true);
    observable.subscribe({
      next: () => this.busy.set(false),
      error: () => this.busy.set(false),
    });
  }
}
