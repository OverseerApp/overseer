import { Location } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { I18NextPipe } from 'angular-i18next';
import { CardSectionComponent } from '../../components/card-section/card-section.component';
import { CreateUserComponent } from '../../components/create-user/create-user.component';
import { CreateUserForm } from '../../models/form.types';
import { User } from '../../models/user.model';
import { UsersService } from '../../services/users.service';

@Component({
  selector: 'app-add-user',
  templateUrl: './add-user.component.html',
  imports: [CardSectionComponent, I18NextPipe, ReactiveFormsModule, RouterLink, CreateUserComponent],
})
export class AddUserComponent {
  private builder = inject(FormBuilder);
  private usersService = inject(UsersService);
  private location = inject(Location);

  form: FormGroup<CreateUserForm> = this.builder.group({});

  save(): void {
    this.form.disable();
    this.usersService.createUser(this.form.getRawValue() as User).subscribe(() => this.location.back());
  }
}
