import { DatePipe, NgClass } from '@angular/common';
import { Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { I18NextPipe } from 'angular-i18next';
import { CardSectionComponent } from '../../components/card-section/card-section.component';
import { AuthenticationService } from '../../services/authentication.service';
import { UsersService } from '../../services/users.service';

@Component({
  selector: 'app-list-users',
  templateUrl: './list-users.component.html',
  imports: [CardSectionComponent, I18NextPipe, RouterLink, NgClass, DatePipe],
})
export class ListUsersComponent {
  private usersService = inject(UsersService);
  private authService = inject(AuthenticationService);

  users = rxResource({ stream: () => this.usersService.getUsers() });
  activeUser = this.authService.activeUser;
}
