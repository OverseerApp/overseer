import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { I18NextPipe } from 'angular-i18next';
import { LoggingService } from '../../services/logging.service';

@Component({
  selector: 'app-help',
  templateUrl: './help.component.html',
  imports: [CommonModule, I18NextPipe],
})
export class HelpComponent {
  private loggingService = inject(LoggingService);

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
