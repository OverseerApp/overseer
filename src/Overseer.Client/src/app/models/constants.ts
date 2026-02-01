import { WebCamOrientation } from './machine.model';
import { AccessLevel } from './user.model';

export class DisplayOption<TValue> {
  constructor(
    public translationKey: string,
    public value: TValue
  ) {}
}

export type SessionLifetime = undefined | 1 | 7 | 30 | 90;
export const sessionLifetimes: DisplayOption<SessionLifetime>[] = [
  new DisplayOption('indefinite', undefined),
  new DisplayOption('day', 1),
  new DisplayOption('days', 7),
  new DisplayOption('days', 30),
  new DisplayOption('days', 90),
];

export const defaultPollInterval = 10000;
export type PollInterval = 1000 | 5000 | 10000 | 20000 | 30000;
export const pollIntervals: DisplayOption<PollInterval>[] = [
  new DisplayOption('second', 1000),
  new DisplayOption('seconds', 5000),
  new DisplayOption('seconds', 10000),
  new DisplayOption('seconds', 20000),
  new DisplayOption('seconds', 30000),
];

export const accessLevels: DisplayOption<AccessLevel>[] = [
  new DisplayOption('readonly', 'Readonly'),
  new DisplayOption('user', 'User'),
  new DisplayOption('administrator', 'Administrator'),
];

export const webCamOrientations: DisplayOption<WebCamOrientation>[] = [
  new DisplayOption('default', 'Default'),
  new DisplayOption('flipVertically', 'FlippedVertically'),
  new DisplayOption('flipHorizontally', 'FlippedHorizontally'),
];

export const themes = ['blue', 'indigo', 'purple', 'pink', 'red', 'orange', 'yellow', 'green', 'teal', 'cyan'];
