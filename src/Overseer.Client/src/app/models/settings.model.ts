export type ApplicationSettings = {
  interval?: number;
  hideDisabledMachines?: boolean;
  hideIdleMachines?: boolean;
  sortByTimeRemaining: boolean;

  enableAiMonitoring?: boolean;
  aiMonitoringFrameCaptureRate?: number;
  aiMonitoringFailureAction?: 'AlertOnly' | 'PauseJob' | 'CancelJob';
};
