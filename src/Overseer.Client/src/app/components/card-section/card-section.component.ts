import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-card-section',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './card-section.component.html',
})
export class CardSectionComponent {
  @Input() icon: string = '';
  @Input() heading: string = '';
}
