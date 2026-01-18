import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { UpdateInfo, UpdateResult } from '../models/update-info.model';
import { endpointFactory } from './endpoint-factory';

@Injectable({ providedIn: 'root' })
export class UpdateService {
  private getUpdateEndpoint = endpointFactory('/api/updates');
  private http = inject(HttpClient);

  /**
   * Check for available updates from GitHub releases
   * @param includePreRelease Whether to include pre-release versions
   */
  checkForUpdates(includePreRelease = false): Observable<UpdateInfo> {
    return this.http.get<UpdateInfo>(this.getUpdateEndpoint('check'), {
      params: { includePreRelease: includePreRelease.toString() },
    });
  }

  /**
   * Initiate the auto-update process (Linux only)
   * @param version The version to update to
   */
  installUpdate(version: string): Observable<UpdateResult> {
    return this.http.post<UpdateResult>(this.getUpdateEndpoint('install'), { version });
  }

  /**
   * Check if the current platform supports auto-update
   */
  canAutoUpdate(): Observable<{ canAutoUpdate: boolean }> {
    return this.http.get<{ canAutoUpdate: boolean }>(this.getUpdateEndpoint('can-auto-update'));
  }
}
