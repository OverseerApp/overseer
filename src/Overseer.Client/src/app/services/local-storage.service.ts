import { Injectable } from '@angular/core';
import { fromEvent, Observable, Subject } from 'rxjs';
import { filter, map } from 'rxjs/operators';

@Injectable({ providedIn: 'root' })
export class LocalStorageService {
  private changeSubject = new Subject<{ key: string; value: any }>();
  private storageEvent$ = fromEvent<StorageEvent>(window, 'storage');

  // Observable that emits when localStorage changes in the same tab
  changes$ = this.changeSubject.asObservable();

  get<T>(name: string): T | undefined {
    try {
      let value = window.localStorage.getItem(name);
      if (!value) {
        // it may be a legacy key, try to migrate it
        const prefix = window.localStorage.getItem('NGX-STORE_prefix');
        const key = `${prefix}_${name}`;
        value = window.localStorage.getItem(key);
        if (!value) return undefined;

        this.set(name, JSON.parse(value));
        window.localStorage.removeItem(key);
      }
      return JSON.parse(value);
    } catch (error) {
      console.error(error);
      return undefined;
    }
  }

  set<T>(name: string, item: T): void {
    if (!item) return;
    window.localStorage.setItem(name, JSON.stringify(item));
    this.changeSubject.next({ key: name, value: item });
  }

  remove(name: string): void {
    window.localStorage.removeItem(name);
    this.changeSubject.next({ key: name, value: null });
  }

  clear(): void {
    window.localStorage.clear();
  }

  // Observable that emits when localStorage changes in other tabs/windows
  watchKey(key: string): Observable<any> {
    return this.storageEvent$.pipe(
      filter((event) => event.key === key && event.storageArea === localStorage),
      map((event) => (event.newValue ? JSON.parse(event.newValue) : null))
    );
  }
}
