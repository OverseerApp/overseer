import { Component, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { WebcamOrientation } from '../../models/machine.model';

@Component({
  selector: 'app-web-cam-panel',
  templateUrl: './web-cam-panel.component.html',
  styleUrls: ['./web-cam-panel.component.scss'],
  host: {
    '[class]': 'orientation()',
  },
})
export class WebCamPanelComponent {
  private refreshInterval = signal(60000);
  private hasError = signal(false);
  private onDestroy = inject(DestroyRef);
  private interval: ReturnType<typeof setInterval> | undefined;

  url = input<string | undefined>();
  orientation = input<WebcamOrientation | undefined>('Default');
  protected backgroundImage = signal<string | undefined>(undefined);
  protected imageLoadFailed = signal(false);

  constructor() {
    this.onDestroy.onDestroy(() => clearInterval(this.interval));

    const effectRef = effect(() => {
      const url = this.url();
      if (!url) return;

      this.setBackgroundImage(url);
      this.startInterval();
      effectRef.destroy();
    });
  }

  private setBackgroundImage(imageUrl: string): void {
    this.backgroundImage.set(imageUrl);
  }

  private getNextUrl(): string | undefined {
    const webCamUrl = this.url();
    if (!webCamUrl) return undefined;

    const url = new URL(webCamUrl);
    url.searchParams.set('t', Date.now().toString());
    const next = url.toString();
    return next;
  }

  private updateWebCamUrl(): void {
    const url = this.getNextUrl();
    if (!url) return;

    if (this.hasError()) {
      // the pre-caching failed, so just set the background image directly
      this.setBackgroundImage(url.toString());
      return;
    }

    // this is to prevent the flickering effect when the image is updated
    const img = new Image();
    img.onload = () => this.setBackgroundImage(url.toString());
    img.onerror = () => {
      // if the image fails fallback to directly setting the background image
      // this will "flicker" but the refresh interval is increased so it doesn't happen often
      this.hasError.set(true);
      this.refreshInterval.update((i) => i * 5);
      this.startInterval();
    };
    img.src = url.toString();
  }

  private startInterval(): void {
    if (this.interval) {
      clearInterval(this.interval);
    }
    this.interval = setInterval(() => this.updateWebCamUrl(), this.refreshInterval());
  }
}
