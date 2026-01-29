import { Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { I18NextPipe } from 'angular-i18next';
import { LoggingService } from '../../services/logging.service';
import { SettingsService } from '../../services/settings.service';
import { SystemService } from '../../services/system.service';

@Component({
  selector: 'app-about',
  templateUrl: './about.component.html',
  imports: [I18NextPipe],
})
export class AboutComponent {
  private settingsService = inject(SettingsService);
  private loggingService = inject(LoggingService);
  private updateService = inject(SystemService);

  isUpdating = signal(false);
  updateError = signal<string | undefined>(undefined);
  updatedDismissed = signal(false);

  applicationInfo = rxResource({
    stream: () => this.settingsService.getApplicationInfo(),
  });

  updateInfo = rxResource({
    stream: () => this.updateService.checkForUpdates(),
  });

  installUpdate() {
    const updateInfo = this.updateInfo.value();
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
          this.updatedDismissed.set(true);
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
    const updateInfo = this.updateInfo.value();
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

  // UI States for Clipboard feedback
  copyStates: { [key: string]: boolean } = {};
  async copyToClipboard(text?: string): Promise<void> {
    if (!text) return;

    try {
      await navigator.clipboard.writeText(text);

      // Trigger visual feedback (e.g. change icon to checkmark)
      this.copyStates[text] = true;

      // Reset after 2 seconds
      setTimeout(() => {
        this.copyStates[text] = false;
      }, 2000);
    } catch (err) {
      console.error('Failed to copy: ', err);
    }
  }
}
