import { FormControl } from '@angular/forms';
import { AccessLevel } from './user.model';

export type CreateUserForm = {
  username?: FormControl<string>;
  password?: FormControl<string>;
  confirmPassword?: FormControl<string>;
  sessionLifetime?: FormControl<number>;
  accessLevel?: FormControl<AccessLevel>;
};
