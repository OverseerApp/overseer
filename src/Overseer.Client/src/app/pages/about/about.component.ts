import { Component, inject, OnInit, signal } from '@angular/core';
import { I18NextPipe } from 'angular-i18next';
import { ApplicationInfo } from '../../models/application-info.model';
import { UpdateInfo } from '../../models/update-info.model';
import { LoggingService } from '../../services/logging.service';
import { SettingsService } from '../../services/settings.service';
import { UpdateService } from '../../services/update.service';

@Component({
  selector: 'app-about',
  templateUrl: './about.component.html',
  imports: [I18NextPipe],
})
export class AboutComponent implements OnInit {
  private settingsService = inject(SettingsService);
  private loggingService = inject(LoggingService);
  private updateService = inject(UpdateService);

  applicationInfo = signal<{ label: string; value?: string }[] | undefined>(undefined);
  currentYear = signal(new Date().getFullYear());
  updateInfo = signal<UpdateInfo | undefined>(undefined);
  isCheckingForUpdates = signal(false);
  isUpdating = signal(false);
  updateError = signal<string | undefined>(undefined);

  ngOnInit() {
    this.settingsService.getApplicationInfo().subscribe((applicationInfo) => {
      this.applicationInfo.set(Object.keys(applicationInfo).map((key) => ({ label: key, value: applicationInfo[key as keyof ApplicationInfo] })));
    });

    this.checkForUpdates();
  }

  checkForUpdates() {
    this.isCheckingForUpdates.set(true);
    this.updateError.set(undefined);

    this.updateService.checkForUpdates().subscribe({
      next: (updateInfo) => {
        this.updateInfo.set(updateInfo);
        this.isCheckingForUpdates.set(false);
      },
      error: (error) => {
        console.error('Failed to check for updates:', error);
        this.isCheckingForUpdates.set(false);
        this.updateError.set('Failed to check for updates');
      },
    });
  }

  installUpdate() {
    const updateInfo = this.updateInfo();
    if (!updateInfo?.latestVersion) {
      return;
    }

    this.isUpdating.set(true);
    this.updateError.set(undefined);

    this.updateService.installUpdate(updateInfo.latestVersion).subscribe({
      next: (result) => {
        if (result.success) {
          // The application will restart, show a message
          this.updateError.set(result.message);
        } else {
          this.updateError.set(result.message || 'Update failed');
        }
        this.isUpdating.set(false);
      },
      error: (error) => {
        console.error('Failed to install update:', error);
        this.updateError.set(error.error?.message || 'Failed to install update');
        this.isUpdating.set(false);
      },
    });
  }

  openReleaseUrl() {
    const updateInfo = this.updateInfo();
    if (updateInfo?.releaseUrl) {
      window.open(updateInfo.releaseUrl, '_blank');
    }
  }

  downloadLog() {
    this.loggingService.download().subscribe((log: string) => {
      const blob = new Blob([log], { type: 'text/plain;charset=utf-8' });
      const link = document.createElement('a');
      link.download = 'overseer.log';
      link.href = URL.createObjectURL(blob);
      link.click();

      URL.revokeObjectURL(link.href);
    });
  }
}
