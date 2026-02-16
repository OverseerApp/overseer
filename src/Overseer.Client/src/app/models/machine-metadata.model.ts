export type MachineMetadata = {
  propertyName: string;
  displayName?: string;
  displayType: 'Both' | 'SetupOnly' | 'UpdateOnly';
  description?: string;
  isRequired: boolean;
  isSensitive: boolean;
  isIgnored: boolean;
  options?: string[];
};
