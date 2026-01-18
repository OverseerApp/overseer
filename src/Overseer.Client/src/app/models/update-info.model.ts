export interface UpdateInfo {
  currentVersion?: string;
  latestVersion?: string;
  updateAvailable: boolean;
  canAutoUpdate: boolean;
  releaseUrl?: string;
  downloadUrl?: string;
  releaseNotes?: string;
  publishedAt?: string;
  isPreRelease: boolean;
}

export interface UpdateResult {
  success: boolean;
  message?: string;
  version?: string;
}
