import { Location } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { I18NextPipe } from 'angular-i18next';
import { filter, forkJoin, map, Observable, take } from 'rxjs';
import { CardSectionComponent } from '../../components/card-section/card-section.component';
import { ChangePasswordComponent } from '../../components/change-password/change-password.component';
import { MagicLinkComponent } from '../../components/magic-link/magic-link.component';
import { accessLevels, sessionLifetimes } from '../../models/constants';
import { AccessLevel, User } from '../../models/user.model';
import { AuthenticationService } from '../../services/authentication.service';
import { DialogService } from '../../services/dialog.service';
import { ToastsService } from '../../services/toast.service';
import { UsersService } from '../../services/users.service';

@Component({
  selector: 'app-edit-user',
  templateUrl: './edit-user.component.html',
  imports: [CardSectionComponent, ChangePasswordComponent, I18NextPipe, ReactiveFormsModule, RouterLink, MagicLinkComponent],
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
  activeUser = computed(() => this.authenticationService.activeUser());
  isOwnProfile = computed(() => this.user()?.id === this.activeUser()?.id);
  displayMagicLink = computed(() => this.activeUser()?.accessLevel === 'Administrator' && !this.isOwnProfile());
  isOnlyAdmin = computed(() => {
    return this.user()?.accessLevel === 'Administrator' && this.users().filter((u) => u.accessLevel === 'Administrator').length === 1;
  });

  updateForm: FormGroup<{ id: FormControl<number>; sessionLifetime: FormControl<number>; accessLevel: FormControl<AccessLevel> }> =
    this.formBuilder.group({
      id: new FormControl<number>(0, { nonNullable: true }),
      sessionLifetime: new FormControl<number>(0, { nonNullable: true }),
      accessLevel: new FormControl<AccessLevel>('Readonly', { nonNullable: true }),
    });

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
      if (this.isOwnProfile() && this.isOnlyAdmin()) {
        this.updateForm.controls.accessLevel.disable();
      }
    });
  }

  signOut() {
    const user = this.user();
    if (user?.id === null || user?.id === undefined) return;
    if (this.isOwnProfile()) {
      this.authenticationService.logout().subscribe(() => this.router.navigate(['/login']));
    } else {
      this.authenticationService.logoutUser(user.id).subscribe((user) => this.user.set(user));
      this.toastsService.show({ message: 'userSignedOut', type: 'success' });
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
