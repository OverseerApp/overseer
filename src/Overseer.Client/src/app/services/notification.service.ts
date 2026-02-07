import { HttpClient } from '@angular/common/http';
import { effect, inject, Injectable } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { Observable, Subject, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { Notification } from '../models/notifications.model';
import { endpointFactory } from './endpoint-factory';

@Injectable({
  providedIn: 'root',
})
export class NotificationService {
  private http = inject(HttpClient);

  private endpointFactory = endpointFactory('/api/notifications');
  private notifications$ = new Subject<Notification>();
  private notificationTrigger = toSignal(this.notifications$);
  private hubConnection: HubConnection;
  private isConnected = false;

  constructor() {
    this.hubConnection = new HubConnectionBuilder().withUrl(`${environment.apiHost}/push/notifications`).build();
    this.hubConnection.on('notification', (notification: Notification) => this.notifications$.next(notification));
    this.hubConnection.onclose(() => {
      if (!this.isConnected) return;
      this.connect();
    });
    this.connect();

    effect(() => {
      const notification = this.notificationTrigger();
      if (notification) {
        this.notifications.reload();
      }
    });
  }

  private connect(): void {
    try {
      this.hubConnection.start().then(() => {
        this.isConnected = true;
      });
    } catch (error) {
      setTimeout(() => {
        if (!this.isConnected) return;
        this.connect();
      }, 10000);
    }
  }

  notifications = rxResource({
    stream: () => this.getNotifications(),
  });

  getNotifications(): Observable<Notification[]> {
    return this.http.get<Notification[]>(this.endpointFactory());
  }

  markAllAsRead(ids: number[]): Observable<void> {
    return this.http.post<void>(this.endpointFactory('read'), ids).pipe(tap(() => this.notifications.reload()));
  }

  removeNotification(id: number): Observable<void> {
    return this.http.delete<void>(this.endpointFactory(id)).pipe(tap(() => this.notifications.reload()));
  }

  clearNotifications(): Observable<void> {
    return this.http.delete<void>(this.endpointFactory()).pipe(tap(() => this.notifications.reload()));
  }
}
