import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { Observable, ReplaySubject } from 'rxjs';
import { environment } from '../../environments/environment';
import { defaultPollInterval } from '../models/constants';
import { MachineStatus } from '../models/machine-status.model';

@Injectable({
  providedIn: 'root',
})
export class MonitoringService {
  private statusEvent$ = new ReplaySubject<MachineStatus>(10, defaultPollInterval, { now: () => Date.now() });
  private hubConnection: HubConnection;
  private isConnected = false;

  constructor() {
    this.hubConnection = new HubConnectionBuilder().withUrl(`${environment.apiHost}/push/status`).build();
    this.hubConnection.on('statusUpdate', (statusUpdate: MachineStatus) => this.statusEvent$.next(statusUpdate));
    this.hubConnection.onclose(() => {
      if (!this.isConnected) return;
      this.start();
    });
  }

  private async start(): Promise<void> {
    try {
      this.hubConnection.start().then(() => {
        this.hubConnection.invoke('startMonitoring');
        this.isConnected = true;
      });
    } catch (error) {
      setTimeout(() => {
        if (!this.isConnected) return;
        this.start();
      }, defaultPollInterval);
    }
  }

  enableMonitoring(): Observable<MachineStatus> {
    if (this.isConnected) return this.statusEvent$;
    if (this.hubConnection.state === HubConnectionState.Connecting) return this.statusEvent$;
    if (this.hubConnection.state === HubConnectionState.Connected) {
      this.isConnected = true;
      return this.statusEvent$;
    }

    if (this.hubConnection.state === HubConnectionState.Disconnected) {
      this.start();
    }

    return this.statusEvent$;
  }

  disableMonitoring(): void {
    if (!this.isConnected) return;

    this.isConnected = false;
    this.hubConnection.stop();
  }
}
