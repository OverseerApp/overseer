import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { endpointFactory } from './endpoint-factory';

@Injectable({ providedIn: 'root' })
export class ControlService {
  private getEndpoint = endpointFactory('/api/control');

  constructor(private http: HttpClient) {}

  pauseJob(machineId: number): Observable<void> {
    return this.http.post<void>(this.getEndpoint(machineId, 'pause'), null);
  }

  resumeJob(machineId: number): Observable<void> {
    return this.http.post<void>(this.getEndpoint(machineId, 'resume'), null);
  }

  cancelJob(machineId: number): Observable<void> {
    return this.http.post<void>(this.getEndpoint(machineId, 'cancel'), null);
  }
}
