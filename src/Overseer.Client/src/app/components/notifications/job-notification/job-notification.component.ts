import { NgClass } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { I18NextPipe } from 'angular-i18next';
import { filter } from 'rxjs';
import { JobNotification } from '../../../models/notifications.model';
import { RelativeDatePipe } from '../../../pipes/relative-date.pipe';
import { ControlService } from '../../../services/control.service';
import { DialogService } from '../../../services/dialog.service';
import { MachinesService } from '../../../services/machines.service';
import { NotificationBaseComponent } from '../notification-base.component';

@Component({
  selector: 'app-job-notification',
  templateUrl: './job-notification.component.html',
  imports: [I18NextPipe, RelativeDatePipe, NgClass],
  providers: [DialogService],
})
export class JobNotificationComponent extends NotificationBaseComponent {
  private machinesService = inject(MachinesService);
  private controlService = inject(ControlService);
  private dialogService = inject(DialogService);

  protected jobNotification = computed(() => this.notification() as JobNotification);

  protected machine = computed(() => {
    if (this.machinesService.machines.error()) return undefined;
    if (this.machinesService.machines.isLoading()) return undefined;
    if (!this.machinesService.machines.hasValue()) return undefined;

    const machineId = this.jobNotification()?.machineId;
    return this.machinesService.machines.value().find((m) => m.id === machineId);
  });

  protected handleDismiss() {
    this.notificationService.removeNotification(this.notification().id).subscribe();
  }

  protected pause(): void {
    this.controlService.pauseJob(this.jobNotification().machineId).subscribe();
    this.close.emit();
  }

  protected resume(): void {
    this.controlService.resumeJob(this.jobNotification().machineId).subscribe();
    this.close.emit();
  }

  protected cancel(): void {
    const machineId = this.jobNotification().machineId;

    this.dialogService
      .prompt({
        titleKey: 'cancelJobTitle',
        messageKey: 'cancelJobMessage',
      })
      .closed.pipe(filter((confirmed) => confirmed))
      .subscribe(() => {
        this.controlService.cancelJob(machineId).subscribe();
        this.close.emit();
      });
  }
}
