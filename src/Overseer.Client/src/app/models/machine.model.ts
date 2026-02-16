export type MachineToolType = 'Undetermined' | 'Heater' | 'Extruder';

export type MachineTool = {
  toolType: MachineToolType;
  index: number;
  name: string;
};

export type WebcamOrientation = 'Default' | 'FlippedVertically' | 'FlippedHorizontally';

// properties that can be set by the user
export const machineInputProperties = ['name', 'webcamUrl', 'webcamOrientation'] as const;
export type MachineInputProperty = (typeof machineInputProperties)[number];

export const machineInputOptions: Record<MachineInputProperty, string[] | null> = {
  webcamOrientation: ['Default', 'FlippedVertically', 'FlippedHorizontally'],
  name: null,
  webcamUrl: null,
};

export type Machine = {
  id: number;
  machineType: string;
  tools: MachineTool[];
  sortIndex: number;
  properties: Record<string, unknown>;
  webcamUrl?: string;
  webcamOrientation?: WebcamOrientation;
  ['name']?: string;
  ['webcamUrl']?: string;
  ['webcamOrientation']?: WebcamOrientation;
  ['disabled']?: boolean;
};
