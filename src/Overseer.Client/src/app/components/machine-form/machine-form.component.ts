import { Component, computed, effect, input } from '@angular/core';
import { ReactiveFormsModule, UntypedFormControl, UntypedFormGroup, Validators } from '@angular/forms';
import { I18NextPipe } from 'angular-i18next';
import { MachineMetadata } from '../../models/machine-metadata.model';
import { Machine, machineInputOptions, machineInputProperties, MachineInputProperty } from '../../models/machine.model';

export type FormFieldDescriptor = {
  propertyName: string;
  displayName: string;
  description?: string;
  isSensitive: boolean;
  isInPropertiesGroup: boolean;
  options?: string[];
};

@Component({
  selector: 'app-machine-form',
  templateUrl: './machine-form.component.html',
  imports: [ReactiveFormsModule, I18NextPipe],
})
export class MachineFormComponent {
  mode = input.required<'create' | 'edit'>();
  machineMetadata = input.required<MachineMetadata[]>();
  machine = input<Machine | undefined>();
  form = input<UntypedFormGroup>();

  formFields = computed<FormFieldDescriptor[]>(() => {
    const metadata = this.machineMetadata();
    const mode = this.mode();
    if (!metadata) return [];

    const metadataPropertyNames = new Set(metadata.map((m) => m.propertyName));
    const fields: FormFieldDescriptor[] = [];

    // Add machineInputProperties not covered by metadata
    machineInputProperties.forEach((key) => {
      if (!metadataPropertyNames.has(key)) {
        fields.push({
          propertyName: key,
          displayName: key,
          isSensitive: false,
          isInPropertiesGroup: false,
          options: machineInputOptions[key as MachineInputProperty] ?? undefined,
        });
      }
    });

    // Add visible, non-ignored metadata properties
    metadata.forEach((m) => {
      const isVisible =
        m.displayType === 'Both' || (m.displayType === 'SetupOnly' && mode === 'create') || (m.displayType === 'UpdateOnly' && mode === 'edit');

      if (isVisible && !m.isIgnored) {
        fields.push({
          propertyName: m.propertyName,
          displayName: m.displayName ?? m.propertyName,
          description: m.description,
          isSensitive: m.isSensitive,
          isInPropertiesGroup: !machineInputProperties.includes(m.propertyName as MachineInputProperty),
          options: machineInputProperties.includes(m.propertyName as MachineInputProperty)
            ? (machineInputOptions[m.propertyName as MachineInputProperty] ?? undefined)
            : m.options,
        });
      }
    });

    return fields;
  });

  constructor() {
    effect(() => {
      const form = this.form();
      const metadata = this.machineMetadata();
      const machine = this.machine();

      if (!form) return;
      if (!metadata) return;

      const mode = this.mode();
      const metadataPropertyNames = new Set(metadata.map((m) => m.propertyName));

      // Add controls for dynamic properties not in metadata
      machineInputProperties.forEach((key) => {
        if (!metadataPropertyNames.has(key as string)) {
          form.addControl(key as string, new UntypedFormControl(machine?.[key] ?? null, Validators.required));
        }
      });

      // Add controls for metadata properties
      const propertiesGroup = new UntypedFormGroup({});
      form.addControl('properties', propertiesGroup);

      metadata.forEach((metadata) => {
        const isVisible =
          metadata.displayType === 'Both' ||
          (metadata.displayType === 'SetupOnly' && mode === 'create') ||
          (metadata.displayType === 'UpdateOnly' && mode === 'edit');

        if (isVisible && !metadata.isIgnored) {
          const propertyName = metadata.propertyName;
          if (machineInputProperties.includes(metadata.propertyName as MachineInputProperty)) {
            form.addControl(propertyName, new UntypedFormControl(this.getValue(propertyName), metadata.isRequired ? Validators.required : null));
          } else {
            propertiesGroup.addControl(
              propertyName,
              new UntypedFormControl(this.getValue(propertyName), metadata.isRequired ? Validators.required : null)
            );
          }
        }
      });
    });
  }

  private getValue(propertyName: string) {
    const machine = this.machine();
    if (!machine) return null;

    if (propertyName in machine) {
      return machine[propertyName as keyof Machine] ?? null;
    }

    return machine.properties?.[propertyName] ?? null;
  }
}
