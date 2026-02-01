import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { I18NextPipe } from 'angular-i18next';
import { catchError } from 'rxjs/internal/operators/catchError';
import { ChangePasswordComponent } from '../../components/change-password/change-password.component';
import { SvgComponent } from '../../components/svg/svg.component';
import { User } from '../../models/user.model';
import { AuthenticationService } from '../../services/authentication.service';

@Component({
  selector: 'app-sso',
  templateUrl: './sso.component.html',
  imports: [ChangePasswordComponent, I18NextPipe, SvgComponent],
})
export class SsoComponent implements OnInit {
  private readonly authenticationService = inject(AuthenticationService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected userRequiringPasswordChange = signal<User | null>(null);

  redirectLogin() {
    this.redirect('/login');
  }

  redirectHome() {
    this.redirect('/');
  }

  redirect(path: string) {
    this.router.navigate([path]);
  }

  ngOnInit(): void {
    this.authenticationService.checkLogin().subscribe((isAuthenticated) => {
      if (isAuthenticated) {
        this.redirectHome();
      } else {
        this.route.queryParamMap.subscribe((params) => {
          if (params.has('token')) {
            this.authenticationService
              .validatePreauthenticatedToken(params.get('token')!)
              .pipe(catchError(() => [null]))
              .subscribe((user) => {
                if (!user) {
                  this.redirectLogin();
                  return;
                }

                if (user.accessLevel !== 'Readonly') {
                  // if it's a user or admin the link was provided for them
                  // to setup or recover their password
                  this.userRequiringPasswordChange.set(user);
                } else {
                  // if it's a readonly user go directly to the home page
                  this.redirectHome();
                }
              });
          } else {
            this.redirectLogin();
          }
        });
      }
    });
  }
}
