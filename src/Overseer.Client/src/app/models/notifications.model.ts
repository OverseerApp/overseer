export type NotificationType = 'Job' | 'Simple' | 'JobFailure';

export type NotificationBase = {
  notificationType: NotificationType;
  id: number;
  timestamp: number;
  message: string;
  isRead: boolean;
};

export type SimpleNotification = NotificationBase & { notificationType: 'Simple' };

export type JobNotification = {
  notificationType: 'Job';
  id: number;
  timestamp: number;
  message: string;
  isRead: boolean;
  type: 'JobStarted' | 'JobPaused' | 'JobResumed' | 'JobCompleted' | 'JobError';
  machineId: number;
  machineJobId: string;
};

export type JobFailureAnalysisResult = {
  confidenceScore: number;
  failureReason: string | null;
  details: string | null;
};

export type JobFailureNotification = {
  notificationType: 'JobFailure';
  id: number;
  timestamp: number;
  message: string;
  isRead: boolean;
  type: 'JobError';
  machineId: number;
  machineJobId: string;
  jobPaused: boolean;
  jobCancelled: boolean;
  analysisResult: JobFailureAnalysisResult;
};

export type Notification = JobNotification | SimpleNotification | JobFailureNotification;
