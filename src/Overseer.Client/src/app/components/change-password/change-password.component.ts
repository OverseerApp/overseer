import { Component, effect, inject, input, output } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { I18NextPipe } from 'angular-i18next';
import { User } from '../../models/user.model';
import { ToastsService } from '../../services/toast.service';
import { UsersService } from '../../services/users.service';
import { CardSectionComponent } from '../card-section/card-section.component';

@Component({
  selector: 'app-change-password',
  templateUrl: './change-password.component.html',
  imports: [CardSectionComponent, I18NextPipe, ReactiveFormsModule],
})
export class ChangePasswordComponent {
  private formBuilder = inject(FormBuilder);
  private usersService = inject(UsersService);
  private toastsService = inject(ToastsService);

  user = input<User>();
  passwordUpdated = output<void>();

  passwordForm: FormGroup<{ id: FormControl<number>; password: FormControl<string>; confirmPassword: FormControl<string> }> =
    this.formBuilder.nonNullable.group(
      {
        id: [0],
        password: ['', [Validators.minLength(8), Validators.required]],
        confirmPassword: ['', [Validators.required]],
      },
      {
        validators: [
          () => {
            const password = this.passwordForm?.get('password')?.value;
            const confirmPassword = this.passwordForm?.get('confirmPassword')?.value;
            return password === confirmPassword ? null : { passwordMismatch: true };
          },
        ],
      }
    );

  constructor() {
    effect(() => {
      const user = this.user();
      if (user) {
        this.passwordForm.patchValue(user);
      }
    });
  }

  changePassword() {
    this.passwordForm.disable();
    this.usersService.changePassword(this.passwordForm.getRawValue() as User).subscribe({
      next: () => {
        this.toastsService.show({ message: 'savedChanges', type: 'success' });
        this.passwordForm.reset();
        this.passwordForm.enable();
        this.passwordUpdated.emit();
      },
      error: () => this.passwordForm.enable(),
    });
  }
}
