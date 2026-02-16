import { CdkDrag, CdkDragDrop, CdkDropList, moveItemInArray } from '@angular/cdk/drag-drop';
import { NgClass } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { I18NextPipe } from 'angular-i18next';
import { CardSectionComponent } from '../../components/card-section/card-section.component';
import { MachinesService } from '../../services/machines.service';

@Component({
  selector: 'app-list-machines',
  templateUrl: './list-machines.component.html',
  styleUrl: './list-machines.component.scss',
  imports: [I18NextPipe, RouterLink, CdkDropList, CdkDrag, NgClass, CardSectionComponent],
})
export class ListMachinesComponent {
  private machinesService = inject(MachinesService);
  machines = computed(() => {
    if (this.machinesService.machines.error()) return [];
    if (this.machinesService.machines.isLoading()) return [];

    const machines = this.machinesService.machines.value() ?? [];
    machines.sort((a, b) => a.sortIndex - b.sortIndex);
    return machines;
  });

  moveUp(index: number) {
    if (index <= 0) return;
    this.move(index, --index);
  }

  moveDown(index: number) {
    if (index >= this.machines().length) return;
    this.move(index, ++index);
  }

  move(previousIndex: number, currentIndex: number) {
    const machines = this.machines();
    if (!machines?.length) return;

    moveItemInArray(machines, previousIndex, currentIndex);
    this.machinesService.sortMachines(machines.map((m) => m.id)).subscribe();
  }

  drop(event: CdkDragDrop<string[]>) {
    this.move(event.previousIndex, event.currentIndex);
  }
}
