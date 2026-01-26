import { Location } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { I18NextPipe } from 'angular-i18next';
import { filter, map, Observable, switchMap } from 'rxjs';
import { MachineHostComponent } from '../../components/machine-host/machine-host.component';
import { MachineForm } from '../../models/form.types';
import { Machine } from '../../models/machine.model';
import { CertificateErrorService } from '../../services/certificate-error.service';
import { DialogService } from '../../services/dialog.service';
import { MachinesService } from '../../services/machines.service';
import { ToastsService } from '../../services/toast.service';

@Component({
  selector: 'app-edit-machine',
  templateUrl: './edit-machine.component.html',
  imports: [I18NextPipe, ReactiveFormsModule, RouterLink, MachineHostComponent],
  providers: [DialogService, CertificateErrorService],
})
export class EditMachineComponent {
  private location = inject(Location);
  private route = inject(ActivatedRoute);
  private formBuilder = inject(FormBuilder);
  private machinesService = inject(MachinesService);
  private dialogService = inject(DialogService);
  private certificateErrorService = inject(CertificateErrorService);
  private toastsService = inject(ToastsService);

  form?: FormGroup<MachineForm>;

  machine = signal<Machine | undefined>(undefined);

  constructor() {
    this.route.paramMap
      .pipe(
        map((params) => Number(params.get('id'))),
        switchMap((id) => this.machinesService.getMachine(id))
      )
      .subscribe((machine: Machine) => {
        this.machine.set(machine);
        this.form = this.formBuilder.nonNullable.group({}, { updateOn: 'change' });
        this.form.addControl('id', new FormControl(machine?.id));
        this.form.addControl('machineType', new FormControl(null));
      });
  }

  deleteMachine() {
    this.dialogService
      .prompt({ messageKey: 'deleteMachinePrompt' })
      .closed.pipe(filter((result) => result))
      .subscribe(() => this.handleNetworkAction(this.machinesService.deleteMachine(this.machine()!)));
  }

  save() {
    this.handleNetworkAction(this.machinesService.updateMachine({ ...this.machine(), ...this.form!.getRawValue() } as Machine));
  }

  updateMonitoring(disabled: boolean): void {
    var update = { ...this.machine(), disabled } as Machine;
    this.machinesService.updateMachine(update).subscribe({
      complete: () => {
        this.form?.patchValue({ disabled: false });
        this.machine.set(update);
      },
    });
  }

  private handleNetworkAction(observable: Observable<any>) {
    this.form?.disable();

    observable.subscribe({
      complete: () => {
        this.toastsService.show({ message: 'savedChanges', type: 'success' });
        this.location.back();
      },
      error: (ex) => {
        this.form?.enable();
        this.certificateErrorService
          .handleCertificateException(ex)
          .pipe(filter((result) => result))
          .subscribe(() => this.save());
      },
    });
  }
}
