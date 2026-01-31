import { Location } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { I18NextPipe } from 'angular-i18next';
import { filter, forkJoin, map, Observable, take } from 'rxjs';
import { CardSectionComponent } from '../../components/card-section/card-section.component';
import { accessLevels, sessionLifetimes } from '../../models/constants';
import { AccessLevel, User } from '../../models/user.model';
import { AuthenticationService } from '../../services/authentication.service';
import { DialogService } from '../../services/dialog.service';
import { ToastsService } from '../../services/toast.service';
import { UsersService } from '../../services/users.service';

@Component({
  selector: 'app-edit-user',
  templateUrl: './edit-user.component.html',
  imports: [CardSectionComponent, I18NextPipe, ReactiveFormsModule, RouterLink],
  providers: [DialogService],
})
export class EditUserComponent {
  private router = inject(Router);
  private location = inject(Location);
  private route = inject(ActivatedRoute);
  private formBuilder = inject(FormBuilder);
  private usersService = inject(UsersService);
  private authenticationService = inject(AuthenticationService);
  private dialogService = inject(DialogService);
  private toastsService = inject(ToastsService);

  accessLevels = signal(accessLevels);
  lifetimes = signal(sessionLifetimes);
  users = signal<User[]>([]);
  user = signal<User | undefined>(undefined);
  generatedUrl = signal<string | undefined>(undefined);
  activeUser = computed(() => this.authenticationService.activeUser());
  displayPreauthentication = computed(() => {
    return this.activeUser()?.accessLevel === 'Administrator' && this.user()?.accessLevel === 'Readonly';
  });
  isOwnProfile = computed(() => this.user()?.id === this.activeUser()?.id);
  isOnlyAdmin = computed(() => {
    return this.user()?.accessLevel === 'Administrator' && this.users().filter((u) => u.accessLevel === 'Administrator').length === 1;
  });

  updateForm: FormGroup<{ id: FormControl<number>; sessionLifetime: FormControl<number>; accessLevel: FormControl<AccessLevel> }> =
    this.formBuilder.group({
      id: new FormControl<number>(0, { nonNullable: true }),
      sessionLifetime: new FormControl<number>(0, { nonNullable: true }),
      accessLevel: new FormControl<AccessLevel>('Readonly', { nonNullable: true }),
    });

  passwordForm: FormGroup<{ id: FormControl<number>; password: FormControl<string>; confirmPassword: FormControl<string> }> =
    this.formBuilder.nonNullable.group(
      {
        id: [0],
        password: ['', [Validators.min(8), Validators.required]],
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
    forkJoin([
      this.usersService.getUsers(),
      this.route.paramMap.pipe(
        take(1),
        map((params) => Number(params.get('id')!))
      ),
    ]).subscribe(([users, userId]) => {
      const user = users.find((u) => u.id === userId);
      if (!user) {
        this.location.back();
        return;
      }

      this.user.set(user);
      this.users.set(users);
      this.updateForm.patchValue(user);
      this.passwordForm.patchValue(user);
      if (this.isOwnProfile() && this.isOnlyAdmin()) {
        this.updateForm.controls.accessLevel.disable();
      }
    });
  }

  signOut() {
    const user = this.user();
    if (!user?.id) return;
    if (user.id === this.activeUser()?.id) {
      this.authenticationService.logout().subscribe(() => this.router.navigate(['/login']));
    } else {
      this.authenticationService.logoutUser(user.id).subscribe((user) => this.user.set(user));
    }
  }

  deleteUser() {
    if (this.isOnlyAdmin()) {
      this.dialogService.alert({
        titleKey: 'warning',
        messageKey: 'requiresAdminPrompt',
      });
      return;
    }

    this.dialogService
      .prompt({ messageKey: 'deleteUserPrompt' })
      .closed.pipe(filter((result) => result))
      .subscribe(() => this.handleNetworkAction(this.usersService.deleteUser(this.user()!)));
  }

  save() {
    this.handleNetworkAction(this.usersService.updateUser(this.updateForm.getRawValue() as User));
  }

  changePassword() {
    this.handleNetworkAction(this.usersService.changePassword(this.passwordForm.getRawValue() as User));
  }

  generatePreAuthentication() {
    if (!this.user) return;

    this.authenticationService.getPreauthenticatedToken(this.user()?.id!).subscribe((token) => {
      this.generatedUrl.set(`${window.location.origin}/sso?token=${token}`);
    });
  }

  copyToClipboard(input: HTMLInputElement) {
    input.select();
    document.execCommand('copy');
    input.setSelectionRange(0, 0);
    this.toastsService.show({ message: 'copiedToClipboard', type: 'success', delay: 3000 });
  }

  private handleNetworkAction(observable: Observable<any>) {
    this.updateForm.disable();
    observable.subscribe({
      next: () => {
        this.toastsService.show({ message: 'savedChanges', type: 'success' });
        this.location.back();
      },
      error: () => this.updateForm.enable(),
    });
  }
}
