import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { mergeMap, NEVER, Observable, of, tap } from 'rxjs';
import { Machine, MachineType } from '../models/machine.model';
import { AuthenticationService } from './authentication.service';
import { endpointFactory } from './endpoint-factory';

@Injectable({ providedIn: 'root' })
export class MachinesService {
  private authenticationService = inject(AuthenticationService);
  private getEndpoint = endpointFactory('/api/machines');
  private http = inject(HttpClient);

  machines = rxResource({
    params: this.authenticationService.activeUser,
    stream: ({ params: activeUser }) => {
      if (!activeUser) {
        return of([]);
      }
      return this.getMachines();
    },
  });

  getMachines(): Observable<Machine[]> {
    return this.http.get<Machine[]>(this.getEndpoint());
  }

  getMachine(machineId: number): Observable<Machine> {
    return this.http.get<Machine>(this.getEndpoint(machineId));
  }

  createMachine(machine: Machine): Observable<Machine> {
    return this.http.post<Machine>(this.getEndpoint(), machine).pipe(tap(() => this.machines.reload()));
  }

  updateMachine(machine: Machine): Observable<Machine> {
    return this.http.put<Machine>(this.getEndpoint(), machine).pipe(tap(() => this.machines.reload()));
  }

  deleteMachine(machine: Machine): Observable<Machine> {
    return this.http.delete<Machine>(this.getEndpoint(machine.id)).pipe(tap(() => this.machines.reload()));
  }

  sortMachines(sortOrder: number[]): Observable<never> {
    return this.http.post(this.getEndpoint('sort'), sortOrder).pipe(
      tap(() => this.machines.reload()),
      mergeMap(() => NEVER)
    );
  }

  getMachineTypes(): Observable<MachineType[]> {
    return this.http.get<MachineType[]>(this.getEndpoint('types'));
  }
}
