import { Component, DestroyRef, inject, input, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { I18NextPipe } from 'angular-i18next';
import { accessLevels, SessionLifetime, sessionLifetimes } from '../../models/constants';
import { CreateUserForm } from '../../models/form.types';
import { AccessLevel } from '../../models/user.model';

@Component({
  selector: 'app-create-user',
  templateUrl: './create-user.component.html',
  imports: [ReactiveFormsModule, I18NextPipe],
})
export class CreateUserComponent implements OnInit {
  private destroyRef = inject(DestroyRef);

  accessLevel = input<AccessLevel | undefined>();
  form = input<FormGroup<CreateUserForm>>();
  isInitialSetup = input<boolean>(false);

  lifetimes = sessionLifetimes;
  accessLevels = accessLevels;

  ngOnInit(): void {
    const form = this.form();
    if (!form) return;

    const accessLevel = this.accessLevel();
    form.addControl('accessLevel', new FormControl<AccessLevel | undefined>(accessLevel, Validators.required));
    form.addControl('username', new FormControl<string>('', Validators.required));
    form.addControl('password', new FormControl<string>('', [Validators.required, Validators.minLength(8)]));
    form.addControl('confirmPassword', new FormControl<string>('', Validators.required));
    form.addControl('sessionLifetime', new FormControl<SessionLifetime>(undefined));

    if (accessLevel) {
      form.get('accessLevel')?.disable();
    }

    form?.setValidators([
      () => {
        const password = form.get('password')?.value;
        const confirmPassword = form.get('confirmPassword')?.value;
        return password === confirmPassword ? null : { passwordMismatch: true };
      },
    ]);

    const accessLevelControl = form.get('accessLevel');
    const passwordControl = form.get('password');
    const confirmPasswordControl = form.get('confirmPassword');

    accessLevelControl?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((level) => {
      if (level === 'User') {
        passwordControl?.setValidators([]);
        confirmPasswordControl?.setValidators([]);
      } else {
        passwordControl?.setValidators([Validators.required, Validators.minLength(8)]);
        confirmPasswordControl?.setValidators([Validators.required]);
      }
      passwordControl?.updateValueAndValidity();
      confirmPasswordControl?.updateValueAndValidity();
    });
  }
}
