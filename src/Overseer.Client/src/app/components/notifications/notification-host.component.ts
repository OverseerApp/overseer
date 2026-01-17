import { Component, ComponentRef, effect, inject, input, output, OutputRefSubscription, ViewContainerRef } from '@angular/core';
import { Notification, NotificationType } from '../../models/notifications.model';
import { JobFailureNotificationComponent } from './job-failure-notification/job-failutre-notification.component';
import { JobNotificationComponent } from './job-notification/job-notification.component';
import { NotificationBaseComponent } from './notification-base.component';
import { SimpleNotificationComponent } from './simple-notification/simple-notification.component';

@Component({
  selector: 'app-notification',
  template: '',
})
export class NotificationHostComponent {
  private componentRef?: ComponentRef<NotificationBaseComponent>;
  private viewContainerRef = inject(ViewContainerRef);
  private closeSubscription?: OutputRefSubscription;

  private notificationTypeMap: Record<NotificationType, typeof NotificationBaseComponent> = {
    Job: JobNotificationComponent,
    Simple: SimpleNotificationComponent,
    JobFailure: JobFailureNotificationComponent,
  };

  notification = input.required<Notification>();
  close = output<void>();

  constructor() {
    effect(() => {
      const notification = this.notification();

      if (this.componentRef) {
        this.closeSubscription?.unsubscribe();
        this.componentRef?.destroy?.();
      }

      this.viewContainerRef.clear();

      if (!notification) return;

      const componentType = this.notificationTypeMap[notification.notificationType];
      this.componentRef = this.viewContainerRef.createComponent<NotificationBaseComponent>(componentType);
      this.componentRef.setInput('notification', notification);
      this.closeSubscription = this.componentRef.instance.close.subscribe(() => this.close.emit());
    });
  }
}
