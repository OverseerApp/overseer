import { NgClass } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { I18NextPipe } from 'angular-i18next';
import { CardSectionComponent } from '../../components/card-section/card-section.component';
import { User } from '../../models/user.model';
import { UsersService } from '../../services/users.service';

@Component({
  selector: 'app-list-users',
  templateUrl: './list-users.component.html',
  imports: [CardSectionComponent, I18NextPipe, RouterLink, NgClass],
})
export class ListUsersComponent {
  private usersService = inject(UsersService);
  users = signal<User[]>([]);

  constructor() {
    this.usersService.getUsers().subscribe((users) => this.users.set(users));
  }
}
