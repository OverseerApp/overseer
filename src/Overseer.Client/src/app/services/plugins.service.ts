import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PluginInfo } from '../models/plugin-info.model';
import { endpointFactory } from './endpoint-factory';

@Injectable({ providedIn: 'root' })
export class PluginsService {
  private getEndpoint = endpointFactory('/api/plugins');
  private http = inject(HttpClient);

  getPlugins(refresh = false): Observable<PluginInfo[]> {
    return this.http.get<PluginInfo[]>(this.getEndpoint(), { params: { refresh: refresh.toString() } });
  }

  installPlugin(pluginInfo: PluginInfo): Observable<boolean> {
    return this.http.post<boolean>(this.getEndpoint(), pluginInfo);
  }

  uninstallPlugin(pluginInfo: PluginInfo): Observable<boolean> {
    return this.http.delete<boolean>(this.getEndpoint(pluginInfo.name));
  }
}
