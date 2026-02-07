import { HttpClient } from '@angular/common/http';
import { effect, inject, Injectable, signal } from '@angular/core';
import { catchError, filter, map, Observable, of, tap } from 'rxjs';
import { User } from '../models/user.model';
import { endpointFactory } from './endpoint-factory';
import { LocalStorageService } from './local-storage.service';

@Injectable({ providedIn: 'root' })
export class AuthenticationService {
  private getEndpoint = endpointFactory('/api/auth');
  private localStorageService = inject(LocalStorageService);
  private http = inject(HttpClient);

  activeUser = signal(this.localStorageService.get<User>('activeUser'));

  constructor() {
    effect(() => {
      this.updateActiveUser(this.activeUser());
    });

    // Listen for activeUser changes in the same tab
    this.localStorageService.changes$.pipe(filter((change) => change.key === 'activeUser')).subscribe((change) => {
      this.activeUser.set(change.value);
    });

    // Listen for activeUser changes from other tabs/windows
    this.localStorageService.watchKey('activeUser').subscribe((value) => {
      this.activeUser.set(value);
    });
  }

  updateActiveUser(user: User | undefined) {
    if (user) {
      this.localStorageService.set('activeUser', user);
    } else {
      this.localStorageService.remove('activeUser');
    }
  }

  checkLogin(): Observable<boolean> {
    return this.http.get<User>(this.getEndpoint()).pipe(
      tap((user) => {
        if (user && user.id) {
          this.activeUser.set(user);
        }
      }),
      map(() => true),
      catchError(() => {
        this.activeUser.set(undefined);
        return of(false);
      })
    );
  }

  login(user: User): Observable<User> {
    return this.http.post<User>(this.getEndpoint('login'), user).pipe(
      tap((activeUser) => {
        this.activeUser.set(activeUser);
        this.updateActiveUser(activeUser);
      })
    );
  }

  logout(): Observable<Object> {
    return this.http.delete(this.getEndpoint('logout')).pipe(
      tap(() => {
        this.activeUser.set(undefined);
      })
    );
  }

  logoutUser(userId: number): Observable<User> {
    return this.http.post<User>(this.getEndpoint('logout', userId), {});
  }

  createInitialUser(user: User): Observable<User> {
    return this.http.post<User>(this.getEndpoint('setup'), user);
  }

  getPreauthenticatedToken(userId: number): Observable<string> {
    return this.http.get<string>(this.getEndpoint('sso', userId));
  }

  validatePreauthenticatedToken(token: string): Observable<User> {
    return this.http.post<User>(this.getEndpoint('sso'), {}, { params: { token } }).pipe(tap((activeUser) => this.activeUser.set(activeUser)));
  }
}
