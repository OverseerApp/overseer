import { DecimalPipe, NgClass } from '@angular/common';
import { Component, computed } from '@angular/core';
import { I18NextPipe } from 'angular-i18next';
import { JobFailureNotification } from '../../../models/notifications.model';
import { RelativeDatePipe } from '../../../pipes/relative-date.pipe';
import { DialogService } from '../../../services/dialog.service';
import { JobNotificationComponent } from '../job-notification/job-notification.component';

@Component({
  selector: 'app-job-failure-notification',
  templateUrl: './job-failure-notification.component.html',
  imports: [I18NextPipe, RelativeDatePipe, NgClass, DecimalPipe],
  providers: [DialogService],
})
export class JobFailureNotificationComponent extends JobNotificationComponent {
  protected jobFailureNotification = computed(() => this.notification() as JobFailureNotification);
}
