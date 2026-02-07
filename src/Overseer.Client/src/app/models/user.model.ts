export type AccessLevel = 'Readonly' | 'User' | 'Administrator';

export type User = {
  id?: number;
  username?: string;
  password?: string;
  sessionLifetime?: number;
  isLoggedIn?: boolean;
  accessLevel?: AccessLevel;
  lastLogin?: string;
};
