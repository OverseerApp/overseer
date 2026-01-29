import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { endpointFactory } from './endpoint-factory';

@Injectable({ providedIn: 'root' })
export class FeaturesService {
  private getEndpoint = endpointFactory('/api/features');
  private http = inject(HttpClient);

  /**
   * Returns true when any AI monitoring plugin is installed
   */
  supportsAiMonitoring(): Observable<boolean> {
    return this.http.get<boolean>(this.getEndpoint('ai-monitoring'));
  }

  /**
   * Returns true when the host platform supports auto updating.
   * E.g. linux running overseer as a system service.
   */
  canAutoUpdate(): Observable<boolean> {
    return this.http.get<boolean>(this.getEndpoint('auto-update'));
  }
}
