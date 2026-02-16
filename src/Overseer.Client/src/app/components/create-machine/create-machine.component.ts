import { Component, computed, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, UntypedFormGroup, Validators } from '@angular/forms';
import { I18NextPipe } from 'angular-i18next';
import { MachinesService } from '../../services/machines.service';
import { MachineFormComponent } from '../machine-form/machine-form.component';

@Component({
  selector: 'app-create-machine',
  templateUrl: './create-machine.component.html',
  imports: [I18NextPipe, ReactiveFormsModule, MachineFormComponent],
})
export class CreateMachineComponent implements OnInit {
  private machinesService = inject(MachinesService);
  private destroyRef = inject(DestroyRef);

  form = input.required<UntypedFormGroup>();

  protected machineMetadata = rxResource({
    stream: () => this.machinesService.getMachineMetadata(),
  });

  protected machineTypes = computed(() => {
    if (this.machineMetadata.isLoading()) return [];
    if (this.machineMetadata.error()) return [];

    const metadata = this.machineMetadata.value();
    if (!metadata) return [];

    return Object.keys(metadata);
  });

  protected selectedMachineType = signal<string | undefined>(undefined);

  protected selectedMetadata = computed(() => {
    const selectedType = this.selectedMachineType();
    const metadata = this.machineMetadata.value();

    if (!selectedType) return undefined;
    if (!metadata) return undefined;

    return metadata[selectedType] ?? undefined;
  });

  ngOnInit(): void {
    const form = this.form();
    form?.addControl('machineType', new FormControl('', Validators.required));
    form
      ?.get('machineType')
      ?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => this.selectedMachineType.set(value));
  }
}
