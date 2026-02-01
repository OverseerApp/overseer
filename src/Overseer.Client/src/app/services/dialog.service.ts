import { inject, Injectable, Type } from '@angular/core';
import { NgbModal, NgbModalOptions, NgbModalRef } from '@ng-bootstrap/ng-bootstrap';
import { AlertComponent, AlertOptions } from '../components/alert/alert.component';
import { PromptComponent, PromptOptions } from '../components/prompt/prompt.component';

@Injectable()
export class DialogService {
  private modalService = inject(NgbModal);

  show<T, O extends Record<string, unknown>>(content: Type<T>, options?: O): NgbModalRef {
    const modalOptionKeys = new Set<keyof NgbModalOptions>([
      'animation',
      'backdrop',
      'beforeDismiss',
      'centered',
      'container',
      'fullscreen',
      'keyboard',
      'modalDialogClass',
      'scrollable',
      'size',
      'windowClass',
      'backdropClass',
      'ariaLabelledBy',
      'ariaDescribedBy',
    ]);

    const modalOptions: NgbModalOptions = {};
    const componentOptions: Record<string, unknown> = {};

    if (options) {
      for (const [key, value] of Object.entries(options)) {
        if (modalOptionKeys.has(key as keyof NgbModalOptions)) {
          (modalOptions as Record<string, unknown>)[key] = value;
        } else {
          componentOptions[key] = value;
        }
      }
    }

    const modal = this.modalService.open(content, modalOptions);
    if (Object.keys(componentOptions).length > 0) {
      Object.assign(modal.componentInstance, componentOptions);
    }
    return modal;
  }

  alert(options: AlertOptions = {}): NgbModalRef {
    const modal = this.modalService.open(AlertComponent);
    const alert = modal.componentInstance as AlertComponent;
    alert.options.set({
      titleKey: 'warning',
      messageKey: 'areYourSure',
      actionTextKey: 'ok',
      ...options,
    });
    return modal;
  }

  prompt(options: PromptOptions = {}): NgbModalRef {
    const modal = this.modalService.open(PromptComponent);
    modal.closed.subscribe;
    const prompt = modal.componentInstance as PromptComponent;
    prompt.options.set({
      titleKey: 'warning',
      messageKey: 'areYourSure',
      negativeActionTextKey: 'no',
      positiveActionTextKey: 'yes',
      ...options,
    });
    return modal;
  }
}
