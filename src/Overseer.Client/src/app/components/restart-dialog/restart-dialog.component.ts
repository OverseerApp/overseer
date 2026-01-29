import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { I18NextPipe } from 'angular-i18next';
import { catchError, delay, of, retry, switchMap, timer } from 'rxjs';
import { AuthenticationService } from '../../services/authentication.service';
import { FeaturesService } from '../../services/features.service';
import { SystemService } from '../../services/system.service';

@Component({
  selector: 'app-restart-dialog',
  templateUrl: './restart-dialog.component.html',
  imports: [I18NextPipe],
})
export class RestartDialogComponent {
  private featuresService = inject(FeaturesService);
  private authenticationService = inject(AuthenticationService);
  private systemService = inject(SystemService);
  private destroyRef = inject(DestroyRef);

  protected activeModal = inject(NgbActiveModal);
  protected isRestarting = signal(false);
  protected restartComplete = signal(false);
  protected restartFailed = signal(false);

  private readonly POLL_INTERVAL_MS = 2000;
  private readonly MAX_RETRIES = 30;

  protected canAutoUpdate = rxResource({
    stream: () => this.featuresService.canAutoUpdate(),
  });

  constructor() {
    effect(() => {
      const canAutoUpdate = this.canAutoUpdate.value();
      if (canAutoUpdate) {
        this.initiateRestart();
      }
    });
  }

  private initiateRestart(): void {
    if (this.isRestarting()) return;

    this.isRestarting.set(true);
    this.restartComplete.set(false);
    this.restartFailed.set(false);

    this.systemService
      .restart()
      .pipe(
        catchError(() => of(void 0)),
        delay(3000),
        switchMap(() => this.pollForBackend()),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (isUp) => {
          if (isUp) {
            this.isRestarting.set(false);
            this.restartComplete.set(true);
          } else {
            this.isRestarting.set(false);
            this.restartFailed.set(true);
          }
        },
        error: () => {
          this.isRestarting.set(false);
          this.restartFailed.set(true);
        },
      });
  }

  private pollForBackend() {
    return timer(0, this.POLL_INTERVAL_MS).pipe(
      switchMap(() => this.authenticationService.checkLogin()),
      retry({ count: this.MAX_RETRIES, delay: this.POLL_INTERVAL_MS }),
      catchError(() => of(false))
    );
  }

  protected retryRestart(): void {
    this.initiateRestart();
  }
}
