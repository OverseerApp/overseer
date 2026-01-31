import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { UpdateInfo, UpdateResult } from '../models/update-info.model';
import { endpointFactory } from './endpoint-factory';

@Injectable({ providedIn: 'root' })
export class SystemService {
  private getUpdateEndpoint = endpointFactory('/api/system');
  private http = inject(HttpClient);

  ping(): Observable<'pong'> {
    return this.http.get<'pong'>(this.getUpdateEndpoint('ping'));
  }

  /**
   * Check for available updates from GitHub releases
   * @param includePreRelease Whether to include pre-release versions
   */
  checkForUpdates(includePreRelease = false): Observable<UpdateInfo> {
    return this.http.get<UpdateInfo>(this.getUpdateEndpoint('updates/check'), {
      params: { includePreRelease: includePreRelease.toString() },
    });
  }

  /**
   * Initiate the auto-update process (Linux only)
   * @param version The version to update to
   */
  installUpdate(version: string): Observable<UpdateResult> {
    return this.http.post<UpdateResult>(this.getUpdateEndpoint('updates/install'), { version });
  }

  /**
   * Restart the overseer application, if supported.
   */
  restart(): Observable<void> {
    return this.http.post<void>(this.getUpdateEndpoint('restart'), {});
  }
}
