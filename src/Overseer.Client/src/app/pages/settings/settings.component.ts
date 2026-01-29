import { Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormBuilder, FormControl, FormGroup, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { I18NextPipe } from 'angular-i18next';
import { defaultPollInterval, pollIntervals } from '../../models/constants';
import { ApplicationSettings } from '../../models/settings.model';
import { FeaturesService } from '../../services/features.service';
import { SettingsService } from '../../services/settings.service';
import { ThemeService } from '../../services/theme.service';
import { ToastsService } from '../../services/toast.service';

@Component({
  selector: 'app-settings',
  templateUrl: './settings.component.html',
  imports: [ReactiveFormsModule, I18NextPipe, FormsModule],
})
export class SettingsComponent {
  intervals = pollIntervals;
  themeService = inject(ThemeService);
  private formBuilder = inject(FormBuilder);
  private toastsService = inject(ToastsService);
  private settingsService = inject(SettingsService);
  private featuresService = inject(FeaturesService);

  protected supportsAiMonitoring = rxResource({ stream: () => this.featuresService.supportsAiMonitoring() });

  form?: FormGroup<{
    interval: FormControl<number>;
    hideDisabledMachines: FormControl<boolean>;
    hideIdleMachines: FormControl<boolean>;
    sortByTimeRemaining: FormControl<boolean>;
    enableAiMonitoring: FormControl<boolean>;
    aiMonitoringFrameCaptureRate: FormControl<number>;
    aiMonitoringFailureAction: FormControl<'AlertOnly' | 'PauseJob' | 'CancelJob'>;
  }>;

  constructor() {
    this.settingsService.getSettings().subscribe((settings) => {
      this.form = this.formBuilder.nonNullable.group({
        interval: settings.interval ?? defaultPollInterval,
        hideDisabledMachines: settings.hideDisabledMachines ?? false,
        hideIdleMachines: settings.hideIdleMachines ?? false,
        sortByTimeRemaining: settings.sortByTimeRemaining ?? false,
        enableAiMonitoring: settings.enableAiMonitoring ?? false,
        aiMonitoringFrameCaptureRate: settings.aiMonitoringFrameCaptureRate ?? 5,
        aiMonitoringFailureAction: settings.aiMonitoringFailureAction ?? 'AlertOnly',
      });

      this.form.valueChanges.subscribe(() => {
        this.updateAiMonitoringState();
        this.settingsService.updateSettings(this.form!.getRawValue() as ApplicationSettings).subscribe(() => this.updateComplete());
      });

      this.updateAiMonitoringState();
    });
  }

  updateComplete(): void {
    this.toastsService.show({
      delay: 1000,
      type: 'success',
      message: 'savedChanges',
    });
  }

  updateAiMonitoringState(): void {
    if (!this.form) return;
    const enableAiMonitoring = this.form.get('enableAiMonitoring')?.value;
    if (enableAiMonitoring) {
      this.form.get('aiMonitoringFrameCaptureRate')?.enable({ emitEvent: false });
      this.form.get('aiMonitoringFailureAction')?.enable({ emitEvent: false });
    } else {
      this.form.get('aiMonitoringFrameCaptureRate')?.disable({ emitEvent: false });
      this.form.get('aiMonitoringFailureAction')?.disable({ emitEvent: false });
    }
  }

  handleSchemeChange(scheme: string): void {
    if (!['auto', 'light', 'dark'].includes(scheme)) return;
    this.themeService.scheme.set(scheme as 'auto' | 'light' | 'dark');
    this.updateComplete();
  }

  handleThemeChange(theme: string): void {
    this.themeService.theme.set(theme);
    this.updateComplete();
  }
}
