import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { map, mergeMap, NEVER, Observable, of, tap } from 'rxjs';
import { MachineMetadata } from '../models/machine-metadata.model';
import { Machine } from '../models/machine.model';
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
    return this.http.post<Machine>(this.getEndpoint(), this.denormalizeProperties(machine)).pipe(tap(() => this.machines.reload()));
  }

  updateMachine(machine: Machine): Observable<Machine> {
    return this.http.put<Machine>(this.getEndpoint(), this.denormalizeProperties(machine)).pipe(tap(() => this.machines.reload()));
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

  getMachineMetadata(): Observable<Record<string, MachineMetadata[]>> {
    const getPropertyName = (metadata: MachineMetadata): string => {
      return metadata.propertyName.charAt(0).toLowerCase() + metadata.propertyName.slice(1);
    };

    return this.http.get<Record<string, MachineMetadata[]>>(this.getEndpoint('metadata')).pipe(
      map((data) => {
        return Object.entries(data).reduce(
          (acc, [key, metadata]) => {
            acc[key] = metadata.map((m) => ({ ...m, propertyName: getPropertyName(m) }) as MachineMetadata);
            return acc;
          },
          {} as Record<string, MachineMetadata[]>
        );
      })
    );
  }

  private denormalizeProperties(machine: Machine): Machine {
    if (!machine.properties) return machine;

    const denormalized = Object.entries(machine.properties).reduce(
      (acc, [key, value]) => {
        const originalKey = key.charAt(0).toUpperCase() + key.slice(1);
        acc[originalKey] = value;
        return acc;
      },
      {} as Record<string, unknown>
    );

    return { ...machine, properties: denormalized };
  }
}
